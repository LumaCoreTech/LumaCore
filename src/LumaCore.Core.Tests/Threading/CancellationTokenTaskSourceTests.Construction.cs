// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

public partial class CancellationTokenTaskSourceTests
{
	/// <summary>
	/// Verifies that the constructor creates a task source with an incomplete task
	/// when the token is not canceled.
	/// </summary>
	[Fact]
	public void Constructor_WithNonCanceledToken_CreatesIncompleteTask()
	{
		// Arrange
		using var cts = new CancellationTokenSource();

		// Act
		using var source = new CancellationTokenTaskSource<int>(cts.Token);

		// Assert
		Assert.False(source.Task.IsCompleted);
	}

	/// <summary>
	/// Verifies that the constructor creates a task source with a pre-canceled task
	/// when the token is already canceled.
	/// </summary>
	[Fact]
	public void Constructor_WithAlreadyCanceledToken_CreatesCanceledTask()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act
		using var source = new CancellationTokenTaskSource<int>(cts.Token);

		// Assert
		Assert.True(source.Task.IsCanceled);
	}

	/// <summary>
	/// Verifies that the constructor creates an incomplete task
	/// when using <see cref="CancellationToken.None"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithNonCancelableToken_CreatesIncompleteTask()
	{
		// Arrange + Act
		using var source = new CancellationTokenTaskSource<string>(CancellationToken.None);

		// Assert
		Assert.False(source.Task.IsCompleted);
	}
}
