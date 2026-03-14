// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

/// <summary>
/// Unit tests for <see cref="DatabaseInitializationStatus"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify the thread-safe state machine that tracks database initialization and connection status.
///     </para>
///     <list type="bullet">
///         <item>
///         <c>DatabaseInitializationStatusTests.SetCompleted.cs</c> — Successful completion and failure state reset
///         </item>
///         <item>
///         <c>DatabaseInitializationStatusTests.SetFailed.cs</c> — Failure tracking, escalation at
///         <see cref="DatabaseInitializationStatus.MaxConsecutiveFailures"/>, and
///         <see cref="DatabaseInitializationStatus.ShouldRetry"/> logic
///         </item>
///         <item>
///         <c>DatabaseInitializationStatusTests.SetDisconnected.cs</c> — Runtime disconnection guard (only from
///         <see cref="DatabaseInitializationState.Completed"/>)
///         </item>
///         <item><c>DatabaseInitializationStatusTests.Helpers.cs</c> — Shared state verification helper</item>
///     </list>
/// </remarks>
[Trait("Category", "Initialization")]
public sealed partial class DatabaseInitializationStatusTests
{
	#region Constructor

	/// <summary>
	/// Verifies that a newly constructed <see cref="DatabaseInitializationStatus"/> instance has the expected
	/// initial state: <see cref="DatabaseInitializationState.NotStarted"/>, not ready, no failure information.
	/// </summary>
	[Fact]
	public void Constructor_Default_InitializesToNotStartedState()
	{
		// Arrange + Act
		var sut = new DatabaseInitializationStatus();

		// Assert
		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.NotStarted,
			expectedIsReady: false,
			expectedCategory: null,
			expectedFailureCount: 0,
			expectedShouldRetry: false,
			expectedFailureException: null,
			expectedFailureMessage: null);
	}

	#endregion

	#region SetInProgress()

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.SetInProgress"/> transitions the complete observable
	/// state to <see cref="DatabaseInitializationState.InProgress"/> without setting any failure information.
	/// </summary>
	[Fact]
	public void SetInProgress_WhenCalled_TransitionsToInProgressState()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();

		// Act
		sut.SetInProgress();

		// Assert
		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.InProgress,
			expectedIsReady: false,
			expectedCategory: null,
			expectedFailureCount: 0,
			expectedShouldRetry: false,
			expectedFailureException: null,
			expectedFailureMessage: null);
	}

	#endregion
}
