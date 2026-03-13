// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text;

using LumaCore.Configuration;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Provides extension methods for registering authentication services with the dependency injection container.
/// </summary>
/// <remarks>
/// Configures JWT-based authentication for the LumaCore API, including token validation, authorization, and the
/// <see cref="IJwtTokenFactory"/> for issuing tokens.
/// </remarks>
static class ServiceRegistration
{
	/// <summary>
	/// Registers JWT authentication, authorization, options binding, and supporting services using the
	/// <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <returns>The modified application builder.</returns>
	/// <remarks>
	/// This is a convenience wrapper that forwards to <see cref="AddAuthFeatureCore"/> using the
	/// <see cref="IServiceCollection"/> and <see cref="IConfiguration"/> exposed by the builder.
	/// </remarks>
	public static WebApplicationBuilder AddAuthFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddAuthFeatureCore(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// Registers JWT authentication, authorization, options binding, and supporting services using the underlying
	/// <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register authentication services with.</param>
	/// <param name="configuration">The application configuration used to bind <see cref="JwtOptions"/>.</param>
	/// <returns>The original <see cref="IServiceCollection"/> for fluent chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method wires up the complete authentication stack for the LumaCore HTTP API: it binds
	///     <see cref="JwtOptions"/>, configures the JWT bearer handler, and registers authorization services and the
	///     <see cref="IJwtTokenFactory"/>.
	///     </para>
	///     <para>
	///     It is factored to operate on <see cref="IServiceCollection"/> and <see cref="IConfiguration"/> so that it
	///     can be reused in other hosting scenarios (for example background workers using the same JWT infrastructure)
	///     and easily unit-tested.
	///     </para>
	/// </remarks>
	public static IServiceCollection AddAuthFeatureCore(
		this IServiceCollection services,
		IConfiguration          configuration)
	{
		// Bind and validate JWT options at startup so misconfiguration fails fast.
		services.AddFeatureOptions<JwtOptions>(configuration, JwtOptions.SectionName);

		// Configure JWT bearer authentication for incoming requests.
		services
			.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
			.AddJwtBearer(options =>
			{
				options.Events = new JwtBearerEvents
				{
					// Log authentication failures to help diagnose issues.
					// This includes expired tokens, invalid signatures, malformed JWTs, etc.
					OnAuthenticationFailed = context =>
					{
						// Create a logger scoped to JWT bearer authentication.
						ILogger logger = context.HttpContext.RequestServices
							.GetRequiredService<ILoggerFactory>()
							.CreateLogger("AuthFeature.JwtBearer");

						// Log the exception with request path for context.
						// This helps identify which endpoint caused the failure.
						logger.LogWarning(
							context.Exception,
							"JWT authentication failed for request to {Path}",
							context.HttpContext.Request.Path);

						return Task.CompletedTask;
					},

					// Log successful token validation to help trace security flows.
					// This can help track authentication flows without polluting normal logs.
					// The log level is set to Debug to keep it low-verbosity.
					OnTokenValidated = context =>
					{
						// Create a logger scoped to JWT bearer authentication.
						ILogger logger = context.HttpContext.RequestServices
							.GetRequiredService<ILoggerFactory>()
							.CreateLogger("AuthFeature.JwtBearer");

						// Extract the subject (sub claim) or fallback to the identity name.
						string subject = context.Principal?.FindFirst("sub")?.Value
						                 ?? context.Principal?.Identity?.Name
						                 ?? "(unknown)";

						// Log the successful validation with subject and request path.
						// This helps trace which user authenticated on which endpoint.
						logger.LogDebug(
							"JWT token successfully validated for subject '{Subject}' on request to {Path}",
							subject,
							context.HttpContext.Request.Path);

						return Task.CompletedTask;
					}
				};
			});

		// Configure token validation parameters for the JWT bearer handler using JwtOptions
		// as the single source of truth for issuer, audience, signing key and lifetime.
		services
			.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
			.Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
			{
				JwtOptions jwtOptions = jwtOptionsAccessor.Value;

				byte[] signingKeyBytes = Encoding.UTF8.GetBytes(jwtOptions.SigningKey);

				options.TokenValidationParameters = new TokenValidationParameters
				{
					// Validate the JWT issuer (iss claim).
					ValidateIssuer = true,
					ValidIssuer = jwtOptions.Issuer,

					// Validate the JWT audience (aud claim).
					ValidateAudience = true,
					ValidAudience = jwtOptions.Audience,

					// Validate the token signature.
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),

					// Validate the token expiry (exp claim) and reject expired tokens.
					ValidateLifetime = true,

					// Allow a small clock skew to account for minor time differences
					// between clients and the server.
					ClockSkew = TimeSpan.FromSeconds(30)
				};
			});

		// Register authorization and the token factory used by the login endpoint.
		services.AddAuthorization();
		services.AddSingleton<IJwtTokenFactory, JwtTokenFactory>();

		return services;
	}
}
