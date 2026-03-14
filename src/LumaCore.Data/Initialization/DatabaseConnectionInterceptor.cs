// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;
using System.Net.Sockets;

using LumaCore.Core.Diagnostics;
using LumaCore.Data.Providers;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Data.Initialization;

/// <summary>
/// An EF Core interceptor that detects database connection and command failures and updates the
/// <see cref="DatabaseInitializationStatus"/> accordingly.
/// </summary>
/// <remarks>
///     <para>
///     This interceptor monitors both database connections and commands executed through EF Core.
///     It implements two interceptor interfaces:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="DbCommandInterceptor"/>: Catches failures during command execution (SELECT, INSERT, etc.)
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="IDbConnectionInterceptor"/>: Catches failures during connection establishment (handshake, auth)
///             </description>
///         </item>
///     </list>
///     <para>
///     When a failure occurs due to a connection-related error (network failure, database server unavailable, etc.),
///     it sets the database status to <see cref="DatabaseInitializationState.Disconnected"/>. This allows the
///     middleware to immediately reject subsequent requests with HTTP 503 instead of letting each request discover
///     the failure independently.
///     </para>
///     <para>
///     <b>Performance:</b> The interceptor has minimal overhead in the success path — it only performs additional
///     work when a failure occurs. The failure detection logic checks for known connection-related exception types.
///     </para>
///     <para>
///     <b>Circuit breaker:</b> To prevent transient network glitches from triggering false alarms, the interceptor
///     uses a threshold-based approach: the status only changes to <see cref="DatabaseInitializationState.Disconnected"/>
///     after <c>Database:Recovery:FailureThreshold</c> failures occur within
///     <c>Database:Recovery:FailureWindowSeconds</c> seconds.
///     </para>
///     <para>
///     <b>Recovery:</b> Once the status is set to <see cref="DatabaseInitializationState.Disconnected"/>, the
///     <see cref="DatabaseConnectionMonitorService"/> will periodically attempt to restore the connection and
///     set the status back to <see cref="DatabaseInitializationState.Completed"/>.
///     </para>
/// </remarks>
public sealed class DatabaseConnectionInterceptor : DbCommandInterceptor, IDbConnectionInterceptor
{
	private readonly DatabaseInitializationStatus           mInitializationStatus;
	private readonly IDatabaseProviderOperations            mProviderOperations;
	private readonly ILogger<DatabaseConnectionInterceptor> mLogger;
	private readonly TimeProvider                           mTimeProvider;
	private readonly int                                    mFailureThreshold;
	private readonly TimeSpan                               mFailureWindow;

	/// <summary>
	/// Lock object for thread-safe access to the failure timestamps list.
	/// </summary>
	private readonly Lock mFailureLock = new();

	/// <summary>
	/// Timestamps of recent failures within the sliding window.
	/// Protected by <see cref="mFailureLock"/>.
	/// </summary>
	private readonly List<DateTimeOffset> mFailureTimestamps = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="DatabaseConnectionInterceptor"/> class.
	/// </summary>
	/// <param name="initializationStatus">The database status tracker.</param>
	/// <param name="providerOperations">The provider-specific operations for detecting service-unavailable errors.</param>
	/// <param name="options">The database configuration options.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <param name="logger">The logger instance.</param>
	public DatabaseConnectionInterceptor(
		DatabaseInitializationStatus           initializationStatus,
		IDatabaseProviderOperations            providerOperations,
		IOptions<DatabaseOptions>              options,
		TimeProvider                           timeProvider,
		ILogger<DatabaseConnectionInterceptor> logger)
	{
		mInitializationStatus = initializationStatus;
		mProviderOperations = providerOperations;
		mLogger = logger;
		mTimeProvider = timeProvider;

		DatabaseOptions.RecoveryOptions recoveryOptions = options.Value.Recovery;
		mFailureThreshold = Math.Max(1, recoveryOptions.FailureThreshold);
		mFailureWindow = TimeSpan.FromSeconds(Math.Max(1, recoveryOptions.FailureWindowSeconds));
	}

	/// <inheritdoc/>
	public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
	{
		HandleCommandFailure(eventData.Exception);
		base.CommandFailed(command, eventData);
	}

	/// <inheritdoc/>
	public override Task CommandFailedAsync(
		DbCommand             command,
		CommandErrorEventData eventData,
		CancellationToken     cancellationToken = default)
	{
		HandleCommandFailure(eventData.Exception);
		return base.CommandFailedAsync(command, eventData, cancellationToken);
	}

