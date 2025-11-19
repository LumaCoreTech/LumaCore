// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Reflection;

using Microsoft.OpenApi;

public static partial class Program
{
	/// <summary>
	/// Registers all services, options, and health checks used by the LumaCore API.
	/// </summary>
	/// <param name="builder">The <see cref="WebApplicationBuilder"/> used to register services.</param>
	/// <remarks>
	///     <para>
	///     This method configures:
	///     <list type="bullet">
	///         <item>Controllers and API endpoints</item>
	///         <item>Response compression for HTTPS</item>
	///         <item>CORS policy for development</item>
	///         <item>Swagger/OpenAPI documentation</item>
	///         <item>Configuration options with validation</item>
	///         <item>Health check implementations for all subsystems</item>
	///     </list>
	///     </para>
	/// </remarks>
	private static void ConfigureServices(WebApplicationBuilder builder)
	{
		// Controllers
		builder.Services.AddControllers();

		// Response compression for reduced bandwidth
		builder.Services.AddResponseCompression(options =>
		{
			options.EnableForHttps = true; // Enable for HTTPS (careful with sensitive data!)
		});

		// CORS policy for development: allow any origin/header/method
		builder.Services.AddCors(options =>
		{
			options.AddPolicy(
				"DevOpen",
				policy =>
					policy.AllowAnyOrigin()
						.AllowAnyHeader()
						.AllowAnyMethod());
		});

		// Swagger/OpenAPI
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen(o =>
		{
			o.SwaggerDoc(
				"v1",
				new OpenApiInfo
				{
					Title = "LumaCore API",
					Version = "v1",
					Description = "LumaCore API Description" // TODO: Add more detailed description.
				});

			// Include XML docs if enabled in the project file.
			string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
			string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
			if (File.Exists(xmlPath))
				o.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
		});

		// Health checks
		builder.Services.AddHealthChecks();
	}
}
