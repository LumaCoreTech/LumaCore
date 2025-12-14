// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.AspNetCore.Mvc;

namespace LumaCore.Api.Features.ErrorHandling;

/// <summary>
/// Defines URN-based error type identifiers for use in RFC 7807 <see cref="ProblemDetails"/> responses.
/// </summary>
/// <remarks>
///     <para>
///     This class provides stable, machine-readable error type identifiers that follow
///     the URN (Uniform Resource Name) format defined in RFC 8141. These URNs are used
///     in the <c>type</c> field of <see cref="ProblemDetails"/> responses to uniquely identify error
///     categories across the LumaCore API.
///     </para>
///     <para>
///     <b>Why URNs instead of URLs?</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <b>Stability</b> – URNs are persistent identifiers that don't depend on
///             server availability or URL structure changes.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>No HTTP expectation</b> – Unlike URLs, URNs don't imply that the
///             identifier should be dereferenceable via HTTP, avoiding confusion.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>Namespace clarity</b> – The <c>urn:lumacore:error:</c> prefix clearly
///             identifies these as LumaCore-specific error types.
///             </description>
///         </item>
///     </list>
///     <para>
///     Clients can use these URNs to programmatically identify error types and provide
///     appropriate handling logic without parsing human-readable error messages.
///     </para>
/// </remarks>
/// <example>
/// Using error types in a custom exception handler:
/// <code>
/// var problemDetails = new ProblemDetails
/// {
///     Type = ErrorTypes.Validation,
///     Title = "Validation Failed",
///     Status = StatusCodes.Status400BadRequest
/// };
/// </code>
/// </example>
public static class ErrorTypes
{
	/// <summary>
	/// The base URN prefix for all LumaCore error types.
	/// </summary>
	/// <remarks>
	/// All error type URNs are constructed by appending a specific error identifier
	/// to this base prefix.
	/// </remarks>
	private const string Base = "urn:lumacore:error";

	/// <summary>
	/// Indicates a validation error where the request body or parameters failed
	/// data annotation or business rule validation.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is returned when:
	///     </para>
	///     <list type="bullet">
	///         <item><description>Required fields are missing</description></item>
	///         <item><description>Field values exceed length constraints</description></item>
	///         <item><description>Field values are outside allowed ranges</description></item>
	///         <item><description>Custom validation attributes fail</description></item>
	///     </list>
	///     <para>
	///     The <see cref="ProblemDetails"/> response will include an <c>errors</c> dictionary mapping
	///     field names to their validation error messages.
	///     </para>
	/// </remarks>
	/// <value><c>urn:lumacore:error:validation</c></value>
	public const string Validation = $"{Base}:validation";

	/// <summary>
	/// Indicates that the requested resource could not be found.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is returned when an entity lookup by identifier fails,
	///     such as requesting a user, document, or other resource that does not exist.
	///     </para>
	///     <para>
	///     Note: This is distinct from a <c>404 Not Found</c> for unknown routes.
	///     Route-level 404s use the standard RFC 9110 type reference.
	///     </para>
	/// </remarks>
	/// <value><c>urn:lumacore:error:not-found</c></value>
	public const string NotFound = $"{Base}:not-found";

	/// <summary>
	/// Indicates that the request lacks valid authentication credentials.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is returned when:
	///     </para>
	///     <list type="bullet">
	///         <item><description>No authentication token is provided</description></item>
	///         <item><description>The provided token is expired</description></item>
	///         <item><description>The provided token is malformed or invalid</description></item>
	///     </list>
	///     <para>
	///     Despite the HTTP status being <c>401 Unauthorized</c>, this error represents
	///     an <em>authentication</em> failure (who are you?), not an authorization
	///     failure (are you allowed?).
	///     </para>
	/// </remarks>
	/// <value><c>urn:lumacore:error:unauthorized</c></value>
	public const string Unauthorized = $"{Base}:unauthorized";

	/// <summary>
	/// Indicates that the authenticated user lacks permission to access the resource.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is returned when the user is successfully authenticated
	///     but does not have the required roles, claims, or policy permissions to
	///     perform the requested operation.
	///     </para>
	///     <para>
	///     This represents an <em>authorization</em> failure (you are not allowed),
	///     as opposed to <see cref="Unauthorized"/> which represents an authentication
	///     failure (we don't know who you are).
	///     </para>
	/// </remarks>
	/// <value><c>urn:lumacore:error:forbidden</c></value>
	public const string Forbidden = $"{Base}:forbidden";

	/// <summary>
	/// Indicates an unexpected internal server error.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is used as a fallback when an unhandled exception occurs
	///     during request processing. The response will include a <c>traceId</c> that
	///     can be used for correlation with server-side logs.
	///     </para>
	///     <para>
	///     <b>Security note:</b> Internal error responses intentionally omit exception
	///     details to prevent information disclosure. Full exception information is
	///     available only in server logs.
	///     </para>
	/// </remarks>
	/// <value><c>urn:lumacore:error:internal</c></value>
	public const string Internal = $"{Base}:internal";

	/// <summary>
	/// Indicates that the request conflicts with the current state of the resource.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is returned when:
	///     </para>
	///     <list type="bullet">
	///         <item><description>Creating a resource that already exists</description></item>
	///         <item><description>Updating a resource with a stale version (optimistic concurrency)</description></item>
	///         <item><description>Attempting an operation that violates business invariants</description></item>
	///     </list>
	/// </remarks>
	/// <value><c>urn:lumacore:error:conflict</c></value>
	public const string Conflict = $"{Base}:conflict";

	/// <summary>
	/// Indicates that the server cannot process the request due to rate limiting.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is returned when the client has exceeded the allowed number
	///     of requests within a time window. The response may include a <c>Retry-After</c>
	///     header indicating when the client can retry.
	///     </para>
	/// </remarks>
	/// <value><c>urn:lumacore:error:rate-limited</c></value>
	public const string RateLimited = $"{Base}:rate-limited";
}
