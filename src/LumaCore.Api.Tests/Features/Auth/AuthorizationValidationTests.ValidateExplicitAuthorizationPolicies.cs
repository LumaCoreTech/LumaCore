// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

// ValidateExplicitAuthorizationPolicies(): startup configuration guard.
//
// This is NOT a runtime authorization mechanism — it validates that developers have made
// an explicit security decision for every versioned API endpoint. The validator applies
// two criteria:
//
//   Scope:      Route prefix — only endpoints under /api/v* are inspected.
//   Compliance: Endpoint metadata — each in-scope endpoint must carry RequireAuthorization()
//               or AllowAnonymous(). Missing metadata = startup failure.
//
//   1. Valid configurations: all versioned endpoints have explicit auth declarations,
//      non-versioned endpoints are outside scope, empty graphs pass, case-insensitive
//      prefix matching.
//   2. Invalid configurations: missing declarations are detected and reported with
//      actionable error messages listing the offending endpoints.
public sealed partial class AuthorizationValidationTests
{
	// --- 1. Valid configurations ---

	/// <summary>
	/// Verifies that validation passes and returns the same <see cref="WebApplication"/> instance when all
	/// versioned API endpoints have explicit authorization declarations — either
	/// <c>RequireAuthorization()</c> or <c>AllowAnonymous()</c>.
	/// </summary>
	[Fact]
	public void ValidateExplicitAuthorizationPolicies_AllEndpointsAuthorized_ReturnsApp()
	{
		// Arrange
		WebApplication app = CreateApp(a =>
		{
			a.MapGet("/api/v1/protected", () => Results.Ok()).RequireAuthorization();
			a.MapPost("/api/v1/public", () => Results.Ok()).AllowAnonymous();
		});

		// Act
		WebApplication result = app.ValidateExplicitAuthorizationPolicies();

		// Assert — returns the same instance for method chaining.
		Assert.Same(app, result);
	}

	/// <summary>
	/// Verifies that endpoints whose route does not start with <c>/api/v</c> are skipped entirely —
	/// the validator only inspects versioned API endpoints.
	/// </summary>
	[Fact]
	public void ValidateExplicitAuthorizationPolicies_NonVersionedEndpoints_AreIgnored()
	{
		// Arrange — routes outside the /api/v* prefix are not subject to authorization validation.
		WebApplication app = CreateApp(a =>
		{
			a.MapGet("/health", () => Results.Ok());
			a.MapGet("/swagger", () => Results.Ok());
		});

		// Act
		WebApplication result = app.ValidateExplicitAuthorizationPolicies();

		// Assert
		Assert.Same(app, result);
	}

	/// <summary>
	/// Verifies that an application with no registered endpoints passes validation without error.
	/// </summary>
	[Fact]
	public void ValidateExplicitAuthorizationPolicies_NoEndpointsRegistered_ReturnsApp()
	{
		// Arrange
		WebApplication app = CreateApp();

		// Act
		WebApplication result = app.ValidateExplicitAuthorizationPolicies();

		// Assert
		Assert.Same(app, result);
	}

	/// <summary>
	/// Verifies that the <c>/api/v</c> prefix check is case-insensitive — an endpoint registered with
	/// an uppercase path like <c>/API/V1/data</c> is still recognized as a versioned endpoint and subject
	/// to authorization validation.
	/// </summary>
	[Fact]
	public void ValidateExplicitAuthorizationPolicies_CaseInsensitivePrefix_DetectsMissingAuth()
	{
		// Arrange — uppercase route prefix to verify OrdinalIgnoreCase matching.
		WebApplication app = CreateApp(a =>
		{
			a.MapGet("/API/V1/data", () => Results.Ok());
		});

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => app.ValidateExplicitAuthorizationPolicies());

		AssertValidationMessage(ex.Message, "/API/V1/data");
	}

	// --- 2. Invalid configurations ---

	/// <summary>
	/// Verifies that a single versioned endpoint missing an explicit authorization declaration causes an
	/// <see cref="InvalidOperationException"/> whose message includes the offending endpoint's display name
	/// and route pattern.
	/// </summary>
	[Fact]
	public void ValidateExplicitAuthorizationPolicies_SingleEndpointMissingAuth_ThrowsWithEndpointInMessage()
	{
		// Arrange
		WebApplication app = CreateApp(a =>
		{
			a.MapGet("/api/v1/unprotected", () => Results.Ok());
		});

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => app.ValidateExplicitAuthorizationPolicies());

		AssertValidationMessage(ex.Message, "/api/v1/unprotected");
	}

	/// <summary>
	/// Verifies that when multiple versioned endpoints are missing authorization declarations, all of them
	/// are reported in the exception message.
	/// </summary>
	[Fact]
	public void ValidateExplicitAuthorizationPolicies_MultipleEndpointsMissingAuth_ThrowsWithAllInMessage()
	{
		// Arrange
		WebApplication app = CreateApp(a =>
		{
			a.MapGet("/api/v1/first", () => Results.Ok());
			a.MapPost("/api/v1/second", () => Results.Ok());
			a.MapDelete("/api/v2/third", () => Results.Ok());
		});

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => app.ValidateExplicitAuthorizationPolicies());

		AssertValidationMessage(ex.Message, "/api/v1/first", "/api/v1/second", "/api/v2/third");
	}

	/// <summary>
	/// Verifies that only the endpoints missing authorization declarations are reported — properly
	/// authorized endpoints must not appear in the error message.
	/// </summary>
	[Fact]
	public void ValidateExplicitAuthorizationPolicies_MixOfAuthorizedAndMissing_ThrowsOnlyForMissing()
	{
		// Arrange
		WebApplication app = CreateApp(a =>
		{
			a.MapGet("/api/v1/secured", () => Results.Ok()).RequireAuthorization();
			a.MapPost("/api/v1/open", () => Results.Ok()).AllowAnonymous();
			a.MapGet("/api/v1/forgotten", () => Results.Ok());
			a.MapGet("/health", () => Results.Ok());
		});

		// Act + Assert — the anchored full-message regex validates that ONLY /api/v1/forgotten
		// appears in the bullet list, implicitly proving that authorized and infrastructure endpoints are absent.
		var ex = Assert.Throws<InvalidOperationException>(() => app.ValidateExplicitAuthorizationPolicies());

		AssertValidationMessage(ex.Message, "/api/v1/forgotten");
	}
}
