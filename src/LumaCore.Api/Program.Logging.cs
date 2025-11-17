// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Serilog;
using Serilog.Debugging;

public static partial class Program
{
	/// <summary>
	/// Configures Serilog so that the full logging pipeline is driven by <c>appsettings.json</c>.
	/// A lightweight bootstrap logger is created before configuration to ensure early visibility of startup events.
	/// </summary>
	/// <param name="builder">The web application builder to attach Serilog to.</param>
	private static void ConfigureSerilog(WebApplicationBuilder builder)
	{
		// Optional: enable self-diagnostics for configuration issues (DEV only).
		if (builder.Environment.IsDevelopment())
		{
			SelfLog.Enable(msg => Console.Error.WriteLine($"[Serilog-SelfLog] {msg}"));
		}

		// Clear default logging providers to avoid duplicate console output.
		builder.Logging.ClearProviders();

		// Hook Serilog into the generic host.
		builder.Host.UseSerilog((context, services, configuration) =>
		{
			configuration
				.ReadFrom.Configuration(context.Configuration) // Use appsettings.json
				.ReadFrom.Services(services);                  // Support for DI-based enrichers
		});
	}
}
