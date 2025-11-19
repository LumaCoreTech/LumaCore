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
		// In development, enable Serilog's self-diagnostics so that configuration
		// or sink issues are written to stderr. This is very useful for debugging
		// logging problems, but should not be enabled in production.
		if (builder.Environment.IsDevelopment())
		{
			SelfLog.Enable(msg => Console.Error.WriteLine($"[Serilog-SelfLog] {msg}"));
		}

		// Remove all default logging providers (Console, Debug, EventSource, ...)
		// so that Serilog becomes the single, authoritative logging pipeline.
		// This avoids duplicate log entries and keeps output formatting consistent.
		builder.Logging.ClearProviders();

		// Configure Serilog as the host logger. The configuration is read from
		// appsettings.json (and environment-specific variants) and can leverage
		// services from the DI container (for enrichers, sinks, etc.).
		builder.Host.UseSerilog((context, services, configuration) =>
		{
			configuration
				// Load sinks, minimum levels, enrichers, etc. from configuration.
				.ReadFrom.Configuration(context.Configuration)
				// Allow DI-registered components (e.g. enrichers) to participate.
				.ReadFrom.Services(services);
		});
	}
}
