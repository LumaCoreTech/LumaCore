// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text.Json;

using Microsoft.JSInterop;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// Provides localization services for the LumaCore UI.
/// Loads translations from JSON files and provides synchronous translation lookups.
/// </summary>
/// <remarks>
///     <para>
///     This service loads translation files via HTTP and performs all translation lookups
///     in managed code. No JavaScript is used for translations — Blazor renders
///     already-translated strings directly into the DOM.
///     </para>
///     <para>
///     JavaScript is only used for localStorage access (persisting locale preference).
///     </para>
///     <para>
///     <b>Thread-Safety:</b> This implementation assumes single-threaded Blazor WASM execution.
///     The mutable fields (<see cref="mCurrentLocale"/>, <see cref="mTranslations"/>, <see cref="mAvailableLocales"/>)
///     are not thread-safe. A thread-safety review is required if migrating to Blazor Server.
///     </para>
/// </remarks>
public sealed class LocalizationService
{
	private readonly IHttpClientFactory mHttpClientFactory;
	private readonly IJSRuntime         mJsRuntime;

	/// <summary>The cached list of available locales. Not thread-safe.</summary>
	private List<LocaleInfo>? mAvailableLocales;

	/// <summary>The current locale code (e.g., <c>en</c>, <c>de</c>). Not thread-safe.</summary>
	private string mCurrentLocale = "en";

	/// <summary>The loaded translations dictionary. Not thread-safe.</summary>
	private Dictionary<string, JsonElement>? mTranslations;

	/// <summary>
	/// Initializes a new instance of the <see cref="LocalizationService"/> class.
	/// </summary>
	/// <param name="httpClientFactory">The HTTP client factory for creating clients to load translation files.</param>
	/// <param name="jsRuntime">The JavaScript runtime for localStorage access.</param>
	public LocalizationService(IHttpClientFactory httpClientFactory, IJSRuntime jsRuntime)
	{
		mHttpClientFactory = httpClientFactory;
		mJsRuntime = jsRuntime;
	}

	/// <summary>
	/// Gets a value indicating whether the localization service has been initialized.
	/// </summary>
	/// <remarks>
	/// Components should check this property before rendering localized content
	/// to avoid displaying placeholder text while translations are loading.
	/// </remarks>
	public bool IsInitialized => mTranslations != null;

	/// <summary>
	/// Gets the current locale code (e.g., <c>en</c>, <c>de</c>).
	/// </summary>
	/// <remarks>
	/// Returns the cached value. Safe to use after <see cref="InitializeAsync"/> has completed.
	/// In LumaCore, the MainLayout waits for initialization before rendering child components,
	/// so this property is safe to use in all component lifecycle methods.
	/// </remarks>
	public string CurrentLocale => mCurrentLocale;

	/// <summary>
	/// Initializes the localization service by loading the user's preferred locale.
	/// </summary>
	/// <remarks>
	/// This method should be called once during application startup, before any
	/// components that use localization are rendered. Typically called in
	/// <c>MainLayout.OnInitializedAsync</c> or <c>App.razor</c>.
	/// </remarks>
	public async Task InitializeAsync()
	{
		try
		{
			// Get saved locale from localStorage
			string? savedLocale = await mJsRuntime.InvokeAsync<string?>("localStorage.getItem", "locale")
				                      .ConfigureAwait(false);

			string locale = string.IsNullOrEmpty(savedLocale) ? "en" : savedLocale;

			await LoadTranslationsAsync(locale).ConfigureAwait(false);
		}
		catch
		{
			// Fallback to English if initialization fails
			try
			{
				await LoadTranslationsAsync("en").ConfigureAwait(false);
			}
			catch
			{
				// If even English fails, initialize with empty translations
				// to prevent null reference exceptions
				mTranslations = new Dictionary<string, JsonElement>();
				mCurrentLocale = "en";
			}
		}
	}

	/// <summary>
	/// Loads translation data for the specified locale.
	/// </summary>
	/// <param name="locale">The locale code (e.g., <c>en</c>, <c>de</c>).</param>
	/// <exception cref="HttpRequestException">Thrown when the translation file cannot be loaded.</exception>
	private async Task LoadTranslationsAsync(string locale)
	{
		HttpClient httpClient = mHttpClientFactory.CreateClient("StaticFilesHttpClient");

		string url = $"locales/{locale}/translations.json";
		string json = await httpClient.GetStringAsync(url).ConfigureAwait(false);

		mTranslations = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
		mCurrentLocale = locale;
	}

