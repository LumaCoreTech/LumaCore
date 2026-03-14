// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

public sealed partial class DatabaseInitializationStatusTests
{
	/// <summary>
	/// Verifies that a single <see cref="DatabaseInitializationStatus.SetFailed"/> call with
	/// <see cref="DatabaseFailureCategory.Transient"/> transitions the complete observable state to the failed state
	/// (including category, exception, message, and <see cref="DatabaseInitializationStatus.ShouldRetry"/>).
	/// </summary>
	[Fact]
	public void SetFailed_WithTransient_BelowThreshold_TransitionsToFailedState()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();
		var exception = new InvalidOperationException("connection timeout");
		const string message = "Database unreachable";

		// Act
		sut.SetFailed(exception, message, DatabaseFailureCategory.Transient);

		// Assert
		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.Failed,
			expectedIsReady: false,
			expectedCategory: DatabaseFailureCategory.Transient,
			expectedFailureCount: 1,
			expectedShouldRetry: true,
			expectedFailureException: exception,
			expectedFailureMessage: message);
	}

	/// <summary>
	/// Verifies that each call to <see cref="DatabaseInitializationStatus.SetFailed"/> increments
	/// <see cref="DatabaseInitializationStatus.ConsecutiveFailureCount"/> by one.
	/// </summary>
	[Fact]
	public void SetFailed_WhenCalled_IncrementsConsecutiveFailureCount()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();
		var exception = new InvalidOperationException("test");

		// Act
		sut.SetFailed(exception, "Failure 1", DatabaseFailureCategory.Transient);
		int countAfterFirst = sut.ConsecutiveFailureCount;
		sut.SetFailed(exception, "Failure 2", DatabaseFailureCategory.Transient);
		int countAfterSecond = sut.ConsecutiveFailureCount;

		// Assert
		Assert.Equal(1, countAfterFirst);
		Assert.Equal(2, countAfterSecond);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.SetFailed"/> with
	/// <see cref="DatabaseFailureCategory.ConfigurationRequired"/> transitions the complete observable state to the
	/// failed state without escalation (including <see cref="DatabaseInitializationStatus.ShouldRetry"/> returning
	/// <see langword="false"/>).
	/// </summary>
	[Fact]
	public void SetFailed_WithConfigurationRequired_TransitionsToFailedState()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();
		var exception = new InvalidOperationException("test");
		const string message = "Config needed";

		// Act
		sut.SetFailed(exception, message, DatabaseFailureCategory.ConfigurationRequired);

		// Assert
		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.Failed,
			expectedIsReady: false,
			expectedCategory: DatabaseFailureCategory.ConfigurationRequired,
			expectedFailureCount: 1,
			expectedShouldRetry: false,
			expectedFailureException: exception,
			expectedFailureMessage: message);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.SetFailed"/> auto-escalates the category to
	/// <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> after exactly
	/// <see cref="DatabaseInitializationStatus.MaxConsecutiveFailures"/> consecutive transient failures, including
	/// the augmented escalation message and <see cref="DatabaseInitializationStatus.ShouldRetry"/> returning
	/// <see langword="false"/>.
	/// </summary>
	[Fact]
	public void SetFailed_WithTransient_AtThreshold_EscalatesToManualIntervention()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();
		var exception = new InvalidOperationException("test");
		const string originalMessage = "Transient failure";

		// Act — fail exactly MaxConsecutiveFailures times
		for (int i = 0; i < DatabaseInitializationStatus.MaxConsecutiveFailures; i++)
		{
			sut.SetFailed(exception, originalMessage, DatabaseFailureCategory.Transient);
		}

		// Assert
		string expectedMessage =
			$"{originalMessage} (Failed {DatabaseInitializationStatus.MaxConsecutiveFailures} times consecutively. " +
			"Automatic recovery has been disabled.)";

		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.Failed,
			expectedIsReady: false,
			expectedCategory: DatabaseFailureCategory.ManualInterventionRequired,
			expectedFailureCount: DatabaseInitializationStatus.MaxConsecutiveFailures,
			expectedShouldRetry: false,
			expectedFailureException: exception,
			expectedFailureMessage: expectedMessage);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.SetFailed"/> does <b>not</b> escalate
	/// non-transient categories even when the consecutive failure count exceeds
	/// <see cref="DatabaseInitializationStatus.MaxConsecutiveFailures"/>.
	/// </summary>
	[Fact]
	public void SetFailed_WithNonTransient_AboveThreshold_DoesNotEscalate()
	{
		// Arrange
		var sut = new DatabaseInitializationStatus();
		var exception = new InvalidOperationException("test");
		const string message = "Config needed";
		int failureCount = DatabaseInitializationStatus.MaxConsecutiveFailures + 1;

		// Act — exceed the threshold with ConfigurationRequired
		for (int i = 0; i < failureCount; i++)
		{
			sut.SetFailed(exception, message, DatabaseFailureCategory.ConfigurationRequired);
		}

		// Assert — category is still ConfigurationRequired, not escalated
		AssertStatusState(
			sut,
			expectedState: DatabaseInitializationState.Failed,
			expectedIsReady: false,
			expectedCategory: DatabaseFailureCategory.ConfigurationRequired,
			expectedFailureCount: failureCount,
			expectedShouldRetry: false,
			expectedFailureException: exception,
			expectedFailureMessage: message);
	}
}
