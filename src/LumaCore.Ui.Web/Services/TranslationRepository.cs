// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.Concurrent;
using System.Text.Json;

using LumaCore.Ui.Web.Models;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// Singleton repository that loads and caches translations on-demand.
/// Thread-safe through per-locale semaphores for parallel loading and Volatile reads/writes with locks for state updates.
/// </summary>
/// <remarks>
///     <para>
///     This repository loads translation files via HTTP on-demand when <see cref="LoadTranslationsAsync"/>
///     is called for a specific locale. Translations are cached in memory after loading. Only locales
///     that are actually used are loaded and cached.
///     </para>
///     <para>
///     <b>Thread-Safety:</b> Each locale has its own <see cref="SemaphoreSlim"/> to allow parallel loading
///     of different locales while preventing duplicate loads of the same locale. Reads use
///     <see cref="Volatile.Read{T}(ref readonly T)"/> for lock-free access. State updates are protected by a lock
///     and use <see cref="Volatile.Write{T}(ref T, T)"/> to atomically publish new immutable state snapshots.
///     This ensures cache coherency across CPU cores while maintaining high read performance and parallel loading
///     capability.
///     </para>
/// </remarks>
public sealed partial class TranslationRepository
{
	private readonly IHttpClientFactory                          mHttpClientFactory;
	private readonly ConcurrentDictionary<string, SemaphoreSlim> mLocaleSemaphores = new();
	private readonly Lock                                        mLock             = new();

	/// <summary>
	/// The cached repository state. Accessed via <see cref="Volatile"/> for thread-safe reads
	/// and locked writes with atomic publication.
	/// </summary>
	private RepositoryState? mState;

	/// <summary>
	/// Initializes a new instance of the <see cref="TranslationRepository"/> class.
	/// </summary>
	/// <param name="httpClientFactory">The HTTP client factory for loading translation files.</param>
	public TranslationRepository(IHttpClientFactory httpClientFactory)
	{
		mHttpClientFactory = httpClientFactory;
	}

	/// <summary>
	/// Gets the list of available locales from the manifest file.
	/// </summary>
	/// <returns>A read-only list of available locales in manifest order.</returns>
	/// <remarks>
	/// Results are cached after the first call. Returns English-only fallback if manifest cannot be loaded.
	/// Locales appear in the order they are listed in the manifest.json file.
	/// Thread-safe - uses lock-free reads with locked writes.
	/// </remarks>
	public async Task<IReadOnlyList<LocaleInfo>> GetAvailableLocalesAsync()
	{
		// Fast path: Return cached list if available (lock-free read)
		RepositoryState? state = Volatile.Read(ref mState);
		if (state?.AvailableLocales != null)
			return state.AvailableLocales;

		try
		{
			// Load manifest file.
			HttpClient httpClient = mHttpClientFactory.CreateClient("StaticFilesHttpClient");
			string json = await httpClient.GetStringAsync("locales/manifest.json").ConfigureAwait(false);

			// Parse JSON and extract locales.
			using JsonDocument doc = JsonDocument.Parse(json);
			JsonElement localesArray = doc.RootElement.GetProperty("locales");

			// Convert to list of LocaleInfo. Maintain order from manifest.
			var locales = new List<LocaleInfo>();
			foreach (JsonElement locale in localesArray.EnumerateArray())
			{
				string code = locale.GetProperty("code").GetString() ?? "en";
				string nativeName = locale.GetProperty("nativeName").GetString() ?? code;
				locales.Add(new LocaleInfo(code, nativeName));
			}

			IReadOnlyList<LocaleInfo> sortedLocales = locales.AsReadOnly();

			// Update state with lock and atomic publish
			lock (mLock)
			{
				// Double-check inside lock
				RepositoryState? current = Volatile.Read(ref mState);
				if (current?.AvailableLocales != null)
					return current.AvailableLocales;

				// Create new state with loaded locales
				var updated = new RepositoryState(
					current?.AllTranslations ?? new Dictionary<string, IReadOnlyDictionary<string, object>>(),
					sortedLocales);

				// Atomic publish
				Volatile.Write(ref mState, updated);

				// Return from the state we just published
				return updated.AvailableLocales;
			}
		}
		catch
		{
			// Loading manifest failed.
			// Fallback to English only.
			IReadOnlyList<LocaleInfo> fallback = new List<LocaleInfo> { new("en", "English") }.AsReadOnly();

			lock (mLock)
			{
				RepositoryState? current = Volatile.Read(ref mState);
				if (current?.AvailableLocales != null)
					return current.AvailableLocales;

				var updated = new RepositoryState(
					current?.AllTranslations ?? new Dictionary<string, IReadOnlyDictionary<string, object>>(),
					fallback);

				Volatile.Write(ref mState, updated);

				// Return from the state we just published
				return updated.AvailableLocales;
			}
		}
	}

