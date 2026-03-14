// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Initialization;

/// <summary>
/// Tracks the initialization and connection status of the database.
/// </summary>
/// <remarks>
///     <para>
///     This singleton service is set by <see cref="DatabaseInitializer"/> during startup and updated by
///     <see cref="DatabaseConnectionInterceptor"/> during runtime. It can be queried by middleware, health checks,
///     and other components to determine whether the database is ready to accept requests.
///     </para>
///     <para>
///         <b>State transitions:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="DatabaseInitializationState.NotStarted"/> → <see cref="DatabaseInitializationState.InProgress"/>
///             → <see cref="DatabaseInitializationState.Completed"/> (normal startup)
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="DatabaseInitializationState.InProgress"/> → <see cref="DatabaseInitializationState.Failed"/>
///             (startup failure, triggers retry)
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="DatabaseInitializationState.Completed"/> →
///             <see cref="DatabaseInitializationState.Disconnected"/>
///             (runtime connection loss, triggers recovery polling)
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="DatabaseInitializationState.Disconnected"/> →
///             <see cref="DatabaseInitializationState.Completed"/>
///             (connection restored)
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Thread safety:</b> All state access is protected by a <see cref="Lock"/> instance. The status can be read
///     from any thread (request threads, health checks) and written by the initializer, interceptor, or recovery
///     service.
///     </para>
/// </remarks>
public sealed class DatabaseInitializationStatus
{
	/// <summary>
	/// Maximum number of consecutive failures before the recovery service gives up.
	/// </summary>
	/// <remarks>
	/// After this many consecutive failures, the <see cref="FailureCategory"/> is automatically
	/// set to <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> to prevent
	/// infinite retry loops for issues that were classified as transient but turn out to be persistent
	/// (e.g., database remaining unreachable, disk full, or recurring permission errors).
	/// </remarks>
	public const int MaxConsecutiveFailures = 3;

	/// <summary>
	/// Sentinel value indicating no failure category is set.
	/// </summary>
	private const int NoFailureCategory = -1;

	/// <summary>
	/// Synchronization object protecting all mutable state in this class.
	/// </summary>
	private readonly Lock mLock = new();

	private int        mStateValue           = (int)DatabaseInitializationState.NotStarted;
	private int        mFailureCategoryValue = NoFailureCategory;
	private int        mConsecutiveFailureCount;
	private Exception? mFailureException;
	private string?    mFailureMessage;

	/// <summary>
	/// Gets the current database state.
	/// </summary>
	/// <value>The current <see cref="DatabaseInitializationState"/> value.</value>
	public DatabaseInitializationState State
	{
		get
		{
			lock (mLock) return (DatabaseInitializationState)mStateValue;
		}
	}

	/// <summary>
	/// Gets a value indicating whether the database is ready to accept requests.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if <see cref="State"/> is <see cref="DatabaseInitializationState.Completed"/>;
	/// otherwise, <see langword="false"/>.
	/// </value>
	public bool IsReady
	{
		get
		{
			lock (mLock) return mStateValue == (int)DatabaseInitializationState.Completed;
		}
	}

	/// <summary>
	/// Gets the category of the current failure, indicating whether recovery is possible.
	/// </summary>
	/// <value>
	/// The <see cref="DatabaseFailureCategory"/> if the database is in a failed state;
	/// otherwise, <see langword="null"/>.
	/// </value>
	/// <remarks>
	///     <para>
	///     This property is used by the <see cref="DatabaseConnectionMonitorService"/> to decide whether to retry:
	///     </para>
	///     <list type="bullet">
	///         <item><see cref="DatabaseFailureCategory.Transient"/>: Retry at configured interval</item>
	///         <item><see cref="DatabaseFailureCategory.ConfigurationRequired"/>: Stop retrying</item>
	///         <item><see cref="DatabaseFailureCategory.ManualInterventionRequired"/>: Stop retrying</item>
	///     </list>
	/// </remarks>
	public DatabaseFailureCategory? FailureCategory
	{
		get
		{
			lock (mLock)
			{
				return mFailureCategoryValue == NoFailureCategory
					       ? null
					       : (DatabaseFailureCategory)mFailureCategoryValue;
			}
		}
	}

