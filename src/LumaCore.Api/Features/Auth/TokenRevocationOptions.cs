// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Configures the token revocation subsystem (blacklist check behavior and caching).
/// </summary>
/// <remarks>
///     <para>
///     Token revocation allows immediate invalidation of JWT access tokens before their natural expiry. When a
///     token is revoked (e.g., via logout or administrative action), its <c>jti</c> (JWT ID) is recorded in the
///     <c>RevokedJwts</c> database table and the authentication pipeline rejects the token on subsequent requests.
///     </para>
///     <para>
///     To avoid a database query on every authenticated request, an in-memory cache stores <em>negative</em>
///     results (i.e., "this token is <b>not</b> revoked"). Positive revocation results are never cached — once a
///     token is revoked, it stays revoked. The cache is automatically evicted when a new revocation is recorded,
///     ensuring near-instant propagation on the same application instance.
///     </para>
///     <para>
///     Set <see cref="CacheDurationSeconds"/> to <c>0</c> to disable caching entirely and check the database on
///     every request. This provides the strongest consistency guarantee at the cost of one additional database
///     query per authenticated request.
///     </para>
///     <para>
///     Options are bound from the configuration section identified by <see cref="SectionName"/>. With the current
///     value this corresponds to the <c>"Jwt:TokenRevocation"</c> section in <c>appsettings.json</c> and
///     environment variables prefixed with <c>Jwt__TokenRevocation__</c>.
///     </para>
/// </remarks>
sealed class TokenRevocationOptions
{
	/// <summary>
	/// Gets the configuration section name used to bind <see cref="TokenRevocationOptions"/>.
	/// </summary>
	public const string SectionName = "Jwt:TokenRevocation";

	/// <summary>
	/// Provides the error message used when <see cref="CacheDurationSeconds"/> is outside the allowed
	/// <c>[0, 60]</c> range.
	/// </summary>
	private const string CacheDurationRangeError =
		"Jwt:TokenRevocation:CacheDurationSeconds must be between 0 and 60 seconds.";

	/// <summary>
	/// Gets or sets the duration (in seconds) for which a "not revoked" lookup result is cached in memory.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Default:</b> <c>5</c> seconds.
	///     </para>
	///     <para>
	///     Higher values reduce database load but increase the window during which a freshly revoked token may
	///     still be accepted. A value of <c>0</c> disables the cache entirely — every authenticated request
	///     queries the database.
	///     </para>
	///     <para>
	///     In a single-instance deployment, cache eviction on revocation makes this window effectively zero for
	///     locally-initiated logouts. The configured duration only matters for multi-instance deployments where
	///     a token is revoked on instance A but the next request hits instance B.
	///     </para>
	/// </remarks>
	[Range(0, 60, ErrorMessage = CacheDurationRangeError)]
	public int CacheDurationSeconds { get; set; } = 5;
}
