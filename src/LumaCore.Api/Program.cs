// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

/// <summary>
/// Entry point of the LumaCore API application.
/// Wires up logging, services, HTTP pipeline and starts the server.
/// </summary>
public static partial class Program
{
	/// <summary>
	/// Application entry method. Builds and starts the ASP.NET Core web host.
	/// </summary>
	/// <param name="args">Optional command-line arguments.</param>
	public static async Task Main(string[] args)
	{
		// -------------------------------------------------------
		// 1. Bootstrap logger (very early logging)
		// -------------------------------------------------------
		// Before the host is built, we create a minimal Serilog instance so that
		// startup messages and configuration errors are visible immediately.
		// This logger is replaced later by the fully configured Serilog pipeline.
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.WriteTo.Console()
			.CreateBootstrapLogger();

		try
		{
			Log.Information("Starting LumaCore.Api...");

			// ---------------------------------------------------
			// 2. Build the web application host
			// ---------------------------------------------------
			WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

			// Configure Serilog as the application-wide logging system.
			// This replaces the bootstrap logger with the fully configured logger.
			ConfigureSerilog(builder);

			// Register all services (controllers, Swagger, CORS, health checks, ...)
			// required by the API. See Program.Services.cs for details.
			ConfigureServices(builder);

			// Build the actual web application instance.
			WebApplication app = builder.Build();

			// Configure the HTTP request pipeline (middleware, routing, Swagger UI, ...)
			// This is where request processing flow is defined.
			ConfigurePipeline(app);

			Log.Information(
				"LumaCore API ready to accept requests on {Url}",
				app.Urls.FirstOrDefault() ?? "http://localhost:5080");

			// ---------------------------------------------------
			// 3. Run the web host
			// ---------------------------------------------------
			// This starts the Kestrel server and begins listening for requests.
			await app.RunAsync();
		}
		catch (Exception ex)
		{
			// Fatal startup or runtime errors are logged here. Because Serilog is
			// already initialized, this will reliably appear in console and sinks.
			Log.Fatal(ex, "LumaCore.Api terminated unexpectedly");
		}
		finally
		{
			// Ensure all buffered log messages are written out before the process exits.
			await Log.CloseAndFlushAsync().ConfigureAwait(false);
		}
	}
}