	/// <summary>
	/// Gets the number of consecutive initialization failures without a successful recovery.
	/// </summary>
	/// <value>
	/// The count of consecutive failures. Reset to 0 when initialization succeeds.
	/// </value>
	/// <remarks>
	/// After <see cref="MaxConsecutiveFailures"/> consecutive failures, the <see cref="FailureCategory"/>
	/// is automatically escalated to <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	/// </remarks>
	public int ConsecutiveFailureCount
	{
		get
		{
			lock (mLock) return mConsecutiveFailureCount;
		}
	}

	/// <summary>
	/// Gets a value indicating whether automatic recovery attempts should continue.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the failure is transient and retry count has not been exceeded;
	/// otherwise, <see langword="false"/>.
	/// </value>
	public bool ShouldRetry
	{
		get
		{
			lock (mLock)
			{
				return mFailureCategoryValue == (int)DatabaseFailureCategory.Transient &&
				       mConsecutiveFailureCount < MaxConsecutiveFailures;
			}
		}
	}

	/// <summary>
	/// Gets the exception that caused the failure or disconnection, if any.
	/// </summary>
	/// <value>
	/// The <see cref="Exception"/> that caused the current failure state, or <see langword="null"/> if the database
	/// is healthy or has not yet been initialized.
	/// </value>
	/// <remarks>
	///     <para>
	///     <b>Unwrapping convention:</b> <see cref="DatabaseInitializer.StartAsync"/> catches
	///     <see cref="DatabaseInitializationException"/> and applies <c>ex.InnerException ?? ex</c>
	///     before calling <see cref="SetFailed"/>. This means the stored exception is always the
	///     <b>root cause</b>, not a transport wrapper:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///         <b>External cause</b> (has <see cref="Exception.InnerException"/>): The inner exception
	///         is stored — an <c>IOException</c>, <c>TimeoutException</c>, or <see cref="AggregateException"/>
	///         bundling migration + restore failures. The <see cref="DatabaseInitializationException"/> shell
	///         is discarded; its category and message are already in <see cref="FailureCategory"/> and
	///         <see cref="FailureMessage"/>.
	///         </item>
	///         <item>
	///         <b>Self-contained cause</b> (no <see cref="Exception.InnerException"/>): The
	///         <see cref="DatabaseInitializationException"/> itself is stored because it <b>is</b> the root
	///         cause — a configuration guard or contract violation with no underlying external error.
	///         </item>
	///     </list>
	///     <para>
	///     Uncategorized exceptions (not wrapped in <see cref="DatabaseInitializationException"/>) are stored
	///     as-is with <see cref="DatabaseFailureCategory.Transient"/>.
	///     </para>
	/// </remarks>
	public Exception? FailureException
	{
		get
		{
			lock (mLock) return mFailureException;
		}
	}

	/// <summary>
	/// Gets a human-readable message describing the failure or disconnection, if any.
	/// </summary>
	/// <value>
	/// A message describing the current problem, or <see langword="null"/> if the database is healthy.
	/// This message is suitable for display in the UI to inform operators of the issue.
	/// </value>
	public string? FailureMessage
	{
		get
		{
			lock (mLock) return mFailureMessage;
		}
	}

	/// <summary>
	/// Marks the database as in progress (initialization or recovery attempt).
	/// </summary>
	/// <remarks>
	/// This method is called at the beginning of initialization or when a recovery attempt starts.
	/// </remarks>
	internal void SetInProgress()
	{
		lock (mLock)
		{
			mStateValue = (int)DatabaseInitializationState.InProgress;
		}
	}