	/// <summary>
	/// Gets a translated string for the specified key.
	/// </summary>
	/// <param name="key">The translation key (supports nested keys like <c>components.settings.header.title</c>).</param>
	/// <returns>The translated string, or <c>?? key ??</c> if not found or not initialized.</returns>
	/// <remarks>
	/// This method is synchronous and performs no JavaScript interop, making it
	/// suitable for use in Razor component render methods.
	/// </remarks>
	public string Get(string key)
	{
		if (mTranslations == null)
			return $"?? {key} ??";

		return GetNestedValue(mTranslations, key) ?? $"?? {key} ??";
	}

	/// <summary>
	/// Navigates through nested JSON keys to find the translation value.
	/// </summary>
	/// <param name="dict">The root translation dictionary.</param>
	/// <param name="key">The dot-separated key path (e.g., <c>components.navbar.logout</c>).</param>
	/// <returns>The translated string or <see langword="null"/> if not found.</returns>
	private static string? GetNestedValue(Dictionary<string, JsonElement> dict, string key)
	{
		string[] keys = key.Split('.');

		// First key must be in the dictionary
		if (!dict.TryGetValue(keys[0], out JsonElement current))
			return null;

		// Navigate through remaining keys in the JsonElement
		for (int i = 1; i < keys.Length; i++)
		{
			if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(keys[i], out JsonElement property))
				current = property;
			else
				return null;
		}

		// Return the string value
		return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
	}

	/// <summary>
	/// Gets the list of available locales from the manifest file.
	/// </summary>
	/// <remarks>
	/// Results are cached after the first call to avoid repeated HTTP requests.
	/// </remarks>
	/// <returns>A list of available locales sorted by display order.</returns>
	public async Task<IReadOnlyList<LocaleInfo>> GetAvailableLocalesAsync()
	{
		// Return cached list if available
		if (mAvailableLocales != null)
			return mAvailableLocales;

		try
		{
			HttpClient httpClient = mHttpClientFactory.CreateClient("StaticFilesHttpClient");
			string json = await httpClient.GetStringAsync("locales/manifest.json").ConfigureAwait(false);

			using JsonDocument doc = JsonDocument.Parse(json);
			JsonElement localesArray = doc.RootElement.GetProperty("locales");

			mAvailableLocales = [];

			foreach (JsonElement locale in localesArray.EnumerateArray())
			{
				string code = locale.GetProperty("code").GetString() ?? "en";
				string nativeName = locale.GetProperty("nativeName").GetString() ?? code;
				int order = locale.TryGetProperty("order", out JsonElement orderElement)
					            ? orderElement.GetInt32()
					            : 999;

				mAvailableLocales.Add(new LocaleInfo(code, nativeName, order));
			}

			mAvailableLocales = mAvailableLocales.OrderBy(l => l.Order).ToList();

			return mAvailableLocales;
		}
		catch
		{
			// Fallback to English only
			mAvailableLocales = [new LocaleInfo("en", "English", 1)];
			return mAvailableLocales;
		}
	}

	/// <summary>
	/// Sets the current locale and reloads the page to apply the change.
	/// </summary>
	/// <param name="locale">The locale code to set (e.g., <c>en</c>, <c>de</c>).</param>
	/// <remarks>
	/// This method persists the locale to localStorage and triggers a full page reload
	/// to ensure all components re-render with the new locale.
	/// </remarks>
	public async Task SetLocaleAsync(string locale)
	{
		// Persist to localStorage before reload
		await mJsRuntime.InvokeVoidAsync("localStorage.setItem", "locale", locale).ConfigureAwait(false);

		// Reload page to apply new locale
		await mJsRuntime.InvokeVoidAsync("location.reload").ConfigureAwait(false);
	}

	/// <summary>
	/// Represents information about an available locale.
	/// </summary>
	/// <param name="Code">The locale code (e.g., <c>en</c>, <c>de</c>).</param>
	/// <param name="NativeName">The native name of the language (e.g., <c>English</c>, <c>Deutsch</c>).</param>
	/// <param name="Order">The display order for sorting.</param>
	public sealed record LocaleInfo(string Code, string NativeName, int Order);
}
