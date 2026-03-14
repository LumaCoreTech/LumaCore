// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Seeding;

/// <summary>
/// Manages seed execution and history tracking.
/// </summary>
/// <remarks>
/// This service ensures that seeds are executed only when needed, tracks their versions,
/// and provides infrastructure for idempotent database seeding with automatic retry logic.
/// </remarks>
public sealed class SeedExecutor
{
	private readonly ILogger<SeedExecutor> mLogger;
	private readonly TimeProvider          mTimeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="SeedExecutor"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	public SeedExecutor(ILogger<SeedExecutor> logger, TimeProvider timeProvider)
	{
		mLogger = logger;
		mTimeProvider = timeProvider;
	}

	/// <summary>
	/// Executes a seed if it hasn't been applied yet or if a newer version is available.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="seed">The seed definition to execute.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the seed was executed; <see langword="false"/> if it was skipped.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method wraps the seed execution and history update in a single transaction to ensure atomicity.
	///     If the seed succeeds but the history update fails (e.g., app crash), the entire operation is rolled back.
	///     This prevents inconsistencies where seeds are applied but not recorded in history.
	///     </para>
	///     <para>
	///     Includes retry logic with fixed 50ms delay to handle transient errors (e.g., concurrent inserts, deadlocks).
	///     Uses fixed delay instead of exponential backoff for predictable startup performance.
	///     Each retry attempt clears the change tracker and opens a fresh transaction to avoid working with stale
	///     entity state or invalid transaction state from the previous attempt.
	///     </para>
	/// </remarks>
	public async Task<bool> ExecuteSeedIfNeededAsync(
		LumaCoreDbContext dbContext,
		ISeedDefinition   seed,
		CancellationToken cancellationToken)
	{
		// Check if seed has been applied
		SeedHistoryEntity? existingSeed = await dbContext
			                                  .SeedHistory
			                                  .FirstOrDefaultAsync(s => s.SeedId == seed.SeedId, cancellationToken)
			                                  .ConfigureAwait(false);

		if (existingSeed != null)
		{
			// Compare versions (simple integer comparison)
			if (seed.Version <= existingSeed.Version)
			{
				mLogger.LogDebug(
					"Seed '{SeedId}' version {Version} already applied, skipping",
					seed.SeedId,
					existingSeed.Version);

				return false;
			}

			mLogger.LogInformation(
				"Seed '{SeedId}' has newer version {NewVersion} (current: {OldVersion}), re-executing",
				seed.SeedId,
				seed.Version,
				existingSeed.Version);
		}
		else
		{
			mLogger.LogInformation(
				"Executing seed '{SeedId}' version {Version}: {Description}",
				seed.SeedId,
				seed.Version,
				seed.Description);
		}

		// Retry strategy for transient errors (e.g., concurrent inserts, deadlocks)
		// Uses fixed delay for predictable startup performance (not exponential backoff)
		const int maxRetries = 3;
		const int retryDelayMs = 50; // Fixed 50ms delay for fast startup

		for (int attempt = 1; attempt <= maxRetries; attempt++)
		{
			// Reset the change tracker to discard entities accumulated from any previous failed attempt.
			// Without this, retries accumulate duplicate Added entities (roles, SeedHistory) and always
			// fail with a unique constraint violation — making the retry mechanism self-defeating.
			dbContext.ChangeTracker.Clear();

			// Open a fresh transaction for each attempt (important after failures!)
			// A failed transaction is in an invalid state and must be discarded
			IDbContextTransaction transaction = await dbContext
				                                    .Database
				                                    .BeginTransactionAsync(cancellationToken)
				                                    .ConfigureAwait(false);

			try
			{
				// Execute seed operation
				await seed.ExecuteAsync(dbContext, cancellationToken).ConfigureAwait(false);

				// Re-query seed history inside the loop because ChangeTracker.Clear() detaches any
				// previously tracked entity. The query hits the unique index on SeedId and is fast.
				SeedHistoryEntity? existingHistory = await dbContext
					                                     .SeedHistory
					                                     .FirstOrDefaultAsync(
						                                     s => s.SeedId == seed.SeedId,
						                                     cancellationToken)
					                                     .ConfigureAwait(false);

				// Update or insert seed history (within same transaction as seed operation)
				if (existingHistory != null)
				{
					existingHistory.Version = seed.Version;
					existingHistory.Description = seed.Description;
					existingHistory.AppliedAtUtc = mTimeProvider.GetUtcNow().UtcDateTime;
				}
				else
				{
					dbContext.SeedHistory.Add(
						new SeedHistoryEntity
						{
							SeedId = seed.SeedId,
							Version = seed.Version,
							Description = seed.Description,
							AppliedAtUtc = mTimeProvider.GetUtcNow().UtcDateTime
						});
				}

				// Save both seed changes and history record
				await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

				// Commit both seed data and history together (atomic operation)
				await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

				mLogger.LogInformation(
					"Seed '{SeedId}' version {Version} executed successfully",
					seed.SeedId,
					seed.Version);

				return true;
			}
			catch (DbUpdateException ex) when (attempt < maxRetries)
			{
				// Transient error (e.g., unique constraint violation from concurrent insert)
				// Transaction is rolled back via DisposeAsync() below

				mLogger.LogWarning(
					ex,
					"Seed '{SeedId}' failed on attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs}ms...",
					seed.SeedId,
					attempt,
					maxRetries,
					retryDelayMs);

				await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
				// Loop continues with fresh transaction for next attempt
			}
			catch (Exception ex)
			{
				// Non-transient error (schema problems, permissions, etc.) - fail immediately
				// Transaction is rolled back via DisposeAsync() below
				mLogger.LogError(
					ex,
					"Seed '{SeedId}' failed with non-retryable error on attempt {Attempt}",
					seed.SeedId,
					attempt);
				throw;
			}
			finally
			{
				await transaction.DisposeAsync().ConfigureAwait(false);
			}
		}

		// All retries exhausted
		throw new InvalidOperationException(
			$"Seed '{seed.SeedId}' failed after {maxRetries} attempts. This may indicate a persistent concurrency issue.");
	}

	/// <summary>
	/// Executes all provided seeds in order.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="seeds">The seeds to execute.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of seeds that were executed.</returns>
	public async Task<int> ExecuteSeedsAsync(
		LumaCoreDbContext            dbContext,
		IEnumerable<ISeedDefinition> seeds,
		CancellationToken            cancellationToken)
	{
		int executedCount = 0;

		foreach (ISeedDefinition seed in seeds)
		{
			bool executed = await ExecuteSeedIfNeededAsync(dbContext, seed, cancellationToken).ConfigureAwait(false);
			if (executed)
			{
				executedCount++;
			}
		}

		return executedCount;
	}
}
