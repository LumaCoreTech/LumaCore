// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Provides token revocation (blacklist) operations for JWT access tokens.
/// </summary>
/// <remarks>
///     <para>
///     Implementations maintain a persistent blacklist of revoked JWT IDs (<c>jti</c> claims) in the database and
///     optionally cache negative lookup results (i.e., "not revoked") in memory to reduce database load.
///     </para>
///     <para>
///     Revocation entries are short-lived — they only need to persist until the token's natural expiry has passed.
///     A separate cleanup process can safely remove expired entries.
///     </para>
/// </remarks>
interface ITokenRevocationService
{
	/// <summary>
	/// Records a token as revoked in the persistent blacklist and evicts any cached "not revoked" entry.
	/// If the token is already revoked, the existing entry is preserved unchanged (idempotent, first-write-wins).
	/// </summary>
	/// <remarks>
	/// This method is safe against concurrent revocation of the same <c>jti</c>. If two callers race past the
	/// duplicate check simultaneously, the first insert wins and the second is resolved gracefully via the
	/// database's primary-key constraint — no <see cref="DbUpdateException"/> propagates to the caller.
	/// </remarks>
	/// <param name="jti">The JWT ID (<c>jti</c> claim) of the token to revoke.</param>
	/// <param name="expiresAtUtc">
	/// The token's natural expiry timestamp. Used to determine when the revocation entry can be safely cleaned up.
	/// </param>
	/// <param name="subject">The subject (<c>sub</c> claim) of the revoked token, stored for auditing.</param>
	/// <param name="reason">A short description of why the token was revoked (e.g., <c>"Logout"</c>).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if a new revocation entry was created; <see langword="false"/> if the token was
	/// already revoked and the existing entry was left unchanged.
	/// </returns>
	Task<bool> RevokeAsync(
		string            jti,
		DateTime          expiresAtUtc,
		string            subject,
		string            reason,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks whether a token has been revoked.
	/// </summary>
	/// <param name="jti">The JWT ID (<c>jti</c> claim) to check.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the token is on the revocation blacklist; otherwise, <see langword="false"/>.
	/// </returns>
	Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);
}
