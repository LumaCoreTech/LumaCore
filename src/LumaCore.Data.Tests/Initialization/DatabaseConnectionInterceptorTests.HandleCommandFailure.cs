// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

public sealed partial class DatabaseConnectionInterceptorTests
{
	/// <summary>
	/// Verifies that <see cref="DatabaseConnectionInterceptor.HandleCommandFailure"/> is a no-op when the
	/// database state is not <see cref="DatabaseInitializationState.Completed"/>. The interceptor must not
	/// overwrite <see cref="DatabaseInitializationState.Failed"/> or other non-ready states.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="state">The current state of the database.</param>
	[Theory]
	[MemberData(nameof(NonCompletedStates_TestData))]
	public void HandleCommandFailure_WhenStateIsNotCompleted_DoesNotChangeState(
		string                      scenario,
		DatabaseInitializationState state)
	{
		_ = scenario;

		// Arrange
		DatabaseInitializationStatus status = CreateStatusInState(state);
		DatabaseConnectionInterceptor sut = CreateInterceptor(status);

		// Act
		sut.HandleCommandFailure(new InvalidOperationException("connection lost"));

		// Assert
		Assert.Equal(state, status.State);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseConnectionInterceptor.HandleCommandFailure"/> does not change state
	/// when the provider reports the exception as not connection-related (e.g., SQL syntax error, constraint
	/// violation).
	/// </summary>
	[Fact]
	public void HandleCommandFailure_WhenExceptionIsNotConnectionRelated_DoesNotChangeState()
	{
		// Arrange
		DatabaseInitializationStatus status = CreateCompletedStatus();
		var providerOps = new FakeProviderOperations(serviceUnavailable: false);
		DatabaseConnectionInterceptor sut = CreateInterceptor(status, providerOps);

		// Act — call multiple times to ensure threshold isn't reached through non-connection errors
		for (int i = 0; i < 10; i++)
		{
			sut.HandleCommandFailure(new InvalidOperationException("syntax error"));
		}

		// Assert
		Assert.Equal(DatabaseInitializationState.Completed, status.State);
		Assert.True(status.IsReady);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseConnectionInterceptor.HandleCommandFailure"/> does not change state
	/// when the failure count is below the configured threshold.
	/// </summary>
	[Fact]
	public void HandleCommandFailure_WhenBelowThreshold_StateRemainsCompleted()
	{
		// Arrange — threshold=3, only trigger 2 failures
		DatabaseInitializationStatus status = CreateCompletedStatus();
		DatabaseConnectionInterceptor sut = CreateInterceptor(status, failureThreshold: 3);

		// Act
		sut.HandleCommandFailure(new InvalidOperationException("transient error"));
		sut.HandleCommandFailure(new InvalidOperationException("transient error"));

		// Assert — still Completed, threshold not reached
		Assert.Equal(DatabaseInitializationState.Completed, status.State);
		Assert.True(status.IsReady);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseConnectionInterceptor.HandleCommandFailure"/> transitions the state to
	/// <see cref="DatabaseInitializationState.Disconnected"/> once the configured failure threshold is reached.
	/// </summary>
	[Fact]
	public void HandleCommandFailure_WhenThresholdReached_SetsDisconnected()
	{
		// Arrange — threshold=3, trigger exactly 3 failures
		DatabaseInitializationStatus status = CreateCompletedStatus();
		DatabaseConnectionInterceptor sut = CreateInterceptor(status, failureThreshold: 3);

		// Act
		for (int i = 0; i < 3; i++)
		{
			sut.HandleCommandFailure(new InvalidOperationException("connection lost"));
		}

		// Assert
		Assert.Equal(DatabaseInitializationState.Disconnected, status.State);
		Assert.False(status.IsReady);
	}

	/// <summary>
	/// Verifies the re-check guard: when another thread transitions the state to
	/// <see cref="DatabaseInitializationState.Disconnected"/> between the threshold check and the
	/// re-check guard, <see cref="DatabaseConnectionInterceptor.HandleCommandFailure"/> returns early
	/// without calling <see cref="DatabaseInitializationStatus.SetDisconnected"/> again.
	/// Uses <see cref="ExecutionStageMonitor"/> to inject the state change at the exact point between
	/// threshold confirmation and re-check.
	/// </summary>
	[Fact]
	public void HandleCommandFailure_WhenStateChangedDuringThresholdCheck_DoesNotSetDisconnectedTwice()
	{
		// Arrange — threshold=1 so the first failure reaches the threshold
		DatabaseInitializationStatus status = CreateCompletedStatus();
		DatabaseConnectionInterceptor sut = CreateInterceptor(status, failureThreshold: 1);

		// Inject state change at the exact point between threshold check and re-check guard
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.OnStage(
				"HandleCommandFailure.BeforeRecheck",
				() =>
					status.SetDisconnected(new InvalidOperationException("other thread"), "Simulated race"));

		// Act — failure reaches threshold, stage fires before re-check, re-check guard catches it
		sut.HandleCommandFailure(new InvalidOperationException("connection lost"));

		// Assert — state is Disconnected (set by the stage action, not by the interceptor)
		Assert.Equal(DatabaseInitializationState.Disconnected, status.State);
		Assert.Equal("Simulated race", status.FailureMessage);
	}
}
