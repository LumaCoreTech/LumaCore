// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text;
using System.Text.RegularExpressions;

using Asp.Versioning.Builder;

using LumaCore.Api.Features.ApiVersioning;

using Microsoft.AspNetCore.Builder;

using Xunit;

namespace LumaCore.Api.Tests.Features.ApiVersioning;

public sealed partial class ApiVersionValidationTests
{
	/// <summary>
	/// Creates a minimal <see cref="WebApplication"/> with API versioning services registered and an
	/// <see cref="ApiVersionSet"/> containing <see cref="ApiVersions.V1"/>. The delegate receives both the
	/// application and the version set — tests decide whether to use the version set (valid endpoints) or
	/// ignore it (endpoints missing versioning metadata).
	/// </summary>
	/// <param name="configureEndpoints">
	/// A delegate that maps endpoints on the application. The second parameter is an <see cref="ApiVersionSet"/>
	/// pre-configured with <see cref="ApiVersions.V1"/>. Pass <see langword="null"/> or an empty delegate to
	/// create an application with no endpoints.
	/// </param>
	/// <returns>A built <see cref="WebApplication"/> with the configured endpoints.</returns>
	private static WebApplication CreateApp(Action<WebApplication, ApiVersionSet>? configureEndpoints = null)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder();
		builder.Services.AddApiVersioningFeatureCore();
		WebApplication app = builder.Build();

		if (configureEndpoints is not null)
		{
			ApiVersionSet versionSet = app.NewApiVersionSet()
				.HasApiVersion(ApiVersions.V1)
				.Build();

			configureEndpoints(app, versionSet);
		}

		return app;
	}

	/// <summary>
	/// Asserts that the exception message matches the exact structure produced by
	/// <see cref="ApiVersionValidation.ValidateExplicitApiVersionMappings"/>. The full message — header,
	/// bullet points, and instructional footer — is validated as a single anchored regex. Only the
	/// framework-generated <c>DisplayName</c> portion of each bullet is treated as dynamic (<c>.+</c>);
	/// route patterns and optional suffixes are matched literally via <see cref="Regex.Escape"/>.
	/// </summary>
	/// <param name="actualMessage">The <see cref="Exception.Message"/> to validate.</param>
	/// <param name="expectedEndpoints">
	/// The expected bullet-point entries in registration order. Each tuple contains the route pattern and an
	/// optional suffix (e.g., <c>"No ApiVersionMetadata"</c> for the <c>metadata is null</c> code path).
	/// </param>
	private static void AssertValidationMessage(
		string                                         actualMessage,
		params (string RoutePattern, string? Suffix)[] expectedEndpoints)
	{
		const string nl = @"\r?\n";

		var pattern = new StringBuilder();
		pattern.Append('^');

		// Header.
		pattern.Append(@"API version validation failed\. The following endpoints are missing ");
		pattern.Append(@"explicit MapToApiVersion\(\) calls:");
		pattern.Append(nl);
		pattern.Append(nl);

		// Bullet points — DisplayName is framework-generated, matched with .+ to tolerate any value.
		foreach ((string routePattern, string? suffix) in expectedEndpoints)
		{
			pattern.Append(@"  • .+ \(");
			pattern.Append(Regex.Escape(routePattern));
			pattern.Append(@"\)");

			if (suffix is not null)
			{
				pattern.Append(" - ");
				pattern.Append(Regex.Escape(suffix));
			}

			pattern.Append(nl);
		}

		// Instructional footer.
		pattern.Append(nl);
		pattern.Append(@"Every versioned API endpoint must explicitly declare its API version\(s\):");
		pattern.Append(nl);
		pattern.Append(nl);
		pattern.Append(@"  group\.MapPost\(""/items"", HandleCreateItem\)");
		pattern.Append(nl);
		pattern.Append(@"      \.MapToApiVersion\(ApiVersions\.V1\);");
		pattern.Append(nl);
		pattern.Append(nl);
		pattern.Append(@"This prevents endpoints from being unintentionally exposed in all API versions\.");
		pattern.Append(nl);
		pattern.Append('$');

		Assert.Matches(pattern.ToString(), actualMessage);
	}
}
