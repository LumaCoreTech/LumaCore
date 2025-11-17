// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

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
		// 1. Bootstrap logger: minimal console output before config
		// -------------------------------------------------------
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.WriteTo.Console()
			.CreateBootstrapLogger();

		try
		{
			Log.Information("Starting LumaCore.Api...");

			// ---------------------------------------------------
			// 2. Build the web host (uses helpers below)
			// ---------------------------------------------------
			var builder = WebApplication.CreateBuilder(args);

			// Configure logging (reads Serilog from appsettings.json)
			ConfigureSerilog(builder);

			// Register services (DI container)
			ConfigureServices(builder);

			var app = builder.Build();

			// Configure request pipeline
			ConfigurePipeline(app);

			Log.Information("LumaCore API ready to accept requests on {Url}",
				app.Urls.FirstOrDefault() ?? "http://localhost:5080");

			// ---------------------------------------------------
			// 3. Run the web host
			// ---------------------------------------------------
			await app.RunAsync();
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "LumaCore.Api terminated unexpectedly");
		}
		finally
		{
			await Log.CloseAndFlushAsync().ConfigureAwait(false);
		}
	}
}
