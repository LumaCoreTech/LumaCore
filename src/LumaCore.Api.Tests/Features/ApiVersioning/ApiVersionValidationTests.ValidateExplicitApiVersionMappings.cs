// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Asp.Versioning;
using Asp.Versioning.Builder;

using LumaCore.Api.Features.ApiVersioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Xunit;

namespace LumaCore.Api.Tests.Features.ApiVersioning;

// ValidateExplicitApiVersionMappings(): startup guard for endpoint version mappings.
//
// These tests create minimal WebApplication instances with specific endpoint configurations
// to verify that the validation correctly enforces explicit MapToApiVersion() declarations:
//
//   1. Valid configurations: all versioned endpoints have explicit MapToApiVersion(), non-versioned
//      endpoints are ignored, empty endpoint graphs pass without error.
//   2. Invalid configurations: missing version mappings and missing ApiVersionMetadata are detected
//      and reported with actionable error messages listing the offending endpoints.
public sealed partial class ApiVersionValidationTests
{
	// --- 1. Valid configurations ---

	/// <summary>
	/// Verifies that validation passes and returns the same <see cref="WebApplication"/> instance when all
	/// versioned API endpoints have explicit <c>MapToApiVersion()</c> calls.
	/// </summary>
	[Fact]
	public void ValidateExplicitApiVersionMappings_AllEndpointsMapped_ReturnsApp()
	{
		// Arrange
		WebApplication app = CreateApp((a, vs) =>
		{
			a.MapGet("/api/v{version:apiVersion}/items", () => Results.Ok())
				.WithApiVersionSet(vs)
				.MapToApiVersion(ApiVersions.V1);

			a.MapPost("/api/v{version:apiVersion}/items", () => Results.Ok())
				.WithApiVersionSet(vs)
				.MapToApiVersion(ApiVersions.V1);
		});

		// Act
		WebApplication result = app.ValidateExplicitApiVersionMappings();

		// Assert — returns the same instance for method chaining.
		Assert.Same(app, result);
	}

	/// <summary>
	/// Verifies that non-versioned infrastructure endpoints (e.g., <c>/health</c>) are not subject to
	/// version mapping validation, even when they lack <c>MapToApiVersion()</c> declarations.
	/// Returns the same <see cref="WebApplication"/> instance for method chaining.
	/// </summary>
	[Fact]
	public void ValidateExplicitApiVersionMappings_NonVersionedEndpointsWithoutMapping_AreIgnored()
	{
		// Arrange — infrastructure endpoints outside /api/v* have no version mapping requirement.
		WebApplication app = CreateApp((a, _) =>
		{
			a.MapGet("/health", () => Results.Ok());
			a.MapGet("/swagger", () => Results.Ok());
		});

		// Act
		WebApplication result = app.ValidateExplicitApiVersionMappings();

		// Assert — returns the same instance for method chaining.
		Assert.Same(app, result);
	}

	/// <summary>
	/// Verifies that an application with no registered endpoints passes validation without error.
	/// </summary>
	[Fact]
	public void ValidateExplicitApiVersionMappings_NoEndpointsRegistered_ReturnsApp()
	{
		// Arrange
		WebApplication app = CreateApp();

		// Act
		WebApplication result = app.ValidateExplicitApiVersionMappings();

		// Assert
		Assert.Same(app, result);
	}

	// --- 2. Invalid configurations ---

	/// <summary>
	/// Verifies that a versioned endpoint registered without any API versioning infrastructure (no
	/// <see cref="ApiVersionSet"/>, no <c>MapToApiVersion()</c>) causes an
	/// <see cref="InvalidOperationException"/> whose message includes the <c>"No ApiVersionMetadata"</c> suffix.
	/// This covers the <c>metadata is null</c> code path.
	/// </summary>
	[Fact]
	public void ValidateExplicitApiVersionMappings_EndpointWithoutVersionMetadata_ThrowsWithNoMetadataMessage()
	{
		// Arrange — plain MapGet under /api/v1 without any versioning setup → no ApiVersionMetadata.
		WebApplication app = CreateApp((a, _) =>
		{
			a.MapGet("/api/v1/unversioned", () => Results.Ok());
		});

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => app.ValidateExplicitApiVersionMappings());