	/// <summary>
	/// Marks the database as ready and clears any previous failure information.
	/// </summary>
	/// <remarks>
	/// This method is called after successful initialization or after a successful recovery from a disconnected state.
	/// It resets both the failure details and the consecutive failure counter.
	/// </remarks>
	internal void SetCompleted()
	{
		lock (mLock)
		{
			mConsecutiveFailureCount = 0;
			mFailureCategoryValue = NoFailureCategory;
			mFailureException = null;
			mFailureMessage = null;
			mStateValue = (int)DatabaseInitializationState.Completed;
		}
	}

	/// <summary>
	/// Marks the initialization as failed with the specified exception, message, and category.
	/// </summary>
	/// <param name="exception">
	/// The unwrapped cause exception to expose via <see cref="FailureException"/>. Callers that catch
	/// <see cref="DatabaseInitializationException"/> must pass <c>ex.InnerException ?? ex</c> — see
	/// <see cref="FailureException"/> remarks for the full unwrapping contract.
	/// </param>
	/// <param name="message">A human-readable message describing the failure, suitable for UI display.</param>
	/// <param name="category">The category of failure, indicating whether automatic recovery is possible.</param>
	/// <remarks>
	///     <para>
	///     This method increments <see cref="ConsecutiveFailureCount"/> each time it is called.
	///     After <see cref="MaxConsecutiveFailures"/> consecutive failures with
	///     <see cref="DatabaseFailureCategory.Transient"/>,
	///     the category is automatically escalated to <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	///     </para>
	///     <para>
	///     The recovery service checks <see cref="ShouldRetry"/> to determine whether to attempt another recovery.
	///     </para>
	/// </remarks>
	internal void SetFailed(Exception exception, string message, DatabaseFailureCategory category)
	{
		lock (mLock)
		{
			mConsecutiveFailureCount++;
			mFailureException = exception;
			mFailureMessage = message;

			// Auto-escalate to ManualInterventionRequired after too many consecutive failures.
			// This prevents infinite retry loops for issues that were classified as transient but
			// turn out to be persistent (e.g., database unreachable, disk full, permission errors).
			if (category == DatabaseFailureCategory.Transient &&
			    mConsecutiveFailureCount >= MaxConsecutiveFailures)
			{
				mFailureCategoryValue = (int)DatabaseFailureCategory.ManualInterventionRequired;
				mFailureMessage =
					$"{message} (Failed {mConsecutiveFailureCount} times consecutively. " +
					"Automatic recovery has been disabled.)";
			}
			else
			{
				mFailureCategoryValue = (int)category;
			}

			mStateValue = (int)DatabaseInitializationState.Failed;
		}
	}

	/// <summary>
	/// Marks the database as disconnected due to a runtime connection failure.
	/// </summary>
	/// <param name="exception">The exception that caused the disconnection.</param>
	/// <param name="message">A human-readable message describing the disconnection.</param>
	/// <remarks>
	///     <para>
	///     This method is called by <see cref="DatabaseConnectionInterceptor"/> when a database operation fails due
	///     to a connection error. Once set, the middleware will reject new requests with HTTP 503 until the connection
	///     is restored.
	///     </para>
	///     <para>
	///     Disconnection is always treated as <see cref="DatabaseFailureCategory.Transient"/> because it typically
	///     resolves on its own (database restart, network recovery, etc.).
	///     </para>
	///     <para>
	///     <b>Thread safety:</b> The state check and transition are performed under the shared lock, preventing
	///     race conditions when multiple request threads detect connection failures simultaneously.
	///     </para>
	/// </remarks>
	internal void SetDisconnected(Exception exception, string message)
	{
		lock (mLock)
		{
			// Only transition to Disconnected if currently Completed.
			// This prevents overwriting Failed state (which requires re-initialization) with Disconnected.
			if (mStateValue != (int)DatabaseInitializationState.Completed)
				return;

			mStateValue = (int)DatabaseInitializationState.Disconnected;
			mFailureCategoryValue = (int)DatabaseFailureCategory.Transient;
			mFailureException = exception;
			mFailureMessage = message;
		}
	}
}
