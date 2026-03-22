// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;
using LumaCore.Data.Entities;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

// Revocation lookup: from basic lookups through caching semantics to input validation.
//
// These tests follow IsRevokedAsync() from its core behavior through the cache layer's
// edge cases:
//
//   1. Basic lookups: non-revoked → false (WhenTokenNotRevoked),
//      revoked → true (WhenTokenIsRevoked).
//   2. Cache behavior: negative results cached (CachesNegativeResult),
//      positive results not cached (DoesNotCachePositiveResult).
//   3. Multi-instance propagation: externally revoked token stays cached until expiry
//      (WhenRevokedExternallyWhileCached).
//   4. Cache disabled: CacheDurationSeconds=0 bypasses the cache entirely (WhenCacheDisabled).
//   5. Input validation: null jti → ArgumentNullException (WhenJtiIsNull),
//      empty/whitespace jti → ArgumentException (WhenJtiIsEmptyOrWhiteSpace).
//
// For the revocation write path, see RevokeAsync(). Cache eviction on same-instance
// revocation is tested in RevokeAsync() (WhenCacheEntryExists).
public sealed partial class TokenRevocationServiceTests
{
	// --- 1. Basic lookups ---

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.IsRevokedAsync"/> returns
	/// <see langword="false"/> for a token that has not been revoked.
	/// </summary>
	[Fact]
	public async Task IsRevokedAsync_WhenTokenNotRevoked_ReturnsFalse()
	{
		// Arrange
		var harness = new TestHarness();

		try
		{
			// Act
			bool result = await harness.Service.IsRevokedAsync("jti-not-revoked");

			// Assert
			Assert.False(result);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.IsRevokedAsync"/> returns
	/// <see langword="true"/> for a token that has been revoked.
	/// </summary>
	[Fact]
	public async Task IsRevokedAsync_WhenTokenIsRevoked_ReturnsTrue()
	{
		// Arrange
		var harness = new TestHarness();

		try
		{
			await harness.Service.RevokeAsync(
				"jti-revoked",
				new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
				"user@example.com",
				"Logout");

			// Act
			bool result = await harness.Service.IsRevokedAsync("jti-revoked");

			// Assert
			Assert.True(result);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Cache behavior ---

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.IsRevokedAsync"/> caches a
	/// negative result ("not revoked") when the cache is enabled, so that a subsequent call can be served from
	/// memory without querying the database.
	/// </summary>
	[Fact]
	public async Task IsRevokedAsync_WhenTokenNotRevokedAndCacheEnabled_CachesNegativeResult()
	{
		// Arrange
		var harness = new TestHarness(cacheDurationSeconds: 30);

		try
		{
			// Act
			bool result = await harness.Service.IsRevokedAsync("jti-cacheable");

			// Assert
			Assert.False(result);
			Assert.True(harness.HasCacheEntry("jti-cacheable"));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.IsRevokedAsync"/> does
	/// <b>not</b> cache a positive result ("is revoked"). Revoked tokens are rejected immediately;
	/// caching them would be pointless because the client stops presenting the token after a <c>401</c>.
	/// </summary>
	[Fact]
	public async Task IsRevokedAsync_WhenTokenIsRevoked_DoesNotCachePositiveResult()
	{
		// Arrange
		var harness = new TestHarness(cacheDurationSeconds: 30);

		try
		{
			await harness.Service.RevokeAsync(
				"jti-pos",
				new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
				"user@example.com",
				"Logout");

			// Act
			bool result = await harness.Service.IsRevokedAsync("jti-pos");

			// Assert — the token is revoked, but no cache entry is created for positive results.
			Assert.True(result);
			Assert.False(harness.HasCacheEntry("jti-pos"));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Multi-instance propagation ---

	/// <summary>
	/// Verifies the multi-instance propagation delay:
	/// externally (simulating another instance), <see cref="TokenRevocationService.IsRevokedAsync"/>
	/// continues to return <see langword="false"/> until the cache entry expires.
	/// </summary>
	/// <remarks>
	/// This test simulates the multi-instance scenario described in
	/// <see cref="TokenRevocationOptions.CacheDurationSeconds"/>: token is revoked on instance A,
	/// but instance B still serves the cached "not revoked" result until its cache expires.
	/// The cache eviction on same-instance revocation is tested separately in
	/// <see cref="RevokeAsync_WhenCacheEntryExists_EvictsCacheEntry"/>.
	/// </remarks>
	[Fact]
	public async Task IsRevokedAsync_WhenRevokedExternallyWhileCached_ReturnsFalseUntilCacheExpires()
	{
		// Arrange — cache a negative result with a 30-second duration.
		var harness = new TestHarness(cacheDurationSeconds: 30);

		try
		{
			bool initialCheck = await harness.Service.IsRevokedAsync("jti-ext");
			Assert.False(initialCheck);
			Assert.True(harness.HasCacheEntry("jti-ext"));

			// Simulate external revocation by inserting directly into the database (bypassing the service,
			// which would evict the cache — this mimics another instance performing the revocation).
			harness.DbContext.RevokedJwts.Add(
				new RevokedJwtEntity
				{
					Jti = "jti-ext",
					ExpiresAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
					RevokedAtUtc = harness.TimeProvider.GetUtcNow().UtcDateTime,
					Subject = "user@example.com",
					Reason = "Admin revocation on another instance"
				});
			await harness.DbContext.SaveChangesAsync();

			// Act — cache still has the "not revoked" entry, so the service returns false.
			bool stillCached = await harness.Service.IsRevokedAsync("jti-ext");

			// Assert — stale cache returns false despite the token being revoked in the database.
			Assert.False(stillCached);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 4. Cache disabled ---

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.IsRevokedAsync"/> skips the
	/// cache entirely when <see cref="TokenRevocationOptions.CacheDurationSeconds"/>
	/// is <c>0</c>, querying the database on every call.
	/// </summary>
	[Fact]
	public async Task IsRevokedAsync_WhenCacheDisabled_AlwaysQueriesDatabase()
	{
		// Arrange — cache disabled (0 seconds).
		var harness = new TestHarness(cacheDurationSeconds: 0);

		try
		{
			// Act — first call: not revoked. No cache entry should be created.
			bool beforeRevocation = await harness.Service.IsRevokedAsync("jti-nocache");
			Assert.False(beforeRevocation);
			Assert.False(harness.HasCacheEntry("jti-nocache"));

			// Revoke the token.
			await harness.Service.RevokeAsync(
				"jti-nocache",
				new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
				"user@example.com",
				"Logout");

			// Act — second call: now revoked. Without caching, the change is visible immediately.
			bool afterRevocation = await harness.Service.IsRevokedAsync("jti-nocache");

			// Assert
			Assert.True(afterRevocation);
			Assert.False(harness.HasCacheEntry("jti-nocache"));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 5. Input validation ---

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.IsRevokedAsync"/> throws
	/// <see cref="ArgumentNullException"/> when the <c>jti</c> parameter is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task IsRevokedAsync_WhenJtiIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var harness = new TestHarness();

		try
		{
			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => harness.Service.IsRevokedAsync(null!));
			Assert.Equal("jti", ex.ParamName);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.IsRevokedAsync"/> throws
	/// <see cref="ArgumentException"/> when the <c>jti</c> parameter is empty or whitespace.
	/// </summary>
	/// <param name="jti">The invalid JWT ID value.</param>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task IsRevokedAsync_WhenJtiIsEmptyOrWhiteSpace_ThrowsArgumentException(string jti)
	{
		// Arrange
		var harness = new TestHarness();

		try
		{
			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.IsRevokedAsync(jti));
			Assert.Equal("jti", ex.ParamName);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
