// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Ui.Web.Services;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Localization;

namespace LumaCore.Ui.Web;

/// <summary>
/// Entry point for the LumaCore Blazor WebAssembly application.
/// </summary>
public class Program
{
	/// <summary>
	/// Application entry point that configures and starts the Blazor WebAssembly host.
	/// </summary>
	/// <param name="args">Command-line arguments passed to the application.</param>
	/// <returns>A task that completes when the application shuts down.</returns>
	public static Task Main(string[] args)
	{
		var builder = WebAssemblyHostBuilder.CreateDefault(args);

		// NOTE: For now we use the default root component mapping.
		// You can later embed this UI under a different DOM node if needed.
		builder.RootComponents.Add<App>("#app");
		builder.RootComponents.Add<HeadOutlet>("head::after");

		// Read backend base URL from configuration (optional).
		// If not set, fall back to the Blazor host base address (same origin scenario).
		string? backendBaseUrl = builder.Configuration["LumaCore:BackendBaseUrl"];

		// Compute effective backend URL once for reuse.
		string effectiveBaseUrl = !string.IsNullOrWhiteSpace(backendBaseUrl)
			                          ? backendBaseUrl
			                          : builder.HostEnvironment.BaseAddress;

		// Ensure the URL ends with a trailing slash so relative paths behave as expected.
		if (!effectiveBaseUrl.EndsWith("/", StringComparison.Ordinal))
		{
			effectiveBaseUrl += "/";
		}

		Console.WriteLine($"[LumaCore UI] Effective backend base URL: {effectiveBaseUrl}");

		// Register the HTTP message handlers as transient (created per HttpClient instance).
		builder.Services.AddTransient<CookieCredentialHandler>();
		builder.Services.AddTransient<HealthTrackingHandler>();

		// Named HttpClient for API requests with automatic cookie credential inclusion and health tracking.
		// This client targets the backend API and automatically:
		// - Includes the HttpOnly authentication cookie via CookieCredentialHandler
		// - Tracks backend health via HealthTrackingHandler
		builder.Services.AddHttpClient(
				"ApiHttpClient",
				client => client.BaseAddress = new Uri(effectiveBaseUrl))
			.AddHttpMessageHandler<CookieCredentialHandler>()
			.AddHttpMessageHandler<HealthTrackingHandler>();

		// Register a default HttpClient that resolves to the API client.
		// Services that inject HttpClient directly (like AuthService) will get this client.
		builder.Services.AddScoped(sp =>
			sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiHttpClient"));

		// Named HttpClient for loading static files from Blazor app's wwwroot directory.
		// This client targets the Blazor app itself (builder.HostEnvironment.BaseAddress)
		// and can be used by any service that needs to load static resources like:
		// - Translation files (locales/*.json)
		// - Theme files (themes/*.css)
		// - Configuration files (data/*.json)
		// - Static assets (images, fonts, etc.)
		//
		// This client does NOT use CookieCredentialHandler since static files don't require auth.
		builder.Services.AddHttpClient(
			"StaticFilesHttpClient",
			client =>
			{
				client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
				Console.WriteLine(
					$"[LumaCore UI] StaticFilesHttpClient base URL: {builder.HostEnvironment.BaseAddress}");
			});

		// Register authentication services.
		builder.Services.AddScoped<AuthService>();
		builder.Services.AddScoped<CookieAuthenticationStateProvider>();
		builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
			sp.GetRequiredService<CookieAuthenticationStateProvider>());

		// Register backend health state for passive health monitoring.
		// Updated by HealthTrackingHandler, observed by BackendHealthIndicator.
		builder.Services.AddScoped<BackendHealthState>();

		// Register localization services for i18n support.
		// We register both IStringLocalizer (via factory) and JsonStringLocalizer directly:
		//
		// - JsonStringLocalizer (direct, Scoped): Our concrete implementation with custom methods
		//   (CurrentLocale, InitializeAsync, SetLocaleAsync, GetAvailableLocalesAsync).
		//   Components inject this directly to access both standard IStringLocalizer
		//   functionality and our extensions.
		//
		// - IStringLocalizer (via factory): Required by Blazor's validation system.
		//   Validation attributes like [Required(ErrorMessage = "key")] automatically
		//   look up IStringLocalizer from DI. The factory delegates to the DI container
		//   to return the SAME JsonStringLocalizer instance, ensuring state consistency.
		//
		// This setup ensures only ONE instance exists per scope, preventing issues where
		// changing locale in the component wouldn't affect validation messages.
		builder.Services.AddLocalization();
		builder.Services.AddSingleton<TranslationRepository>();
		builder.Services.AddScoped<JsonStringLocalizer>();
		builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();

		// Add authorization services for [Authorize] attribute support.
		builder.Services.AddAuthorizationCore();

		return builder.Build().RunAsync();
	}
}
