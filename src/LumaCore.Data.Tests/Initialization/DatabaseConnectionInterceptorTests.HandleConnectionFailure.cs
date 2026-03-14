// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Data.Initialization;

using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

public sealed partial class DatabaseConnectionInterceptorTests
{
	/// <summary>
	/// Verifies that <see cref="DatabaseConnectionInterceptor.HandleConnectionFailure"/> is a no-op when the
	/// database state is not <see cref="DatabaseInitializationState.Completed"/>.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="state">The current state of the database.</param>
	[Theory]
	[MemberData(nameof(NonCompletedStates_TestData))]
	public void HandleConnectionFailure_WhenStateIsNotCompleted_DoesNotChangeState(
		string                      scenario,
		DatabaseInitializationState state)
	{
		_ = scenario;

		// Arrange
		DatabaseInitializationStatus status = CreateStatusInState(state);
		DatabaseConnectionInterceptor sut = CreateInterceptor(status);

		// Act
		sut.HandleConnectionFailure(new InvalidOperationException("connection refused"));

		// Assert
		Assert.Equal(state, status.State);
	}

	/// <summary>
	/// Test data for <see cref="HandleConnectionFailure_WhenExceptionContainsCancellation_DoesNotChangeState"/>.
	/// Covers direct, derived, wrapped, and aggregated cancellations.
	/// </summary>
	public static TheoryData<string, Exception> CancellationExceptions_TestData() => new()
	{
		// Direct OperationCanceledException
		{
			"Direct OperationCanceledException",
			new OperationCanceledException("User cancelled")
		},

		// TaskCanceledException (derives from OperationCanceledException)
		{
			"TaskCanceledException",
			new TaskCanceledException("Task cancelled")
		},

		// OperationCanceledException wrapped in another exception (e.g., Npgsql wraps it)
		{
			"Wrapped OperationCanceledException",
			new InvalidOperationException("Wrapper", new OperationCanceledException())
		},

		// OperationCanceledException inside AggregateException
		{
			"OperationCanceledException in AggregateException",
			new AggregateException(new OperationCanceledException())
		}
	};

	/// <summary>
	/// Verifies that <see cref="DatabaseConnectionInterceptor.HandleConnectionFailure"/> does not change state
	/// when the exception tree contains an <see cref="OperationCanceledException"/>, because cancellations are
	/// user-initiated and not infrastructure failures.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="exception">The exception containing a cancellation.</param>
	[Theory]
	[MemberData(nameof(CancellationExceptions_TestData))]
	public void HandleConnectionFailure_WhenExceptionContainsCancellation_DoesNotChangeState(
		string    scenario,
		Exception exception)
	{
		_ = scenario;

		// Arrange
		DatabaseInitializationStatus status = CreateCompletedStatus();
		DatabaseConnectionInterceptor sut = CreateInterceptor(status, failureThreshold: 1);

		// Act
		sut.HandleConnectionFailure(exception);

		// Assert — state must remain Completed (cancellation filtered out)
		Assert.Equal(DatabaseInitializationState.Completed, status.State);
		Assert.True(status.IsReady);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseConnectionInterceptor.HandleConnectionFailure"/> does not change state
	/// when the failure count is below the configured threshold.
	/// </summary>
	[Fact]
	public void HandleConnectionFailure_WhenBelowThreshold_StateRemainsCompleted()
	{
		// Arrange — threshold=3, only trigger 2 failures
		DatabaseInitializationStatus status = CreateCompletedStatus();
		DatabaseConnectionInterceptor sut = CreateInterceptor(status, failureThreshold: 3);

		// Act
		sut.HandleConnectionFailure(new InvalidOperationException("connection refused"));
		sut.HandleConnectionFailure(new InvalidOperationException("connection refused"));

		// Assert
		Assert.Equal(DatabaseInitializationState.Completed, status.State);
		Assert.True(status.IsReady);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseConnectionInterceptor.HandleConnectionFailure"/> transitions the state to
	/// <see cref="DatabaseInitializationState.Disconnected"/> once the configured threshold is reached.
	/// </summary>
	[Fact]
	public void HandleConnectionFailure_WhenThresholdReached_SetsDisconnected()
	{
		// Arrange — threshold=3
		DatabaseInitializationStatus status = CreateCompletedStatus();
		DatabaseConnectionInterceptor sut = CreateInterceptor(status, failureThreshold: 3);

		// Act
		for (int i = 0; i < 3; i++)
		{
			sut.HandleConnectionFailure(new InvalidOperationException("connection refused"));
		}

		// Assert
		Assert.Equal(DatabaseInitializationState.Disconnected, status.State);
		Assert.False(status.IsReady);
	}

	/// <summary>
	/// Verifies the re-check guard: when another thread transitions the state to
	/// <see cref="DatabaseInitializationState.Disconnected"/> between the threshold check and the
	/// re-check guard, <see cref="DatabaseConnectionInterceptor.HandleConnectionFailure"/> returns early
	/// without calling <see cref="DatabaseInitializationStatus.SetDisconnected"/> again.
	/// Uses <see cref="ExecutionStageMonitor"/> to inject the state change at the exact point between
	/// threshold confirmation and re-check.
	/// </summary>
	[Fact]
	public void HandleConnectionFailure_WhenStateChangedDuringThresholdCheck_DoesNotSetDisconnectedTwice()
	{
		// Arrange — threshold=1 so the first failure reaches the threshold
		DatabaseInitializationStatus status = CreateCompletedStatus();
		DatabaseConnectionInterceptor sut = CreateInterceptor(status, failureThreshold: 1);

		// Inject state change at the exact point between threshold check and re-check guard
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.OnStage(
				"HandleConnectionFailure.BeforeRecheck",
				() =>
					status.SetDisconnected(new InvalidOperationException("other thread"), "Simulated race"));

		// Act — failure reaches threshold, stage fires before re-check, re-check guard catches it
		sut.HandleConnectionFailure(new InvalidOperationException("connection refused"));

		// Assert — state is Disconnected (set by the stage action, not by the interceptor)
		Assert.Equal(DatabaseInitializationState.Disconnected, status.State);
		Assert.Equal("Simulated race", status.FailureMessage);
	}

	/// <summary>
	/// Verifies the sliding-window behavior: failures that occurred before the window started are not counted
	/// toward the threshold. After the window expires, old failures are pruned and new failures must accumulate
	/// again to reach the threshold.
	/// </summary>
	[Fact]
	public void HandleConnectionFailure_WhenOldFailuresExpire_ThresholdNotReached()
	{
		// Arrange — threshold=3, window=30 seconds
		var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
		DatabaseInitializationStatus status = CreateCompletedStatus();
		DatabaseConnectionInterceptor sut = CreateInterceptor(
			status,
			failureThreshold: 3,
			failureWindowSeconds: 30,
			timeProvider: timeProvider);
		var exception = new InvalidOperationException("connection refused");

		// Act — record 2 failures, then advance past the window, then record 1 more
		sut.HandleConnectionFailure(exception);
		sut.HandleConnectionFailure(exception);

		// Advance time past the 30-second window so the first 2 failures expire
		timeProvider.Advance(TimeSpan.FromSeconds(31));

		sut.HandleConnectionFailure(exception);

		// Assert — only 1 failure in the current window, threshold (3) not reached
		Assert.Equal(DatabaseInitializationState.Completed, status.State);
		Assert.True(status.IsReady);
	}
}
