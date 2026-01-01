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
///     This class provides stable, machine-readable error type identifiers that follow the URN (Uniform Resource
///     Name) format defined in RFC 8141. These URNs are used in the <c>type</c> field of <see cref="ProblemDetails"/>
///     responses to uniquely identify error categories across the LumaCore API.
///     </para>
///     <para>
///         <b>Why URNs instead of URLs?</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <b>Stability</b> – URNs are persistent identifiers that don't depend on server availability or URL
///             structure changes.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>No HTTP expectation</b> – Unlike URLs, URNs don't imply that the identifier should be
///             dereferenceable via HTTP, avoiding confusion.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>Namespace clarity</b> – The <c>urn:lumacore:error:</c> prefix clearly identifies these as
///             LumaCore-specific error types.
///             </description>
///         </item>
///     </list>
///     <para>
///     Clients can use these URNs to programmatically identify error types and provide appropriate handling logic
///     without parsing human-readable error messages.
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
static class ErrorTypes
{
	/// <summary>
	/// Indicates that the request is malformed or contains invalid data.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is returned for general request format errors, such as malformed JSON, missing required
	///     headers, or invalid query parameters. For field-level validation errors, see <see cref="Validation"/>.
	///     </para>
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:bad-request</c>
	/// </value>
	public const string BadRequest = $"{Base}:bad-request";

	/// <summary>
	/// Indicates that the request conflicts with the current state of the resource.
	/// </summary>
	/// <remarks>
	///     <para>This error type is returned when:</para>
	///     <list type="bullet">
	///         <item>
	///             <description>Creating a resource that already exists</description>
	///         </item>
	///         <item>
	///             <description>Updating a resource with a stale version (optimistic concurrency)</description>
	///         </item>
	///         <item>
	///             <description>Attempting an operation that violates business invariants</description>
	///         </item>
	///     </list>
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:conflict</c>
	/// </value>
	public const string Conflict = $"{Base}:conflict";

	/// <summary>
	/// Indicates that the authenticated user lacks permission to access the resource.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is returned when the user is successfully authenticated but does not have the required
	///     roles, claims, or policy permissions to perform the requested operation.
	///     </para>
	///     <para>
	///     This represents an <em>authorization</em> failure (you are not allowed), as opposed to
	///     <see cref="Unauthorized"/> which represents an authentication failure (we don't know who you are).
	///     </para>
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:forbidden</c>
	/// </value>
	public const string Forbidden = $"{Base}:forbidden";

	/// <summary>
	/// Indicates that the requested resource is permanently gone and will not be available again.
	/// </summary>
	/// <remarks>
	/// This error type is returned when a resource has been intentionally and permanently removed.
	/// Unlike <see cref="NotFound"/>, this indicates the resource existed but was deleted.
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:gone</c>
	/// </value>
	public const string Gone = $"{Base}:gone";

	/// <summary>
	/// Indicates that the request headers are too large.
	/// </summary>
	/// <remarks>
	/// This error type is returned when the server refuses to process the request because one or more
	/// header fields (or all headers combined) exceed the configured size limit.
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:headers-too-large</c>
	/// </value>
	public const string HeadersTooLarge = $"{Base}:headers-too-large";

	/// <summary>
	/// Indicates an unexpected internal server error.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is used as a fallback when an unhandled exception occurs during request processing. The
	///     response will include a <c>traceId</c> that can be used for correlation with server-side logs.
	///     </para>
	///     <para>
	///     <b>Security note:</b> Internal error responses intentionally omit exception details to prevent information
	///     disclosure. Full exception information is available only in server logs.
	///     </para>
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:internal</c>
	/// </value>
	public const string Internal = $"{Base}:internal";

	/// <summary>
	/// Indicates that the HTTP method is not supported for the requested endpoint.
	/// </summary>
	/// <remarks>
	/// This error type is returned when a client uses an HTTP method (e.g., POST, DELETE) that the endpoint
	/// does not support. The response includes an <c>Allow</c> header listing the supported methods.
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:method-not-allowed</c>
	/// </value>
	public const string MethodNotAllowed = $"{Base}:method-not-allowed";

	/// <summary>
	/// Indicates that the server cannot produce a response matching the client's Accept header.
	/// </summary>
	/// <remarks>
	/// This error type is returned when content negotiation fails because the server cannot generate
	/// a response in any of the formats specified in the request's <c>Accept</c> header.
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:not-acceptable</c>
	/// </value>
	public const string NotAcceptable = $"{Base}:not-acceptable";

	/// <summary>
	/// Indicates that the requested resource could not be found.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This error type is returned when an entity lookup by identifier fails, such as requesting a user,
	///     document, or other resource that does not exist.
	///     </para>
	///     <para>
	///     Note: This is distinct from a <c>404 Not Found</c> for unknown routes. Route-level 404s use the standard
	///     RFC 9110 type reference.
	///     </para>
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:not-found</c>
	/// </value>
	public const string NotFound = $"{Base}:not-found";

