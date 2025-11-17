// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Serilog;

public static partial class Program
{
	/// <summary>
	/// Configures middleware, developer utilities and endpoint mapping.
	/// </summary>
	/// <param name="app">The <see cref="WebApplication"/> used to define the HTTP pipeline.</param>
	private static void ConfigurePipeline(WebApplication app)
	{
		if (app.Environment.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
			app.UseSwagger();
			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint("/swagger/v1/swagger.json", "LumaCore API v1");
				c.RoutePrefix = "swagger";
			});
		}

		app.UseHttpsRedirection();

		app.UseRouting();

		if (app.Environment.IsDevelopment())
			app.UseCors("DevOpen");

		// Structured request logging (per-request summary line)
		app.UseSerilogRequestLogging(opts =>
		{
			opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
		});

		app.UseResponseCompression();

		// Endpoint mapping
		app.MapControllers();

		// Split health endpoints to avoid conflicts
		app.MapHealthChecks("/health"); // readiness/probes via HealthChecks
		app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
			.WithName("HealthLive")
			.WithDescription("Liveness probe (static OK).");

		// Fallback for root
		app.MapGet("/", () => Results.Text("LumaCore API is running.", "text/plain"));
	}
}
