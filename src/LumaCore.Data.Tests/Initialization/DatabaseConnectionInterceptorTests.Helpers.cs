// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

public sealed partial class DatabaseConnectionInterceptorTests
{
	/// <summary>
	/// Creates a <see cref="DatabaseConnectionInterceptor"/> configured for testing.
	/// </summary>
	/// <param name="status">The database initialization status tracker.</param>
	/// <param name="providerOperations">
	/// The provider operations to use. Defaults to a <see cref="FakeProviderOperations"/> that reports
	/// all exceptions as service-unavailable.
	/// </param>
	/// <param name="failureThreshold">
	/// The number of failures within the window required to trip the circuit breaker.
	/// </param>
	/// <param name="failureWindowSeconds">The sliding window duration in seconds.</param>
	/// <param name="timeProvider">The time provider for controlling time in tests.</param>
	/// <returns>A configured <see cref="DatabaseConnectionInterceptor"/> instance.</returns>
	private static DatabaseConnectionInterceptor CreateInterceptor(
		DatabaseInitializationStatus status,
		IDatabaseProviderOperations? providerOperations   = null,
		int                          failureThreshold     = 3,
		int                          failureWindowSeconds = 60,
		TimeProvider?                timeProvider         = null)
	{
		providerOperations ??= new FakeProviderOperations();
		timeProvider ??= TimeProvider.System;

		IOptions<DatabaseOptions> options = Options.Create(
			new DatabaseOptions
			{
				Recovery = new DatabaseOptions.RecoveryOptions
				{
					FailureThreshold = failureThreshold,
					FailureWindowSeconds = failureWindowSeconds
				}
			});

		return new DatabaseConnectionInterceptor(
			status,
			providerOperations,
			options,
			timeProvider,
			NullLogger<DatabaseConnectionInterceptor>.Instance);
	}

	/// <summary>
	/// Creates a <see cref="DatabaseInitializationStatus"/> in the <see cref="DatabaseInitializationState.Completed"/>
	/// state, which is the prerequisite for the interceptor to process failures.
	/// </summary>
	private static DatabaseInitializationStatus CreateCompletedStatus()
	{
		var status = new DatabaseInitializationStatus();
		status.SetCompleted();
		return status;
	}

	/// <summary>
	/// Creates a <see cref="DatabaseInitializationStatus"/> in the specified state for testing state guards.
	/// </summary>
	/// <param name="state">The desired state.</param>
	private static DatabaseInitializationStatus CreateStatusInState(DatabaseInitializationState state)
	{
		var status = new DatabaseInitializationStatus();

		switch (state)
		{
			case DatabaseInitializationState.NotStarted:
				break;

			case DatabaseInitializationState.InProgress:
				status.SetInProgress();
				break;

			case DatabaseInitializationState.Completed:
				status.SetCompleted();
				break;

			case DatabaseInitializationState.Failed:
				status.SetFailed(
					new InvalidOperationException("setup"),
					"Setup failure",
					DatabaseFailureCategory.Transient);
				break;

			case DatabaseInitializationState.Disconnected:
				status.SetCompleted();
				status.SetDisconnected(new InvalidOperationException("setup"), "Setup disconnection");
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported state.");
		}

		return status;
	}

	/// <summary>
	/// Test data containing all <see cref="DatabaseInitializationState"/> values except
	/// <see cref="DatabaseInitializationState.Completed"/>. The interceptor should be a no-op in all of these.
	/// </summary>
	public static TheoryData<string, DatabaseInitializationState> NonCompletedStates_TestData() => new()
	{
		// Initialization has not started yet
		{ "NotStarted", DatabaseInitializationState.NotStarted },

		// Initialization is currently running
		{ "InProgress", DatabaseInitializationState.InProgress },

		// Initialization failed (e.g., migration error)
		{ "Failed", DatabaseInitializationState.Failed },

		// Previously completed, but connection was lost
		{ "Disconnected", DatabaseInitializationState.Disconnected }
	};

	/// <summary>
	/// A fake <see cref="IDatabaseProviderOperations"/> that allows controlling the return value of
	/// <see cref="IDatabaseProviderOperations.IsServiceUnavailable"/>.
	/// </summary>
	/// <remarks>
	/// Only <see cref="IsServiceUnavailable"/> and <see cref="ProviderName"/> are implemented; all other
	/// members throw <see cref="NotSupportedException"/> because they are not called by the interceptor.
	/// </remarks>
	private sealed class FakeProviderOperations(bool serviceUnavailable = true) : IDatabaseProviderOperations
	{
		/// <inheritdoc/>
		public string ProviderName => "fake";

		/// <inheritdoc/>
		public string QuoteIdentifier(string identifier) => throw new NotSupportedException();

		/// <inheritdoc/>
		public bool IsServiceUnavailable(Exception exception) => serviceUnavailable;

		/// <inheritdoc/>
		public Task DropSchemaObjectsAsync(
			LumaCoreDbContext    dbContext,
			IReadOnlySet<string> tablesToPreserve,
			CancellationToken    cancellationToken,
			ILogger?             logger = null) => throw new NotSupportedException();

		/// <inheritdoc/>
		public Task<bool> TableExistsAsync(
			DbConnection      connection,
			string            tableName,
			CancellationToken cancellationToken,
			string?           schema = null) => throw new NotSupportedException();

		/// <inheritdoc/>
		public Task<RestoreCheckpointData?> ReadCheckpointAsync(
			DbConnection      connection,
			string            tableName,
			CancellationToken cancellationToken,
			string?           schema = null) => throw new NotSupportedException();

		/// <inheritdoc/>
		public Task WriteCheckpointAsync(
			LumaCoreDbContext dbContext,
			string            tableName,
			string            shuttleId,
			string            baselineMigrationId,
			string            startedUtc,
			CancellationToken cancellationToken,
			string?           schema = null) => throw new NotSupportedException();

		/// <inheritdoc/>
		public Task UpdateCheckpointPhaseAsync(
			LumaCoreDbContext dbContext,
			string            tableName,
			string            phase,
			string            updatedUtc,
			CancellationToken cancellationToken,
			string?           schema = null) => throw new NotSupportedException();

		/// <inheritdoc/>
		public Task DropCheckpointTableAsync(
			LumaCoreDbContext dbContext,
			string            tableName,
			CancellationToken cancellationToken,
			string?           schema = null) => throw new NotSupportedException();

		/// <inheritdoc/>
		public IDataExportReader CreateExportReader(
			DatabaseOptions options,
			ILogger         logger) => throw new NotSupportedException();

		/// <inheritdoc/>
		public IDataImportWriter CreateImportWriter(
			string       connectionString,
			ILogger      logger,
			TimeProvider timeProvider) => throw new NotSupportedException();

		/// <inheritdoc/>
		public string MapToShuttleStorageType(string providerDbType) => throw new NotSupportedException();
	}
}