		AssertValidationMessage(ex.Message, ("/api/v1/unversioned", "No ApiVersionMetadata"));
	}

	/// <summary>
	/// Verifies that a versioned endpoint with an <see cref="ApiVersionSet"/> but without an explicit
	/// <c>MapToApiVersion()</c> call causes an <see cref="InvalidOperationException"/>. This covers the
	/// <c>DeclaredApiVersions.Count == 0</c> code path where <see cref="ApiVersionMetadata"/> exists but
	/// contains no explicit version mappings.
	/// </summary>
	[Fact]
	public void ValidateExplicitApiVersionMappings_EndpointWithVersionSetButNoMapping_ThrowsWithEndpointInMessage()
	{
		// Arrange — endpoint has WithApiVersionSet() but no MapToApiVersion() → metadata exists,
		// but explicit version list is empty.
		WebApplication app = CreateApp((a, vs) =>
		{
			a.MapGet("/api/v{version:apiVersion}/unmapped", () => Results.Ok())
				.WithApiVersionSet(vs);
		});

		// Act + Assert — metadata exists but contains no explicit version mappings, so no
		// "No ApiVersionMetadata" suffix appears. The anchored full-message regex proves this.
		var ex = Assert.Throws<InvalidOperationException>(() => app.ValidateExplicitApiVersionMappings());

		AssertValidationMessage(ex.Message, ("/api/v{version:apiVersion}/unmapped", null));
	}

	/// <summary>
	/// Verifies that when multiple versioned endpoints are missing version mappings, all of them
	/// are reported in the exception message.
	/// </summary>
	[Fact]
	public void ValidateExplicitApiVersionMappings_MultipleEndpointsMissingMapping_ThrowsWithAllInMessage()
	{
		// Arrange
		WebApplication app = CreateApp((a, vs) =>
		{
			a.MapGet("/api/v{version:apiVersion}/first", () => Results.Ok())
				.WithApiVersionSet(vs);
			a.MapPost("/api/v{version:apiVersion}/second", () => Results.Ok())
				.WithApiVersionSet(vs);
			a.MapDelete("/api/v{version:apiVersion}/third", () => Results.Ok())
				.WithApiVersionSet(vs);
		});

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => app.ValidateExplicitApiVersionMappings());

		AssertValidationMessage(
			ex.Message,
			("/api/v{version:apiVersion}/first", null),
			("/api/v{version:apiVersion}/second", null),
			("/api/v{version:apiVersion}/third", null));
	}

	/// <summary>
	/// Verifies that only the endpoints missing version mappings are reported — properly mapped endpoints
	/// and non-versioned infrastructure endpoints must not appear in the error message.
	/// </summary>
	[Fact]
	public void ValidateExplicitApiVersionMappings_MixOfMappedAndMissing_ThrowsOnlyForMissing()
	{
		// Arrange
		WebApplication app = CreateApp((a, vs) =>
		{
			a.MapGet("/api/v{version:apiVersion}/mapped", () => Results.Ok())
				.WithApiVersionSet(vs)
				.MapToApiVersion(ApiVersions.V1);

			a.MapGet("/api/v{version:apiVersion}/forgotten", () => Results.Ok())
				.WithApiVersionSet(vs);

			a.MapGet("/health", () => Results.Ok());
		});

		// Act + Assert — the anchored full-message regex validates that ONLY the missing endpoint
		// appears in the bullet list, implicitly proving that mapped and infrastructure endpoints are absent.
		var ex = Assert.Throws<InvalidOperationException>(() => app.ValidateExplicitApiVersionMappings());

		AssertValidationMessage(ex.Message, ("/api/v{version:apiVersion}/forgotten", null));
	}
}