	/// <summary>
	/// Indicates that the server does not support the functionality required to fulfill the request.
	/// </summary>
	/// <remarks>
	/// This error type is returned when the server does not recognize or support the request method
	/// or lacks the ability to fulfill the request (e.g., a feature is not yet implemented).
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:not-implemented</c>
	/// </value>
	public const string NotImplemented = $"{Base}:not-implemented";

	/// <summary>
	/// Indicates that the request payload exceeds the server's size limits.
	/// </summary>
	/// <remarks>
	/// This error type is returned when the request body is larger than the server is willing or able
	/// to process. The response may include a <c>Retry-After</c> header if the condition is temporary.
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:payload-too-large</c>
	/// </value>
	public const string PayloadTooLarge = $"{Base}:payload-too-large";

	/// <summary>
	/// Indicates that the server cannot process the request due to rate limiting.
	/// </summary>
	/// <remarks>
	/// This error type is returned when the client has exceeded the allowed number of requests within a time
	/// window. The response may include a <c>Retry-After</c> header indicating when the client can retry.
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:rate-limited</c>
	/// </value>
	public const string RateLimited = $"{Base}:rate-limited";

	/// <summary>
	/// Indicates that the server timed out waiting for the client request.
	/// </summary>
	/// <remarks>
	/// This error type is returned when the client did not produce a request within the time the server
	/// was prepared to wait (e.g., slow upload, idle connection).
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:request-timeout</c>
	/// </value>
	public const string RequestTimeout = $"{Base}:request-timeout";

	/// <summary>
	/// Indicates that the service is temporarily unavailable.
	/// </summary>
	/// <remarks>
	/// This error type is returned when the server is temporarily unable to handle the request due to
	/// maintenance, overload, or dependency failures. The response may include a <c>Retry-After</c> header.
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:service-unavailable</c>
	/// </value>
	public const string ServiceUnavailable = $"{Base}:service-unavailable";

	/// <summary>
	/// Indicates that the request lacks valid authentication credentials.
	/// </summary>
	/// <remarks>
	///     <para>This error type is returned when:</para>
	///     <list type="bullet">
	///         <item>
	///             <description>No authentication token is provided</description>
	///         </item>
	///         <item>
	///             <description>The provided token is expired</description>
	///         </item>
	///         <item>
	///             <description>The provided token is malformed or invalid</description>
	///         </item>
	///     </list>
	///     <para>
	///     Despite the HTTP status being <c>401 Unauthorized</c>, this error represents an <em>authentication</em>
	///     failure (who are you?), not an authorization failure (are you allowed?).
	///     </para>
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:unauthorized</c>
	/// </value>
	public const string Unauthorized = $"{Base}:unauthorized";

	/// <summary>
	/// Indicates that the request content type is not supported by the endpoint.
	/// </summary>
	/// <remarks>
	/// This error type is returned when the <c>Content-Type</c> header specifies a media type that the
	/// endpoint cannot process (e.g., sending XML when only JSON is accepted).
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:unsupported-media-type</c>
	/// </value>
	public const string UnsupportedMediaType = $"{Base}:unsupported-media-type";

	/// <summary>
	/// Indicates that the client must switch to a different protocol.
	/// </summary>
	/// <remarks>
	/// This error type is returned when the server requires the client to upgrade to a different
	/// protocol (e.g., switching from HTTP to WebSocket).
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:upgrade-required</c>
	/// </value>
	public const string UpgradeRequired = $"{Base}:upgrade-required";

	/// <summary>
	/// Indicates a validation error where the request body or parameters failed data annotation or business rule
	/// validation.
	/// </summary>
	/// <remarks>
	///     <para>This error type is returned when:</para>
	///     <list type="bullet">
	///         <item>
	///             <description>Required fields are missing</description>
	///         </item>
	///         <item>
	///             <description>Field values exceed length constraints</description>
	///         </item>
	///         <item>
	///             <description>Field values are outside allowed ranges</description>
	///         </item>
	///         <item>
	///             <description>Custom validation attributes fail</description>
	///         </item>
	///     </list>
	///     <para>
	///     The <see cref="ProblemDetails"/> response will include an <c>errors</c> dictionary mapping field names to
	///     their validation error messages.
	///     </para>
	/// </remarks>
	/// <value>
	///     <c>urn:lumacore:error:validation</c>
	/// </value>
	public const string Validation = $"{Base}:validation";

	/// <summary>
	/// The base URN prefix for all LumaCore error types.
	/// </summary>
	/// <remarks>
	/// All error type URNs are constructed by appending a specific error identifier
	/// to this base prefix.
	/// </remarks>
	private const string Base = "urn:lumacore:error";
}
