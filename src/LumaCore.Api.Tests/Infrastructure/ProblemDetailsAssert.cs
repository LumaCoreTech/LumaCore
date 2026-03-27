// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace LumaCore.Api.Tests.Infrastructure;

/// <summary>
/// Shared assertion helper for RFC 7807 <see cref="ProblemDetails"/> responses in middleware integration tests.
/// </summary>
/// <remarks>
///     <para>
///     This helper validates the <b>structural envelope</b> of a <see cref="ProblemDetails"/> response — the
///     properties that every well-formed error response must have regardless of which feature produced it:
///     </para>
///     <list type="bullet">
///         <item>HTTP status code matches the expected value.</item>
///         <item>Content type is <c>application/problem+json</c>.</item>
///         <item>Response body deserializes to a non-<see langword="null"/> <see cref="ProblemDetails"/>.</item>
///         <item><see cref="ProblemDetails.Status"/> matches the HTTP status code.</item>
///     </list>
///     <para>
///     <b>Design boundary — why semantic properties are excluded:</b> This helper deliberately does <b>not</b>
///     assert on <see cref="ProblemDetails.Type"/>, <see cref="ProblemDetails.Title"/>,
///     <see cref="ProblemDetails.Detail"/>, or <see cref="ProblemDetails.Extensions"/>. Those properties carry
///     feature-specific meaning (e.g., LumaCore error type URNs for status code mapping, <c>traceId</c> extensions
///     for database readiness) and belong in the calling test where the expected values are known and the assertions
///     document the feature's contract — not generic HTTP plumbing.
///     </para>
///     <para>
///     This separation keeps the shared helper stable across features while each feature's tests express their own
///     semantic expectations directly. If a new feature needs additional envelope checks (e.g., a custom header),
///     those should be added to a feature-specific wrapper that delegates here first.
///     </para>
/// </remarks>
internal static class ProblemDetailsAssert
{
	/// <summary>
	/// Reads the response body as a <see cref="ProblemDetails"/> instance and asserts that the HTTP status code,
	/// content type, and <see cref="ProblemDetails.Status"/> match <paramref name="expectedStatusCode"/>.
	/// </summary>
	/// <param name="response">The HTTP response to parse.</param>
	/// <param name="expectedStatusCode">The expected HTTP status code.</param>
	/// <returns>
	/// The deserialized <see cref="ProblemDetails"/> for further semantic assertions by the caller (e.g.,
	/// <see cref="ProblemDetails.Type"/>, <see cref="ProblemDetails.Title"/>, <see cref="ProblemDetails.Detail"/>,
	/// <see cref="ProblemDetails.Extensions"/>).
	/// </returns>
	internal static async Task<ProblemDetails> ReadAndAssertAsync(
		HttpResponseMessage response,
		HttpStatusCode      expectedStatusCode)
	{
		Assert.Equal(expectedStatusCode, response.StatusCode);
		Assert.Equal(
			"application/problem+json",
			response.Content.Headers.ContentType?.MediaType);

		ProblemDetails? problem = await response.Content
			                          .ReadFromJsonAsync<ProblemDetails>()
			                          .ConfigureAwait(false);

		Assert.NotNull(problem);
		Assert.Equal((int)expectedStatusCode, problem.Status);

		return problem;
	}
}
