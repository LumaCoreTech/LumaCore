// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

public sealed partial class DatabaseInitializationStatusTests
{
	/// <summary>
	/// Asserts the complete observable state of a <see cref="DatabaseInitializationStatus"/> instance.
	/// </summary>
	/// <param name="status">The status instance to verify.</param>
	/// <param name="expectedState">The expected <see cref="DatabaseInitializationStatus.State"/> value.</param>
	/// <param name="expectedIsReady">The expected <see cref="DatabaseInitializationStatus.IsReady"/> value.</param>
	/// <param name="expectedCategory">
	/// The expected <see cref="DatabaseInitializationStatus.FailureCategory"/> value, or <see langword="null"/>.
	/// </param>
	/// <param name="expectedFailureCount">
	/// The expected <see cref="DatabaseInitializationStatus.ConsecutiveFailureCount"/> value.
	/// </param>
	/// <param name="expectedShouldRetry">The expected <see cref="DatabaseInitializationStatus.ShouldRetry"/> value.</param>
	/// <param name="expectedFailureException">
	/// The expected <see cref="DatabaseInitializationStatus.FailureException"/> instance, or <see langword="null"/>.
	/// </param>
	/// <param name="expectedFailureMessage">
	/// The expected <see cref="DatabaseInitializationStatus.FailureMessage"/> value, or <see langword="null"/>.
	/// </param>
	private static void AssertStatusState(
		DatabaseInitializationStatus status,
		DatabaseInitializationState  expectedState,
		bool                         expectedIsReady,
		DatabaseFailureCategory?     expectedCategory,
		int                          expectedFailureCount,
		bool                         expectedShouldRetry,
		Exception?                   expectedFailureException,
		string?                      expectedFailureMessage)
	{
		Assert.Equal(expectedState, status.State);
		Assert.Equal(expectedIsReady, status.IsReady);
		Assert.Equal(expectedCategory, status.FailureCategory);
		Assert.Equal(expectedFailureCount, status.ConsecutiveFailureCount);
		Assert.Equal(expectedShouldRetry, status.ShouldRetry);
		Assert.Same(expectedFailureException, status.FailureException);
		Assert.Equal(expectedFailureMessage, status.FailureMessage);
	}
}
