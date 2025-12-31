// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Ui.Web.Services;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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

		// HttpClient configured with a placeholder base address.
		builder.Services.AddScoped(_ =>
		{
			// Use configured backend URL if present; otherwise default to the host base address.
			string effectiveBaseUrl = !string.IsNullOrWhiteSpace(backendBaseUrl)
				                          ? backendBaseUrl
				                          : builder.HostEnvironment.BaseAddress;

			// Ensure the URL ends with a trailing slash so relative paths behave as expected.
			if (!effectiveBaseUrl.EndsWith("/", StringComparison.Ordinal))
			{
				effectiveBaseUrl += "/";
			}

			Console.WriteLine($"[LumaCore UI] Effective backend base URL: {effectiveBaseUrl}");

			return new HttpClient
			{
				BaseAddress = new Uri(effectiveBaseUrl)
			};
		});

		// Named HttpClient for loading static files from Blazor app's wwwroot directory.
		// This client targets the Blazor app itself (builder.HostEnvironment.BaseAddress)
		// and can be used by any service that needs to load static resources like:
		// - Translation files (locales/*.json)
		// - Theme files (themes/*.css)
		// - Configuration files (data/*.json)
		// - Static assets (images, fonts, etc.)
		//
		// This is separate from the default HttpClient which targets the backend API.
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
		builder.Services.AddScoped<JwtAuthenticationStateProvider>();
		builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
			sp.GetRequiredService<JwtAuthenticationStateProvider>());

		// Register localization service for i18n support.
		builder.Services.AddScoped<LocalizationService>();

		// Add authorization services for [Authorize] attribute support.
		builder.Services.AddAuthorizationCore();

		return builder.Build().RunAsync();
	}
}
