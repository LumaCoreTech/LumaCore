// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Features.Validation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

using Xunit;

namespace LumaCore.Api.Tests.Features.Validation;

// ValidationFilter request validation — isolated from versioned API group infrastructure.
//
// These tests exercise the ValidationFilter endpoint filter in isolation, using a plain
// route group with WithValidation() — no ApiVersionSet, no versioning middleware:
//
//   1. Valid requests: the filter passes through to the handler (PassesThrough,
//      NullArgumentSkipped).
//
//   2. Invalid requests: the filter rejects with 400 ProblemDetails — single-field
//      errors (ReturnsFieldLevelErrors) and multi-field validation
//      (ReturnsAllFieldErrors). Both tests verify the complete ProblemDetails
//      envelope (type, title, status, error count) via AssertValidationProblemDetailsAsync().
//
// For group-level wiring verification (WithValidation() on the versioned API group),
// see VersionedApiGroupIntegrationTests.

/// <summary>
/// Unit tests for <see cref="ValidationFilter.InvokeAsync"/> in isolation.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify the <see cref="ValidationFilter"/> endpoint filter logic using a minimal
///     <see cref="TestServer"/>-backed application with a plain route group — no API versioning, no
///     <c>MapVersionedApiGroup()</c>. This isolates the filter behavior from the versioned API group
///     infrastructure.
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <b>Valid requests</b> — the filter skips validation for <see langword="null"/> arguments and
///             passes valid requests through to the handler.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>Invalid requests</b> — the filter returns <c>400 Bad Request</c> with a
///             <see cref="ProblemDetails"/> body containing the <see cref="ErrorTypes.Validation"/> type URN
///             and field-level error details.
///             </description>
///         </item>
///     </list>
///     <para>
///     For group-level wiring verification (<c>WithValidation()</c> on the versioned API group),
///     see <c>VersionedApiGroupIntegrationTests</c>.
///     </para>
/// </remarks>
[Trait("Category", "Validation")]
public sealed partial class ValidationFilterTests
{
	// --- 1. Valid requests: filter passes through ---

	/// <summary>
	/// Verifies that the <see cref="ValidationFilter"/> passes a valid request through to the endpoint handler,
	/// which returns <c>200 OK</c> with the echoed request body.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_ValidRequest_PassesThroughToHandler()
	{
		// Arrange
		using TestHarness harness = await CreateTestHarnessAsync();

		// Act
		using HttpResponseMessage response =
			await harness.Client.PostAsJsonAsync("/probe", new { Name = "TestValue" });

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		Assert.True(doc.RootElement.TryGetProperty("name", out JsonElement nameElement));
		Assert.Equal("TestValue", nameElement.GetString());
	}

	/// <summary>
	/// Verifies that the <see cref="ValidationFilter"/> skips <see langword="null"/> arguments without error.
	/// The filter's <c>foreach</c> loop checks <c>if (argument is null) continue</c>, so optional parameters
	/// that resolve to <see langword="null"/> must not cause a validation failure.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_NullArgument_IsSkippedAndHandlerExecutes()
	{
		// Arrange
		using TestHarness harness = await CreateTestHarnessAsync();

		// Act — GET /probe/optional without a query parameter, so the [FromQuery] string?
		// resolves to null. The filter must skip the null argument without error.
		using HttpResponseMessage response = await harness.Client.GetAsync("/probe/optional");

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		Assert.True(doc.RootElement.TryGetProperty("received", out JsonElement receivedElement));
		Assert.False(receivedElement.GetBoolean());
	}

	// --- 2. Invalid requests: filter rejects with 400 ProblemDetails ---

	/// <summary>
	/// Verifies that the <see cref="ValidationFilter"/> returns a complete <see cref="ProblemDetails"/> response
	/// with the <see cref="ErrorTypes.Validation"/> type URN, <c>400</c> status, and a single field-level error
	/// entry for the <c>Name</c> property with the custom error message from the <c>[Required]</c> attribute.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_InvalidRequest_ReturnsFieldLevelErrors()
	{
		// Arrange
		using TestHarness harness = await CreateTestHarnessAsync();

		// Act
		using HttpResponseMessage response =
			await harness.Client.PostAsJsonAsync("/probe", new { Name = (string?)null });

		// Assert — full ProblemDetails envelope + exactly one error field.
		using JsonDocument doc = (await AssertValidationProblemDetailsAsync(response, expectedFieldCount: 1)).Document;
		JsonElement errors = doc.RootElement.GetProperty("errors");

		// Verify Name is the only field with validation errors — no unexpected additional fields.
		JsonProperty singleErrorField = Assert.Single(errors.EnumerateObject());
		Assert.Equal("Name", singleErrorField.Name);
		string? errorMessage = Assert.Single(singleErrorField.Value.EnumerateArray()).GetString();
		Assert.Equal("Name is required.", errorMessage);
	}

	/// <summary>
	/// Verifies that when multiple fields fail validation, the <see cref="ProblemDetails"/> response contains
	/// error entries for <b>all</b> invalid fields — not just the first one encountered — with the exact error
	/// messages from the <c>[Required]</c> attributes on both <c>Name</c> and <c>Email</c>.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_MultipleInvalidFields_ReturnsAllFieldErrors()
	{
		// Arrange
		using TestHarness harness = await CreateTestHarnessAsync();

		// Act — POST to the multi-field endpoint with both fields null
		using HttpResponseMessage response =
			await harness.Client.PostAsJsonAsync("/probe/multi", new { Name = (string?)null, Email = (string?)null });

		// Assert — full ProblemDetails envelope + exactly two error fields.
		using JsonDocument doc = (await AssertValidationProblemDetailsAsync(response, expectedFieldCount: 2)).Document;
		JsonElement errors = doc.RootElement.GetProperty("errors");

		// Both Name and Email must have exactly one validation error with the expected message.
		string? nameError = Assert.Single(errors.GetProperty("Name").EnumerateArray()).GetString();
		Assert.Equal("Name is required.", nameError);

		string? emailError = Assert.Single(errors.GetProperty("Email").EnumerateArray()).GetString();
		Assert.Equal("Email is required.", emailError);
	}
}
