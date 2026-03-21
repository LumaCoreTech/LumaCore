// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data;
using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Manages token revocation using a database-backed blacklist with an optional in-memory cache layer.
/// </summary>
/// <remarks>
///     <para>
///     The cache stores only <em>negative</em> results ("this <c>jti</c> is <b>not</b> revoked") with an absolute
///     expiration controlled by <see cref="TokenRevocationOptions.CacheDurationSeconds"/>. This covers the hot
///     path — every authenticated request for a valid, non-revoked token can be answered from the cache without
///     hitting the database.
///     </para>
///     <para>
///     Revoked tokens are not cached: when a lookup finds a <c>jti</c> in the <c>RevokedJwts</c> table, the
///     result is returned directly without storing it. This is harmless because the client receives a <c>401</c>
///     and stops presenting the token. When <see cref="RevokeAsync"/> records a new revocation, it evicts the
///     corresponding cache entry so the next check on this instance hits the database and discovers the revocation
///     immediately.
///     </para>
///     <para>
///     This design ensures that revocation propagates immediately on the same application instance (cache eviction)
///     and within the configured cache duration on other instances in a multi-instance deployment.
///     </para>
/// </remarks>
sealed class TokenRevocationService : ITokenRevocationService
{
	/// <summary>
	/// Prefix for cache keys to avoid collisions with other cache consumers.
	/// </summary>
	private const string CacheKeyPrefix = "token:notrevoked:";

	private readonly LumaCoreDbContext      mDbContext;
	private readonly IMemoryCache           mCache;
	private readonly TimeProvider           mTimeProvider;
	private readonly TokenRevocationOptions mOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="TokenRevocationService"/> class.
	/// </summary>
	/// <param name="dbContext">The EF Core database context for accessing the revocation table.</param>
	/// <param name="cache">The in-memory cache for caching negative lookup results.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <param name="options">The token revocation configuration options.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="dbContext"/>, <paramref name="cache"/>, or <paramref name="options"/> is
	/// <see langword="null"/>.
	/// </exception>
	public TokenRevocationService(
		LumaCoreDbContext                dbContext,
		IMemoryCache                     cache,
		TimeProvider                     timeProvider,
		IOptions<TokenRevocationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(options);
		mDbContext = dbContext;
		mCache = cache;
		mTimeProvider = timeProvider;
		mOptions = options.Value;
	}

	/// <inheritdoc/>
	public async Task RevokeAsync(
		string            jti,
		DateTime          expiresAtUtc,
		string            subject,
		string            reason,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jti);

		// Evict any cached "not revoked" entry so subsequent checks on this instance hit the database.
		mCache.Remove(CacheKeyPrefix + jti);

		bool alreadyRevoked = await mDbContext.RevokedJwts
			                      .AnyAsync(r => r.Jti == jti, cancellationToken)
			                      .ConfigureAwait(false);

		if (alreadyRevoked)
			return;

		mDbContext.RevokedJwts.Add(
			new RevokedJwtEntity
			{
				Jti = jti,
				ExpiresAtUtc = expiresAtUtc,
				RevokedAtUtc = mTimeProvider.GetUtcNow().UtcDateTime,
				Subject = subject,
				Reason = reason
			});

		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jti);

		string cacheKey = CacheKeyPrefix + jti;

		// A cached entry means "we checked recently and the token was NOT revoked."
		if (mOptions.CacheDurationSeconds > 0 && mCache.TryGetValue(cacheKey, out object? _))
			return false;

		bool isRevoked = await mDbContext.RevokedJwts
			                 .AnyAsync(r => r.Jti == jti, cancellationToken)
			                 .ConfigureAwait(false);

		if (!isRevoked && mOptions.CacheDurationSeconds > 0)
		{
			var cacheEntryOptions = new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(mOptions.CacheDurationSeconds)
			};

			mCache.Set(cacheKey, true, cacheEntryOptions);
		}

		return isRevoked;
	}
}
