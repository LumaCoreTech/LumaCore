// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Features.Validation;

using Microsoft.AspNetCore.TestHost;

using Xunit;

namespace LumaCore.Api.Tests.Features.ApiVersioning;

// HTTP-level integration tests for the versioned API group.
//
// These tests exercise the runtime behavior of MapVersionedApiGroup() that is not
// observable through metadata inspection alone:
//
//   1. ReportApiVersions: the api-supported-versions response header is present
//      on success (SuccessResponse) and on validation failures (ValidationFailure).
//
//   2. ValidationFilter wiring: the WithValidation() filter is active on the group,
//      rejecting invalid requests (InvalidRequestIsRejected). Detailed filter behavior
//      (ProblemDetails structure, field-level errors) is tested in ValidationFilterTests.
//
//   3. Version routing: unsupported versions are rejected by the route
//      constraint (UnsupportedVersion).
//
// For metadata-level tests (route prefix, ApiVersionSet), see VersionedApiGroupTests.
// For isolated ValidationFilter tests, see ValidationFilterTests.

/// <summary>
/// HTTP-level integration tests for <see cref="VersionedApiGroup.MapVersionedApiGroup"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify runtime behavior that is <b>not</b> observable through endpoint metadata inspection:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <b>
///                 <c>ReportApiVersions()</c>
///             </b>
///             — the <c>api-supported-versions</c> response header is present and
///             contains the expected version.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>
///             <c>WithValidation()</c> wiring
///             </b>
///             — the <see cref="ValidationFilter"/> is active on the versioned group
///             and rejects invalid requests with <c>400 Bad Request</c>. Detailed filter behavior is tested
///             in <c>ValidationFilterTests</c>.
///             </description>
///         </item>
///     </list>
///     <para>
///     All tests use a minimal <see cref="TestServer"/>-backed application with probe endpoints —
///     no database, authentication, or logging infrastructure is required.
///     </para>
///     <para>
///     For metadata-level tests (route prefix, <c>ApiVersionSet</c>), see <see cref="VersionedApiGroupTests"/>.
///     For isolated <see cref="ValidationFilter"/> behavior, see <c>ValidationFilterTests</c>.
///     </para>
/// </remarks>
[Trait("Category", "ApiVersioning")]
public sealed partial class VersionedApiGroupIntegrationTests
{
	// --- 1. ReportApiVersions: response header presence ---

	/// <summary>
	/// Verifies that a successful response from a versioned endpoint includes the
	/// <c>api-supported-versions</c> header with exactly the value <c>1</c>.
	/// </summary>
	[Fact]
	public async Task SuccessResponse_IncludesApiSupportedVersionsHeader()
	{
		// Arrange
		using TestHarness harness = await CreateTestHarnessAsync();

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync("/api/v1/probe");

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.True(
			response.Headers.TryGetValues(ApiSupportedVersionsHeader, out IEnumerable<string>? values),
			$"Expected '{ApiSupportedVersionsHeader}' header to be present.");
		string version = Assert.Single(values);
		Assert.Equal("1", version);
	}

	/// <summary>
	/// Verifies that the <c>api-supported-versions</c> header is present even when the
	/// <see cref="ValidationFilter"/> rejects the request with <c>400 Bad Request</c>.
	/// The versioning middleware adds the header before the endpoint filter runs.
	/// </summary>
	[Fact]
	public async Task ValidationFailure_StillIncludesApiSupportedVersionsHeader()
	{
		// Arrange
		using TestHarness harness = await CreateTestHarnessAsync();

		// Act — POST with null Name triggers [Required] validation
		using HttpResponseMessage response =
			await harness.Client.PostAsJsonAsync("/api/v1/probe", new { Name = (string?)null });

		// Assert — the versioning header must be present even on validation failures,
		// and the type URN confirms it was the ValidationFilter that produced the 400.
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		await AssertValidationTypeUrnAsync(response);
		Assert.True(
			response.Headers.TryGetValues(ApiSupportedVersionsHeader, out IEnumerable<string>? values),
			$"Expected '{ApiSupportedVersionsHeader}' header to be present even on validation failure.");
		string version = Assert.Single(values);
		Assert.Equal("1", version);
	}

	// --- 2. ValidationFilter wiring: group-level validation is active ---

	/// <summary>
	/// Smoke test verifying that the <see cref="ValidationFilter"/> is wired into the versioned API group via
	/// <c>WithValidation()</c>. An invalid request must be rejected with <c>400 Bad Request</c> and the
	/// <see cref="ErrorTypes.Validation"/> type URN, confirming the rejection comes from
	/// the <see cref="ValidationFilter"/> rather than from other middleware.
	/// Detailed filter behavior (ProblemDetails structure, field-level errors, multi-field validation)
	/// is covered by <c>ValidationFilterTests</c>.
	/// </summary>
	[Fact]
	public async Task InvalidRequest_IsRejectedByGroupValidation()
	{
		// Arrange
		using TestHarness harness = await CreateTestHarnessAsync();

		// Act — POST with null Name to trigger [Required] validation
		using HttpResponseMessage response =
			await harness.Client.PostAsJsonAsync("/api/v1/probe", new { Name = (string?)null });

		// Assert — status code + type URN confirms it is the ValidationFilter rejecting the request.
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		await AssertValidationTypeUrnAsync(response);
	}

	// --- 3. Version routing: unsupported version ---

	/// <summary>
	/// Verifies that a request targeting an unsupported API version (e.g., <c>v999</c>) is rejected because
	/// the <c>{version:apiVersion}</c> route constraint only matches configured versions. The route itself
	/// does not match, resulting in <c>404 Not Found</c> without the <c>api-supported-versions</c> header
	/// (confirming the versioning middleware was never involved).
	/// </summary>
	[Fact]
	public async Task UnsupportedVersion_ReturnsNotFound()
	{
		// Arrange
		using TestHarness harness = await CreateTestHarnessAsync();

		// Act — v999 is not registered in the ApiVersionSet, so the apiVersion
		// route constraint rejects it and the route does not match at all.
		using HttpResponseMessage response = await harness.Client.GetAsync("/api/v999/probe");

		// Assert — 404 without versioning header proves the route constraint rejected
		// the request before the versioning middleware could run.
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		Assert.False(
			response.Headers.Contains(ApiSupportedVersionsHeader),
			$"'{ApiSupportedVersionsHeader}' header must be absent when the route does not match.");
	}
}