	/// <summary>
	/// Gets a translation for the specified locale and key.
	/// </summary>
	/// <param name="locale">The locale code (e.g., <c>en</c>, <c>de</c>).</param>
	/// <param name="key">The translation key (supports nested keys like <c>components.login.title</c>).</param>
	/// <returns>The translated string, or <see langword="null"/> if not found.</returns>
	/// <remarks>
	/// This method is synchronous and assumes translations for the locale are already loaded.
	/// Translations must be loaded via <see cref="LoadTranslationsAsync"/> before calling this method.
	/// Thread-safe - uses lock-free <see cref="Volatile.Read{T}(ref readonly T)"/> for high-performance concurrent access.
	/// </remarks>
	public string? GetTranslation(string locale, string key)
	{
		// Lock-free read using Volatile.Read
		RepositoryState? state = Volatile.Read(ref mState);

		// Get translations for this locale (must already be loaded!)
		if (state?.AllTranslations == null ||
		    !state.AllTranslations.TryGetValue(locale, out IReadOnlyDictionary<string, object>? translations))
		{
			return null;
		}

		// Navigate nested structure
		return GetNestedValue(translations, key);
	}

	/// <summary>
	/// Loads translation data for the specified locale.
	/// </summary>
	/// <param name="locale">The locale code (e.g., <c>en</c>, <c>de</c>).</param>
	/// <remarks>
	/// This method is async to avoid blocking in WASM. Translations are cached after loading.
	/// Call this method during initialization before attempting to retrieve translations.
	/// Thread-safe - uses per-locale semaphores to allow parallel loading of different locales while preventing
	/// duplicate loads of the same locale. State updates use <see langword="lock"/> and volatile read/write
	/// for atomic publication.
	/// </remarks>
	public async Task LoadTranslationsAsync(string locale)
	{
		// Fast path: Check if already loaded (lock-free read)
		RepositoryState? state = Volatile.Read(ref mState);
		if (state?.AllTranslations.ContainsKey(locale) == true)
			return;

		// Get or create semaphore for THIS locale (allows parallel loading of different locales)
		SemaphoreSlim semaphore = mLocaleSemaphores.GetOrAdd(locale, _ => new SemaphoreSlim(1, 1));

		// Acquire semaphore for this locale (prevents duplicate loads of same locale)
		await semaphore.WaitAsync().ConfigureAwait(false);
		try
		{
			// Double-check: Another thread might have loaded it while we were waiting
			state = Volatile.Read(ref mState);
			if (state?.AllTranslations.ContainsKey(locale) == true)
				return;

			// Load translation (expensive I/O operation, but other locales can load in parallel!)
			IReadOnlyDictionary<string, object>? translations;

			try
			{
				HttpClient httpClient = mHttpClientFactory.CreateClient("StaticFilesHttpClient");
				string url = $"locales/{locale}/translations.json";
				string json = await httpClient.GetStringAsync(url).ConfigureAwait(false);

				using JsonDocument doc = JsonDocument.Parse(json);

				// Convert JsonDocument to nested dictionaries.
				translations = ConvertToNestedDictionary(doc.RootElement);
			}
			catch
			{
				// If loading fails, use empty dictionary to prevent repeated failed attempts.
				translations = new Dictionary<string, object>();
			}

			// Update state with lock (protects mState writes, short critical section)
			lock (mLock)
			{
				// Read current state
				RepositoryState? current = Volatile.Read(ref mState);

				// Create new immutable state with added translation
				var allTranslations = new Dictionary<string, IReadOnlyDictionary<string, object>>(
					current?.AllTranslations ?? new Dictionary<string, IReadOnlyDictionary<string, object>>())
				{
					[locale] = translations
				};

				var updated = new RepositoryState(
					allTranslations,
					current?.AvailableLocales ?? new List<LocaleInfo>().AsReadOnly());

				// Atomic publish - ensures cache coherency across CPU cores
				Volatile.Write(ref mState, updated);
			}
		}
		finally
		{
			semaphore.Release();
		}
	}

	/// <summary>
	/// Converts a JsonElement to an appropriate .NET object.
	/// </summary>
	/// <param name="element">The JSON element to convert.</param>
	/// <returns>A string for leaf values, or a nested dictionary for objects.</returns>
	private static object ConvertJsonElement(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.String => element.GetString() ?? "",
			JsonValueKind.Object => ConvertToNestedDictionary(element),
			var _                => element.ToString()
		};
	}

	/// <summary>
	/// Converts a JsonElement to a nested dictionary structure.
	/// </summary>
	/// <param name="element">The JSON element to convert.</param>
	/// <returns>A nested dictionary representing the JSON structure.</returns>
	private static IReadOnlyDictionary<string, object> ConvertToNestedDictionary(JsonElement element)
	{
		// Create dictionary to hold properties of the JSON object
		// Recursively convert each property value to appropriate .NET type
		var dict = new Dictionary<string, object>();
		foreach (JsonProperty property in element.EnumerateObject())
		{
			dict[property.Name] = ConvertJsonElement(property.Value);
		}

		return dict;
	}

	/// <summary>
	/// Navigates through nested dictionaries to find a translation value.
	/// </summary>
	/// <param name="dict">The root translation dictionary.</param>
	/// <param name="key">The dot-separated key path (e.g., <c>components.navbar.logout</c>).</param>
	/// <returns>The translated string, or <see langword="null"/> if not found.</returns>
	private static string? GetNestedValue(IReadOnlyDictionary<string, object> dict, string key)
	{
		string[] keys = key.Split('.');

		object current = dict;

		foreach (string part in keys)
		{
			if (current is IReadOnlyDictionary<string, object> d && d.TryGetValue(part, out object? value))
				current = value;
			else
				return null;
		}

		return current as string;
	}
}
