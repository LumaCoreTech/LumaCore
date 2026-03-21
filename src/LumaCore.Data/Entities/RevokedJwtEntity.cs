// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Claims;

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a revoked JWT access token in the token blacklist.
/// </summary>
/// <remarks>
///     <para>
///     When a token is revoked (e.g., via logout or administrative action), its <see cref="Jti"/> (JWT ID) is
///     recorded in this table. The authentication pipeline checks this table (with an optional memory cache layer)
///     to reject tokens that have been explicitly invalidated before their natural expiry.
///     </para>
///     <para>
///     Entries in this table are inherently short-lived — they only need to exist until the token's natural
///     <see cref="ExpiresAtUtc"/> has passed. A periodic cleanup process can safely remove expired entries
///     without affecting security.
///     </para>
///     <para>
///     <b>Primary key:</b> <see cref="Jti"/> (string, not auto-incremented). This is the JWT ID claim from the
///     token, which is already globally unique.
///     </para>
///     <para>
///     Database constraints and indexes are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public sealed class RevokedJwtEntity
{
	/// <summary>
	/// Gets or sets the JWT ID (<c>jti</c> claim) of the revoked token.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is the primary key. The value is assigned by the token factory when the token is created and is
	///     extracted from the authenticated <see cref="ClaimsPrincipal"/> during revocation.
	///     </para>
	///     <para>
	///     <b>Index:</b> Primary key (clustered).
	///     </para>
	/// </remarks>
	public string Jti { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the UTC timestamp when the token naturally expires.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Stored to enable efficient cleanup of expired revocation entries. Once a token has passed its natural
	///     expiry, the blacklist entry is no longer needed because the JWT validation pipeline rejects it based on
	///     the <c>exp</c> claim alone.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index for cleanup queries.
	///     </para>
	/// </remarks>
	public DateTime ExpiresAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when the token was revoked.
	/// </summary>
	public DateTime RevokedAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the subject (<c>sub</c> claim) of the revoked token.
	/// </summary>
	/// <remarks>
	/// Stored for auditing and diagnostics. Allows filtering revoked tokens by user.
	/// </remarks>
	public string Subject { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the reason for revocation.
	/// </summary>
	/// <remarks>
	/// A short description of why the token was revoked (e.g., <c>"Logout"</c>, <c>"Admin revocation"</c>).
	/// Stored for auditing purposes only — not used in the revocation check itself.
	/// </remarks>
	public string Reason { get; set; } = string.Empty;
}
