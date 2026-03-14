// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

public sealed partial class DatabaseInitializationStatusTests
{
	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.SetDisconnected"/> transitions the complete observable
	/// state from <see cref="DatabaseInitializationState.Completed"/> to
	/// <see cref="DatabaseInitializationState.Disconnected"/>, including failure category, exception, and message.
	/// </summary>
	[Fact]
	public void SetDisconnected_FromCompleted_TransitionsToDisconnectedState()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();
		sut.SetCompleted();
		var exception = new InvalidOperationException("connection lost");
		const string message = "Database connection lost";

		// Act
		sut.SetDisconnected(exception, message);

		// Assert
		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.Disconnected,
			expectedIsReady: false,
			expectedCategory: DatabaseFailureCategory.Transient,
			expectedFailureCount: 0,
			expectedShouldRetry: true,
			expectedFailureException: exception,
			expectedFailureMessage: message);
	}

	/// <summary>
	/// Test data for <see cref="SetDisconnected_FromNonCompletedState_DoesNotChangeState"/>. Contains all states
	/// from which <see cref="DatabaseInitializationStatus.SetDisconnected"/> should be a no-op.
	/// </summary>
	public static TheoryData<string, DatabaseInitializationState> SetDisconnected_NonCompletedStates_TestData() => new()
	{
		// NotStarted — initialization has not begun yet
		{ "NotStarted", DatabaseInitializationState.NotStarted },

		// InProgress — initialization is currently running
		{ "InProgress", DatabaseInitializationState.InProgress },

		// Failed — requires re-initialization, not a disconnection
		{ "Failed", DatabaseInitializationState.Failed },

		// Disconnected — already disconnected, should not overwrite
		{ "Disconnected", DatabaseInitializationState.Disconnected }
	};

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.SetDisconnected"/> is a no-op when the current state
	/// is not <see cref="DatabaseInitializationState.Completed"/>. This prevents overwriting
	/// <see cref="DatabaseInitializationState.Failed"/> (which requires re-initialization) or other non-ready states.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="expectedState">The state the instance should remain in after the no-op call.</param>
	[Theory]
	[MemberData(nameof(SetDisconnected_NonCompletedStates_TestData))]
	public void SetDisconnected_FromNonCompletedState_DoesNotChangeState(
		string                      scenario,
		DatabaseInitializationState expectedState)
	{
		_ = scenario;

		// Arrange
		var sut = new DatabaseInitializationStatus();
		SetupNonCompletedState(sut, expectedState);

		// Capture state before the call
		DatabaseInitializationState stateBefore = sut.State;
		Exception? exceptionBefore = sut.FailureException;
		string? messageBefore = sut.FailureMessage;

		var newException = new InvalidOperationException("connection lost");

		// Act
		sut.SetDisconnected(newException, "Should be ignored");

		// Assert — state and failure info remain unchanged
		Assert.Equal(stateBefore, sut.State);
		Assert.Same(exceptionBefore, sut.FailureException);
		Assert.Equal(messageBefore, sut.FailureMessage);
	}

	/// <summary>
	/// Transitions a <see cref="DatabaseInitializationStatus"/> instance into the given
	/// <paramref name="targetState"/> for test setup purposes. Only supports non-Completed states.
	/// </summary>
	/// <param name="status">The status instance to configure.</param>
	/// <param name="targetState">The desired target state.</param>
	private static void SetupNonCompletedState(
		DatabaseInitializationStatus status,
		DatabaseInitializationState  targetState)
	{
		switch (targetState)
		{
			case DatabaseInitializationState.NotStarted:
				// Default state — nothing to do.
				break;

			case DatabaseInitializationState.InProgress:
				status.SetInProgress();
				break;

			case DatabaseInitializationState.Failed:
				status.SetFailed(
					new InvalidOperationException("setup failure"),
					"Setup failure",
					DatabaseFailureCategory.ConfigurationRequired);
				break;

			case DatabaseInitializationState.Disconnected:
				status.SetCompleted();
				status.SetDisconnected(
					new InvalidOperationException("setup disconnection"),
					"Setup disconnection");
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(targetState), targetState, "Unsupported target state.");
		}
	}
}
