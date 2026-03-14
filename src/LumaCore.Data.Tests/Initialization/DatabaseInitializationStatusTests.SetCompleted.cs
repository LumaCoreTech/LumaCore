// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

public sealed partial class DatabaseInitializationStatusTests
{
	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.SetCompleted"/> transitions the complete observable
	/// state to the completed state (including <see cref="DatabaseInitializationStatus.IsReady"/> returning
	/// <see langword="true"/>).
	/// </summary>
	[Fact]
	public void SetCompleted_WhenInProgress_TransitionsToCompletedState()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();
		sut.SetInProgress();

		// Act
		sut.SetCompleted();

		// Assert
		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.Completed,
			expectedIsReady: true,
			expectedCategory: null,
			expectedFailureCount: 0,
			expectedShouldRetry: false,
			expectedFailureException: null,
			expectedFailureMessage: null);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.SetCompleted"/> resets all failure-related state
	/// (category, exception, message, and consecutive failure count) after a previous failure.
	/// </summary>
	[Fact]
	public void SetCompleted_AfterFailure_ResetsAllFailureState()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();
		var exception = new InvalidOperationException("test failure");
		sut.SetFailed(exception, "Something failed", DatabaseFailureCategory.Transient);

		// Act
		sut.SetCompleted();

		// Assert
		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.Completed,
			expectedIsReady: true,
			expectedCategory: null,
			expectedFailureCount: 0,
			expectedShouldRetry: false,
			expectedFailureException: null,
			expectedFailureMessage: null);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.SetCompleted"/> resets all failure-related state
	/// even after multiple consecutive failures.
	/// </summary>
	[Fact]
	public void SetCompleted_AfterMultipleFailures_ResetsAllFailureState()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();
		var exception = new InvalidOperationException("test failure");
		sut.SetFailed(exception, "Failure 1", DatabaseFailureCategory.Transient);
		sut.SetFailed(exception, "Failure 2", DatabaseFailureCategory.Transient);

		// Act
		sut.SetCompleted();

		// Assert
		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.Completed,
			expectedIsReady: true,
			expectedCategory: null,
			expectedFailureCount: 0,
			expectedShouldRetry: false,
			expectedFailureException: null,
			expectedFailureMessage: null);
	}
}
