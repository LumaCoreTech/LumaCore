// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;
using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

// Token revocation: from persistence and cache coherence to input validation.
//
// These tests follow RevokeAsync() from its core behavior to edge cases and guard
// clauses:
//
//   1. Persistence: new token saved with correct properties (WhenTokenNotYetRevoked),
//      duplicate revocation is a no-op that preserves the original entry (WhenTokenAlreadyRevoked).
//   2. Concurrency: a concurrent insert of the same jti between the AnyAsync() check and SaveChangesAsync()
//      is resolved gracefully via the PK constraint (WhenConcurrentInsertOccurs); a non-duplicate
//      DbUpdateException is rethrown as an unrecoverable error (WhenDbUpdateExceptionIsNotDuplicate).
//   3. Cache coherence: cached "not revoked" entry evicted on revocation (WhenCacheEntryExists).
//   4. Time sourcing: RevokedAtUtc uses the injected TimeProvider (UsesTimeProviderForTimestamp).
//   5. Input validation: null jti → ArgumentNullException (WhenJtiIsNull),
//      empty/whitespace jti → ArgumentException (WhenJtiIsEmptyOrWhiteSpace).
//
// For revocation lookup tests, see IsRevokedAsync().
public sealed partial class TokenRevocationServiceTests
{
	// --- 1. Persistence ---

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.RevokeAsync"/> persists a
	/// <see cref="RevokedJwtEntity"/> with the correct properties when the token has not been revoked before.
	/// </summary>
	[Fact]
	public async Task RevokeAsync_WhenTokenNotYetRevoked_PersistsEntity()
	{
		// Arrange
		var harness = new TestHarness();

		try
		{
			var expiresAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act
			bool result = await harness.Service.RevokeAsync("jti-1", expiresAtUtc, "user@example.com", "Logout");

			// Assert
			Assert.True(result);
			RevokedJwtEntity? entity = await harness.DbContext.RevokedJwts.SingleOrDefaultAsync(r => r.Jti == "jti-1");
			Assert.NotNull(entity);

			Assert.Equal("jti-1", entity.Jti);
			Assert.Equal(expiresAtUtc, entity.ExpiresAtUtc);
			Assert.Equal(harness.TimeProvider.GetUtcNow().UtcDateTime, entity.RevokedAtUtc);
			Assert.Equal("user@example.com", entity.Subject);
			Assert.Equal("Logout", entity.Reason);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.RevokeAsync"/> is idempotent:
	/// revoking the same <c>jti</c> twice does not create a duplicate entry and preserves
	/// the original entity's properties unchanged.
	/// </summary>
	[Fact]
	public async Task RevokeAsync_WhenTokenAlreadyRevoked_DoesNotDuplicate()
	{
		// Arrange
		var harness = new TestHarness();

		try
		{
			var expiresAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
			bool firstResult = await harness.Service.RevokeAsync("jti-dup", expiresAtUtc, "user@example.com", "Logout");

			// Act — revoke the same jti again.
			bool secondResult = await harness.Service.RevokeAsync(
				                    "jti-dup",
				                    expiresAtUtc,
				                    "user@example.com",
				                    "Admin revocation");

			// Assert — first call created the entry, second was a no-op.
			Assert.True(firstResult);
			Assert.False(secondResult);
			int count = await harness.DbContext.RevokedJwts.CountAsync(r => r.Jti == "jti-dup");
			Assert.Equal(1, count);

			RevokedJwtEntity? entity = await harness.DbContext
				                           .RevokedJwts
				                           .SingleOrDefaultAsync(r => r.Jti == "jti-dup");
			Assert.NotNull(entity);

			Assert.Equal("user@example.com", entity.Subject);
			Assert.Equal("Logout", entity.Reason);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Concurrency ---

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.RevokeAsync"/> handles a concurrent insert gracefully:
	/// when another instance inserts the same <c>jti</c> between the <c>AnyAsync()</c> check and
	/// <c>SaveChangesAsync()</c>, the resulting primary-key violation is caught and treated as an idempotent
	/// duplicate — returning <see langword="false"/> instead of propagating a <see cref="DbUpdateException"/>.
	/// </summary>
	/// <remarks>
	/// The race condition is simulated deterministically using the <see cref="DbContext.SavingChanges"/> event:
	/// a second harness inserts the same entity just before the first harness's <c>SaveChangesAsync()</c> executes
	/// the INSERT statement, guaranteeing a primary-key violation that exercises the catch block.
	/// </remarks>
	[Fact]
	public async Task RevokeAsync_WhenConcurrentInsertOccurs_ReturnsFalseGracefully()
	{
		// Arrange — two harnesses sharing the same database simulate two service instances.
		var harness1 = new TestHarness();
		var harness2 = new TestHarness(harness1.ConnectionString);

		try
		{
			var expiresAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

			// Intercept harness1's SaveChanges() to simulate a TOCTOU race: harness2 inserts the same
			// entity just before harness1's INSERT reaches the database, causing a PK violation.
			// SavingChanges fires after AnyAsync() returned false (DB was empty) but before the actual
			// INSERT is sent to SQLite — the perfect injection point for the competing write.
			harness1.DbContext.SavingChanges += (_, _) =>
			{
				harness2.DbContext.RevokedJwts.Add(
					new RevokedJwtEntity
					{
						Jti = "jti-race",
						ExpiresAtUtc = expiresAtUtc,
						RevokedAtUtc = harness2.TimeProvider.GetUtcNow().UtcDateTime,
						Subject = "winner@example.com",
						Reason = "Concurrent insert"
					});
				harness2.DbContext.SaveChanges();
			};

			// Act — AnyAsync() returns false (DB is empty at check time), but SaveChangesAsync() hits
			// the PK violation injected by the SavingChanges handler. The catch block resolves the
			// race by re-checking the database and confirming the duplicate.
			bool result = await harness1.Service.RevokeAsync(
				              "jti-race",
				              expiresAtUtc,
				              "latecomer@example.com",
				              "Late insert");

			// Assert — race resolved gracefully: returns false, first writer's entry preserved.
			Assert.False(result);

			RevokedJwtEntity? entity = await harness1.DbContext
				                           .RevokedJwts
				                           .SingleOrDefaultAsync(r => r.Jti == "jti-race");
			Assert.NotNull(entity);

			Assert.Equal("winner@example.com", entity.Subject);
			Assert.Equal("Concurrent insert", entity.Reason);
		}
		finally
		{
			await harness2.DisposeAsync();
			await harness1.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.RevokeAsync"/> rethrows a
	/// <see cref="DbUpdateException"/> when the post-failure re-check confirms that the error was
	/// <b>not</b> caused by a duplicate <c>jti</c> — i.e., the exception represents an unrecoverable
	/// database error rather than a benign race condition.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="RevokeAsync_WhenConcurrentInsertOccurs_ReturnsFalseGracefully"/> which injects a
	/// competing row (causing a real PK violation with a confirmable duplicate), this test throws a
	/// <see cref="DbUpdateException"/> directly from the <see cref="DbContext.SavingChanges"/> handler
	/// without inserting any row. The <c>AnyAsync()</c> re-check finds nothing → not a duplicate → rethrow.
	/// </remarks>
	[Fact]
	public async Task RevokeAsync_WhenDbUpdateExceptionIsNotDuplicate_Rethrows()
	{
		// Arrange — throw a DbUpdateException from SavingChanges without inserting a competing row.
		// Since no INSERT reaches the database, the AnyAsync() re-check finds no entry and the
		// exception is correctly rethrown as an unrecoverable error.
		var harness = new TestHarness();

		try
		{
			// Inject a non-duplicate database error by throwing from SavingChanges without inserting a row.
			// This simulates an unexpected database failure (e.g., connection issue) rather than a
			// duplicate key violation. The test verifies that such exceptions are not swallowed by the catch block.
			harness.DbContext.SavingChanges += (_, _) =>
				throw new DbUpdateException("Simulated non-duplicate database error");

			// Act + Assert
			var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
				         harness.Service.RevokeAsync(
					         "jti-rethrow",
					         new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
					         "user@example.com",
					         "Logout"));

			Assert.Equal("Simulated non-duplicate database error", ex.Message);

			// Verify no entity was persisted — the exception prevented the INSERT.
			bool exists = await harness.DbContext.RevokedJwts.AnyAsync(r => r.Jti == "jti-rethrow");
			Assert.False(exists);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Cache coherence ---

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.RevokeAsync"/> evicts any
	/// existing negative-result cache entry for the revoked <c>jti</c>, ensuring that a subsequent
	/// <see cref="TokenRevocationService.IsRevokedAsync"/> call hits the database
	/// and discovers the revocation immediately.
	/// </summary>
	[Fact]
	public async Task RevokeAsync_WhenCacheEntryExists_EvictsCacheEntry()
	{
		// Arrange — populate the cache by calling IsRevokedAsync() for a non-revoked token.
		var harness = new TestHarness(cacheDurationSeconds: 30);

		try
		{
			// Initial check to populate the cache: should return false and create a cache entry.
			bool initialCheck = await harness.Service.IsRevokedAsync("jti-cached");
			Assert.False(initialCheck);
			Assert.True(harness.HasCacheEntry("jti-cached"));

			// Act — revoke the token; this must evict the cached "not revoked" entry.
			bool result = await harness.Service.RevokeAsync(
				              "jti-cached",
				              harness.TimeProvider.GetUtcNow().UtcDateTime,
				              "user@example.com",
				              "Logout");

			// Assert — revocation succeeded, cache entry is gone and IsRevokedAsync() returns true.
			Assert.True(result);
			Assert.False(harness.HasCacheEntry("jti-cached"));
			bool afterRevocation = await harness.Service.IsRevokedAsync("jti-cached");
			Assert.True(afterRevocation);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 4. Time sourcing ---

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.RevokeAsync"/> uses
	/// <see cref="TimeProvider.GetUtcNow"/> to set <see cref="RevokedJwtEntity.RevokedAtUtc"/>,
	/// rather than <see cref="DateTime.UtcNow"/>.
	/// </summary>
	[Fact]
	public async Task RevokeAsync_WhenCalled_UsesTimeProviderForTimestamp()
	{
		// Arrange
		var harness = new TestHarness();

		try
		{
			// Advance the fake time provider to a specific point.
			harness.TimeProvider.Advance(TimeSpan.FromHours(3));
			DateTime expectedTimestamp = harness.TimeProvider.GetUtcNow().UtcDateTime;

			// Act
			bool result = await harness.Service.RevokeAsync(
				              "jti-time",
				              new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
				              "user@example.com",
				              "Logout");

			// Assert
			Assert.True(result);
			RevokedJwtEntity? entity = await harness.DbContext
				                           .RevokedJwts
				                           .SingleOrDefaultAsync(r => r.Jti == "jti-time");
			Assert.NotNull(entity);
			Assert.Equal(expectedTimestamp, entity.RevokedAtUtc);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 5. Input validation ---

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.RevokeAsync"/> throws
	/// <see cref="ArgumentNullException"/> when the <c>jti</c> parameter is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task RevokeAsync_WhenJtiIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var harness = new TestHarness();

		try
		{
			DateTime expiresAtUtc = harness.TimeProvider.GetUtcNow().UtcDateTime;

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
				         harness.Service.RevokeAsync(null!, expiresAtUtc, "sub", "reason"));
			Assert.Equal("jti", ex.ParamName);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="TokenRevocationService.RevokeAsync"/> throws
	/// <see cref="ArgumentException"/> when the <c>jti</c> parameter is empty or whitespace.
	/// </summary>
	/// <param name="jti">The invalid JWT ID value.</param>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task RevokeAsync_WhenJtiIsEmptyOrWhiteSpace_ThrowsArgumentException(string jti)
	{
		// Arrange
		var harness = new TestHarness();

		try
		{
			DateTime expiresAtUtc = harness.TimeProvider.GetUtcNow().UtcDateTime;

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         harness.Service.RevokeAsync(jti, expiresAtUtc, "sub", "reason"));
			Assert.Equal("jti", ex.ParamName);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
