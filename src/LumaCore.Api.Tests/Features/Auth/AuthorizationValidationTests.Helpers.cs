// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text;
using System.Text.RegularExpressions;

using LumaCore.Api.Features.Auth;

using Microsoft.AspNetCore.Builder;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

public sealed partial class AuthorizationValidationTests
{
	/// <summary>
	/// Creates a minimal <see cref="WebApplication"/> and maps endpoints using the provided configuration
	/// delegate. Uses <see cref="WebApplication.CreateBuilder()"/> to provide the full ASP.NET Core routing
	/// infrastructure — matching production startup conditions.
	/// </summary>
	/// <param name="configureEndpoints">
	/// A delegate that maps endpoints on the application. Pass <see langword="null"/> or an empty delegate to
	/// create an application with no endpoints.
	/// </param>
	/// <returns>A built <see cref="WebApplication"/> with the configured endpoints.</returns>
	private static WebApplication CreateApp(Action<WebApplication>? configureEndpoints = null)
	{
		WebApplication app = WebApplication.CreateBuilder().Build();
		configureEndpoints?.Invoke(app);
		return app;
	}

	/// <summary>
	/// Asserts that the exception message matches the exact structure produced by
	/// <see cref="AuthorizationValidation.ValidateExplicitAuthorizationPolicies"/>. The full message — header,
	/// bullet points, and instructional footer — is validated as a single anchored regex. Only the
	/// framework-generated <c>DisplayName</c> portion of each bullet is treated as dynamic (<c>.+</c>);
	/// route patterns are matched literally via <see cref="Regex.Escape"/>.
	/// </summary>
	/// <param name="actualMessage">The <see cref="Exception.Message"/> to validate.</param>
	/// <param name="expectedRoutePatterns">
	/// The expected route patterns in registration order. Each pattern corresponds to one bullet-point entry
	/// in the error message.
	/// </param>
	private static void AssertValidationMessage(string actualMessage, params string[] expectedRoutePatterns)
	{
		const string nl = @"\r?\n";

		var pattern = new StringBuilder();
		pattern.Append('^');

		// Header.
		pattern.Append(@"Authorization validation failed\. The following endpoints are missing ");
		pattern.Append(@"explicit authorization declarations:");
		pattern.Append(nl);
		pattern.Append(nl);

		// Bullet points — DisplayName is framework-generated, matched with .+ to tolerate any value.
		foreach (string routePattern in expectedRoutePatterns)
		{
			pattern.Append(@"  • .+ \(");
			pattern.Append(Regex.Escape(routePattern));
			pattern.Append(@"\)");
			pattern.Append(nl);
		}

		// Instructional footer.
		pattern.Append(nl);
		pattern.Append(@"Every versioned API endpoint must explicitly declare its authorization requirement:");
		pattern.Append(nl);
		pattern.Append(nl);
		pattern.Append(@"  // For protected endpoints:");
		pattern.Append(nl);
		pattern.Append(@"  group\.MapGet\(""/profile"", HandleGetProfile\)");
		pattern.Append(nl);
		pattern.Append(@"      \.RequireAuthorization\(\);");
		pattern.Append(nl);
		pattern.Append(nl);
		pattern.Append(@"  // For public endpoints:");
		pattern.Append(nl);
		pattern.Append(@"  group\.MapPost\(""/login"", HandleLogin\)");
		pattern.Append(nl);
		pattern.Append(@"      \.AllowAnonymous\(\);");
		pattern.Append(nl);
		pattern.Append(nl);
		pattern.Append(@"This prevents accidental exposure of unprotected endpoints\.");
		pattern.Append(nl);
		pattern.Append('$');

		Assert.Matches(pattern.ToString(), actualMessage);
	}
}
