// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Api.Features.Cors;

/// <summary>
/// Configuration options for Cross-Origin Resource Sharing (CORS) policies.
/// </summary>
/// <remarks>
/// CORS allows web applications running on one origin (domain/port) to access resources from a different origin.
/// For security, browsers restrict cross-origin requests by default.
/// <strong>Important:</strong> CORS policies must be carefully configured to prevent unauthorized access while
/// enabling legitimate cross-origin requests.
/// </remarks>
sealed class CorsOptions : IValidatableObject
{
	/// <summary>
	/// The configuration section name used to bind these options.
	/// </summary>
	public const string SectionName = "Cors";

	private const string PreflightMaxAgeRangeError =
		"PreflightMaxAge must be greater than or equal to 0 when specified.";

	/// <summary>
	/// Gets or sets a value indicating whether credentials (cookies, authorization headers) should be allowed in
	/// cross-origin requests.
	/// </summary>
	/// <remarks>
	///     <para>
	///     When <see langword="true"/>, the server will include <c>Access-Control-Allow-Credentials: true</c> in
	///     responses, allowing browsers to include cookies and authorization headers in cross-origin requests.
	///     </para>
	///     <para>
	///     <strong>Security:</strong> Cannot be used with <c>AllowedOrigins = ["*"]</c>. You must specify exact
	///     origins when credentials are allowed.
	///     </para>
	/// </remarks>
	public bool AllowCredentials { get; set; }

	/// <summary>
	/// Gets or sets the list of allowed HTTP headers in cross-origin requests.
	/// </summary>
	/// <value>
	/// A list of header names (e.g. <c>["Content-Type", "Authorization"]</c>). If empty, all headers are allowed.
	/// </value>
	public List<string> AllowedHeaders { get; set; } = [];

	/// <summary>
	/// Gets or sets the list of allowed HTTP methods for cross-origin requests.
	/// </summary>
	/// <value>
	/// A list of HTTP methods (e.g. <c>["GET", "POST", "PUT", "DELETE"]</c>). If empty, all methods are allowed.
	/// </value>
	public List<string> AllowedMethods { get; set; } = [];

	/// <summary>
	/// Gets or sets the list of allowed origins for cross-origin requests.
	/// </summary>
	/// <remarks>
	///     <para>Specifies which origins (scheme + domain + port) are allowed to make cross-origin requests.</para>
	///     <para>
	///         <strong>Examples:</strong>
	///     </para>
	///     <list type="bullet">
	///         <item><c>"https://example.com"</c> - Specific origin</item>
	///         <item><c>"http://localhost:3000"</c> - Development frontend</item>
	///         <item><c>"*"</c> - Allow all origins (use with extreme caution, not recommended for production)</item>
	///     </list>
	///     <para><strong>Security:</strong> Avoid using <c>"*"</c> in production. Specify exact origins instead.</para>
	/// </remarks>
	public List<string> AllowedOrigins { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether CORS should be enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable CORS with the configured policy;
	/// <see langword="false"/> to block cross-origin requests (default).
	/// </value>
	/// <remarks>
	/// When <see langword="false"/>, no CORS policy is applied and cross-origin requests will be blocked by
	/// browsers (secure-by-default behavior).
	/// </remarks>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the list of response headers that should be exposed to the client in cross-origin responses.
	/// </summary>
	/// <remarks>
	///     <para>
	///     By default, only simple response headers (Cache-Control, Content-Language, Content-Type, etc.) are exposed
	///     to the client. To expose custom headers, list them here.
	///     </para>
	///     <para>
	///         <strong>Example:</strong> <c>["X-Custom-Header", "X-Request-Id"]</c>
	///     </para>
	/// </remarks>
	public List<string> ExposedHeaders { get; set; } = [];

	/// <summary>
	/// Gets or sets the maximum time (in seconds) that the results of a preflight request can be cached.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Preflight requests (OPTIONS) are sent by browsers before actual cross-origin requests. Setting a cache
	///     duration reduces the number of preflight requests.
	///     </para>
	///     <para>
	///     If <see langword="null"/>, no cache duration is specified (browser default applies). Typical values: 600
	///     (10 minutes), 3600 (1 hour), 86400 (24 hours).
	///     </para>
	/// </remarks>
	[Range(0, int.MaxValue, ErrorMessage = PreflightMaxAgeRangeError)]
	public int? PreflightMaxAge { get; set; }

	/// <inheritdoc/>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		// If CORS is disabled, no further validation is needed.
		if (!Enabled)
			yield break;

		// Validate: AllowedOrigins must not be empty when CORS is enabled.
		// This ensures that at least one origin is specified.
		// An empty list would effectively block all cross-origin requests.
		// This is a common misconfiguration to catch.
		if (AllowedOrigins.Count == 0)
		{
			yield return new ValidationResult(
				"Cors:Enabled is set to true, but Cors:AllowedOrigins is empty. " +
				"You must specify at least one allowed origin, or set Enabled to false.",
				[nameof(AllowedOrigins)]);
		}

		// Validate: AllowCredentials cannot be true when AllowedOrigins contains "*".
		// This is a security violation as it would allow any origin to send credentials.
		// We must ensure that exact origins are specified when credentials are allowed.
		// This is a common security pitfall to prevent.
		if (AllowCredentials && AllowedOrigins.Contains("*"))
		{
			yield return new ValidationResult(
				"Cors:AllowCredentials cannot be true when Cors:AllowedOrigins contains '*' (wildcard). " +
				"Specify exact origins instead for security.",
				[nameof(AllowCredentials), nameof(AllowedOrigins)]);
		}
	}
}