	#region Implementation of IDbConnectionInterceptor

	/// <inheritdoc/>
	/// <remarks>
	/// Catches synchronous connection failures that occur during <see cref="DbConnection.Open"/>.
	/// This is important for detecting failures that happen before any command is executed,
	/// such as network unreachable, authentication failures, or server not responding.
	/// </remarks>
	void IDbConnectionInterceptor.ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
	{
		HandleConnectionFailure(eventData.Exception);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Catches asynchronous connection failures that occur during <see cref="DbConnection.OpenAsync(CancellationToken)"/>.
	/// This is the async counterpart to <see cref="IDbConnectionInterceptor.ConnectionFailed"/> and handles the same
	/// failure scenarios.
	/// </remarks>
	Task IDbConnectionInterceptor.ConnectionFailedAsync(
		DbConnection             connection,
		ConnectionErrorEventData eventData,
		CancellationToken        cancellationToken)
	{
		HandleConnectionFailure(eventData.Exception);
		return Task.CompletedTask;
	}

	#endregion

	/// <summary>
	/// Handles a command failure by checking if it's a connection-related error and updating the status accordingly.
	/// </summary>
	/// <param name="exception">The exception that caused the command to fail.</param>
	internal void HandleCommandFailure(Exception exception)
	{
		// Only handle failures when the database was previously working.
		// If we're already in Failed/Disconnected/InProgress state, don't update.
		if (mInitializationStatus.State != DatabaseInitializationState.Completed)
			return;

		// Walk the full exception chain to exclude user-initiated cancellations.
		if (!IsConnectionRelatedError(exception))
			return;

		// Record the failure and check if threshold is reached.
		if (!RecordFailureAndCheckThreshold())
		{
			mLogger.LogDebug(
				exception,
				"Database command failure detected (connection-related) — failure count below threshold, not triggering circuit breaker yet");
			return;
		}

		// Test hook: allows injecting a concurrent state change between threshold confirmation and re-check.
		ExecutionStageMonitor.ReportStage("HandleCommandFailure.BeforeRecheck");

		// Re-check: another thread may have already transitioned to Disconnected between
		// the initial state check and the threshold check. This narrows the race window
		// and prevents duplicate warning logs. SetDisconnected() is guarded by a lock,
		// so correctness is guaranteed regardless — this just avoids noise.
		if (mInitializationStatus.State != DatabaseInitializationState.Completed)
			return;

		mLogger.LogWarning(
			exception,
			"Database command failure threshold reached — setting status to Disconnected, the connection monitor service will attempt to recover");

		mInitializationStatus.SetDisconnected(
			exception,
			"Database connection lost. The service will automatically recover when the connection is restored.");
	}

	/// <summary>
	/// Handles a connection failure by checking if it's a service-unavailable error and updating the status accordingly.
	/// </summary>
	/// <param name="exception">The exception that caused the connection to fail.</param>
	/// <remarks>
	/// Unlike <see cref="HandleCommandFailure"/>, connection failures skip the provider-specific error code checks
	/// because they occur during the connection handshake phase — if the connection cannot be established,
	/// the database is definitively unreachable. However, cancellations are still excluded by walking the full
	/// exception chain.
	/// </remarks>
	internal void HandleConnectionFailure(Exception exception)
	{
		// Only handle failures when the database was previously working.
		// If we're already in Failed/Disconnected/InProgress state, don't update.
		if (mInitializationStatus.State != DatabaseInitializationState.Completed)
			return;

		// Walk the full exception chain to exclude user-initiated cancellations.
		// Providers may wrap OperationCanceledException inside provider-specific exceptions
		// (e.g., Npgsql wraps it inside NpgsqlException when OpenAsync() is cancelled).
		if (ContainsCancellation(exception))
			return;

		// Record the failure and check if threshold is reached.
		if (!RecordFailureAndCheckThreshold())
		{
			mLogger.LogDebug(
				exception,
				"Database connection failure detected (during connection establishment) — failure count below threshold, not triggering circuit breaker yet");
			return;
		}

		// Test hook: allows injecting a concurrent state change between threshold confirmation and re-check.
		ExecutionStageMonitor.ReportStage("HandleConnectionFailure.BeforeRecheck");

		// Re-check: another thread may have already transitioned to Disconnected between
		// the initial state check and the threshold check. This narrows the race window
		// and prevents duplicate warning logs. SetDisconnected() is guarded by a lock,
		// so correctness is guaranteed regardless — this just avoids noise.
		if (mInitializationStatus.State != DatabaseInitializationState.Completed)
			return;

		mLogger.LogWarning(
			exception,
			"Database connection failure threshold reached — setting status to Disconnected, the connection monitor service will attempt to recover");


		mInitializationStatus.SetDisconnected(
			exception,
			"Database connection lost. The service will automatically recover when the connection is restored.");
	}

	/// <summary>
	/// Records a failure timestamp and determines whether the failure threshold has been reached.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if the number of failures within the sliding window has reached or exceeded
	/// <see cref="mFailureThreshold"/>; otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method implements a sliding window algorithm: it removes timestamps older than
	///     <see cref="mFailureWindow"/>, adds the current timestamp, and checks if the count meets the threshold.
	///     </para>
	///     <para>
	///     <b>Thread safety:</b> Access to the timestamps list is protected by <see cref="mFailureLock"/>.
	///     </para>
	/// </remarks>
	private bool RecordFailureAndCheckThreshold()
	{
		DateTimeOffset now = mTimeProvider.GetUtcNow();
		DateTimeOffset windowStart = now - mFailureWindow;

		lock (mFailureLock)
		{
			// Remove timestamps outside the sliding window.
			mFailureTimestamps.RemoveAll(t => t < windowStart);

			// Add the current failure.
			mFailureTimestamps.Add(now);

			// Check if threshold is reached.
			return mFailureTimestamps.Count >= mFailureThreshold;
		}
	}

	/// <summary>
	/// Resets the failure counter. Called when the database connection is restored.
	/// </summary>
	/// <remarks>
	/// This method should be called by <see cref="DatabaseConnectionMonitorService"/> after successful recovery
	/// to clear the failure history and prevent stale failures from affecting future threshold calculations.
	/// </remarks>
	internal void ResetFailureCounter()
	{
		lock (mFailureLock)
		{
			mFailureTimestamps.Clear();
		}
	}

	/// <summary>
	/// Checks whether the exception tree contains an <see cref="OperationCanceledException"/>.
	/// </summary>
	/// <param name="exception">The exception to inspect.</param>
	/// <returns>
	/// <see langword="true"/> if any exception in the tree is an <see cref="OperationCanceledException"/>;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	///     <para>
	///     Database providers may wrap <see cref="OperationCanceledException"/> inside their own exception types.
	///     For example, Npgsql wraps it inside <c>NpgsqlException</c> when
	///     <see cref="DbConnection.OpenAsync(CancellationToken)"/> is cancelled.
	///     </para>
	///     <para>
	///     This method performs a depth-first traversal of the full exception tree, including
	///     <see cref="AggregateException.InnerExceptions"/> (multiple children) and regular
	///     <see cref="Exception.InnerException"/> (single child), to catch cancellations regardless of how
	///     deeply they are nested.
	///     </para>
	/// </remarks>
	private static bool ContainsCancellation(Exception exception)
	{
		Stack<Exception> stack = new();
		stack.Push(exception);

		while (stack.Count > 0)
		{
			Exception current = stack.Pop();

			if (current is OperationCanceledException)
				return true;

			// AggregateException: traverse all inner exceptions (InnerException is InnerExceptions[0]).
			if (current is AggregateException aggregate)
			{
				foreach (Exception inner in aggregate.InnerExceptions)
				{
					stack.Push(inner);
				}
			}
			else if (current.InnerException is not null)
			{
				stack.Push(current.InnerException);
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether the specified exception indicates a connection-related failure.
	/// </summary>
	/// <param name="exception">The exception to check.</param>
	/// <returns>
	/// <see langword="true"/> if the exception indicates a connection failure (network error, server unavailable,
	/// timeout, etc.); otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// Delegates to <see cref="IDatabaseProviderOperations.IsServiceUnavailable"/> for provider-specific error code
	/// checks and generic connection-related exception detection (e.g., <see cref="TimeoutException"/>,
	/// <see cref="SocketException"/>, <see cref="EndOfStreamException"/>).
	/// </remarks>
	private bool IsConnectionRelatedError(Exception exception) => mProviderOperations.IsServiceUnavailable(exception);
}
