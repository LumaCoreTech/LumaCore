// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

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

		return builder.Build().RunAsync();
	}
}
