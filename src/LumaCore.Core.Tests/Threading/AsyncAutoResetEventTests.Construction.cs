// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

public partial class AsyncAutoResetEventTests
{
	/// <summary>
	/// Verifies that the default <see cref="AsyncAutoResetEvent()"/> constructor creates an event in the
	/// non-signaled (reset) state.
	/// </summary>
	[Fact]
	public void Constructor_WithNoArguments_CreatesEventInResetState()
	{
		// Arrange + Act
		var are = new AsyncAutoResetEvent();

		// Assert
		Assert.False(are.IsSet);
	}

	/// <summary>
	/// Verifies that the <see cref="AsyncAutoResetEvent(bool)"/> constructor creates an event in the correct
	/// initial state based on the parameter.
	/// </summary>
	/// <param name="initialState">The initial state to pass to the constructor.</param>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Constructor_WithInitialState_CreatesEventInExpectedState(bool initialState)
	{
		// Arrange + Act
		var are = new AsyncAutoResetEvent(initialState);

		// Assert
		Assert.Equal(initialState, are.IsSet);
	}
}
