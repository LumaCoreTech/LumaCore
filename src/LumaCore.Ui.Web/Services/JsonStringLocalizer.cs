// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Ui.Web.Models;

using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// Implements <see cref="IStringLocalizer"/> with JSON-based translations.
/// Scoped per user - tracks locale preference and delegates translation lookups to <see cref="TranslationRepository"/>.
/// </summary>
/// <remarks>
///     <para>
///     This localizer maintains the user's current locale preference and provides access to translations
///     through Blazor's standard <see cref="IStringLocalizer"/> interface.
///     </para>
///     <para>
///     In Blazor Server, each circuit has its own instance, allowing different users to have different
///     language preferences. In WASM, there's only one user per application instance.
///     </para>
/// </remarks>
public sealed class JsonStringLocalizer : IStringLocalizer
{
	private readonly IJSRuntime            mJsRuntime;
	private readonly TranslationRepository mRepository;

	private bool mIsInitialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonStringLocalizer"/> class.
	/// </summary>
	/// <param name="repository">The translation repository (Singleton).</param>
	/// <param name="jsRuntime">The JavaScript runtime for localStorage access.</param>
	public JsonStringLocalizer(TranslationRepository repository, IJSRuntime jsRuntime)
	{
		mRepository = repository;
		mJsRuntime = jsRuntime;
	}

	/// <summary>
	/// Gets the current locale code (e.g., <c>en</c>, <c>de</c>).
	/// </summary>
	public string CurrentLocale { get; private set; } = "en";

	/// <summary>
	/// Gets the localized string for the specified key.
	/// </summary>
	/// <param name="name">The translation key (supports nested keys like <c>components.login.title</c>).</param>
	/// <returns>A <see cref="LocalizedString"/> containing the translation or a placeholder if not found.</returns>
	public LocalizedString this[string name]
	{
		get
		{
			if (!mIsInitialized)
				return new LocalizedString(name, $"?? {name} ??", resourceNotFound: true);

			Translation translation = mRepository.GetTranslation(CurrentLocale, name);
			return new LocalizedString(
				name,
				translation.Value ?? $"?? {name} ??",
				resourceNotFound: translation.Value == null,
				searchedLocation: translation.SearchedLocation);
		}
	}

	/// <summary>
	/// Gets the localized string for the specified key with formatting arguments.
	/// </summary>
	/// <param name="name">The translation key.</param>
	/// <param name="arguments">The format arguments.</param>
	/// <returns>A <see cref="LocalizedString"/> containing the formatted translation.</returns>
	public LocalizedString this[string name, params object[] arguments]
	{
		get
		{
			LocalizedString localizedString = this[name];
			return !localizedString.ResourceNotFound
				       ? new LocalizedString(name, string.Format(localizedString.Value, arguments))
				       : localizedString;
		}
	}

	/// <summary>
	/// Gets all localized strings.
	/// </summary>
	/// <param name="includeParentCultures">Whether to include parent cultures (not implemented).</param>
	/// <returns>An enumerable of all localized strings (currently returns empty).</returns>
	/// <remarks>
	/// This method is required by <see cref="IStringLocalizer"/> but not used in LumaCore's implementation.
	/// </remarks>
	public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
	{
		// Not implemented - not needed for LumaCore's use case.
		return [];
	}

	/// <summary>
	/// Gets the list of available locales from the repository.
	/// </summary>
	/// <returns>A read-only list of available locales.</returns>
	public Task<IReadOnlyList<LocaleInfo>> GetAvailableLocalesAsync()
	{
		return mRepository.GetAvailableLocalesAsync();
	}

	/// <summary>
	/// Initializes the localizer by loading the user's preferred locale from localStorage.
	/// </summary>
	/// <remarks>
	/// This method should be called once during application startup in <c>MainLayout.OnInitializedAsync</c>.
	/// It loads the translations for the selected locale before marking the localizer as initialized.
	/// </remarks>
	public async Task InitializeAsync()
	{
		try
		{
			// Get saved locale from localStorage.
			string? savedLocale = await mJsRuntime
				                      .InvokeAsync<string?>("localStorage.getItem", "locale")
				                      .ConfigureAwait(false);

			string locale = string.IsNullOrEmpty(savedLocale) ? "en" : savedLocale;

			// Validate locale exists.
			IReadOnlyList<LocaleInfo> availableLocales =
				await mRepository.GetAvailableLocalesAsync().ConfigureAwait(false);

			// Fallback to English if saved locale doesn't exist.
			if (availableLocales.All(l => l.Code != locale))
				locale = "en";

			// CRITICAL: Load translations BEFORE marking as initialized.
			await mRepository.LoadTranslationsAsync(locale).ConfigureAwait(false);

			CurrentLocale = locale;
			mIsInitialized = true;
		}
		catch
		{
			// Fallback to English on any error.
			try
			{
				await mRepository.LoadTranslationsAsync("en").ConfigureAwait(false);
				CurrentLocale = "en";
				mIsInitialized = true;
			}
			catch
			{
				// Even English failed - mark as initialized with empty translations to prevent crashes.
				CurrentLocale = "en";
				mIsInitialized = true;
			}
		}
	}

	/// <summary>
	/// Gets a value indicating whether the localizer has been initialized.
	/// </summary>
	public bool IsInitialized() => mIsInitialized;

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
		// Persist to localStorage before reload.
		await mJsRuntime.InvokeVoidAsync("localStorage.setItem", "locale", locale).ConfigureAwait(false);

		// Reload page to apply new locale.
		await mJsRuntime.InvokeVoidAsync("location.reload").ConfigureAwait(false);
	}
}
