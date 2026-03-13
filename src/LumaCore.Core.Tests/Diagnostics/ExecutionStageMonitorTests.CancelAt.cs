// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

using Xunit;

namespace LumaCore.Core.Tests.Diagnostics;

public sealed partial class ExecutionStageMonitorTests
{
	#region CancelAt()

	/// <summary>
	/// Verifies that the <see langword="out"/> token from <see cref="ExecutionStageMonitor.CancelAt"/> is
	/// cancelled when the matching stage is reported.
	/// </summary>
	[Fact]
	public void CancelAt_WhenStageReported_CancelsToken()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.CancelAt("test.stage", out CancellationToken token);

		// Act
		ExecutionStageMonitor.ReportStage("test.stage");

		// Assert
		Assert.True(token.IsCancellationRequested);
	}

	/// <summary>
	/// Verifies that the <see langword="out"/> token from <see cref="ExecutionStageMonitor.CancelAt"/> is
	/// not cancelled before the matching stage is reported.
	/// </summary>
	[Fact]
	public void CancelAt_BeforeStageReported_TokenIsNotCancelled()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.CancelAt("test.stage", out CancellationToken token);

		// Assert — token is live but not yet cancelled
		Assert.False(token.IsCancellationRequested);
		Assert.True(token.CanBeCanceled);
	}

	/// <summary>
	/// Verifies that multiple <see cref="ExecutionStageMonitor.CancelAt"/> calls produce independent
	/// tokens — cancelling one does not affect the other.
	/// </summary>
	[Fact]
	public void CancelAt_WhenMultipleStages_ProducesIndependentTokens()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.CancelAt("stage.a", out CancellationToken tokenA)
			.CancelAt("stage.b", out CancellationToken tokenB);

		// Act — only fire stage A
		ExecutionStageMonitor.ReportStage("stage.a");

		// Assert
		Assert.True(tokenA.IsCancellationRequested);
		Assert.False(tokenB.IsCancellationRequested);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.CancelAt"/> throws
	/// <see cref="ArgumentNullException"/> when the stage name is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void CancelAt_WhenStageIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => monitor.CancelAt(null!, out CancellationToken _));
		Assert.Equal("stage", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.CancelAt"/> throws
	/// <see cref="ArgumentException"/> when the stage name is empty.
	/// </summary>
	[Fact]
	public void CancelAt_WhenStageIsEmpty_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => monitor.CancelAt("", out CancellationToken _));
		Assert.Equal("stage", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.CancelAt"/> throws
	/// <see cref="ArgumentException"/> when the stage name is whitespace.
	/// </summary>
	[Fact]
	public void CancelAt_WhenStageIsWhitespace_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => monitor.CancelAt("   ", out CancellationToken _));
		Assert.Equal("stage", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.CancelAt"/> throws
	/// <see cref="ArgumentException"/> when the same stage name is registered twice.
	/// </summary>
	[Fact]
	public void CancelAt_WhenDuplicateStage_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.CancelAt("test.stage", out CancellationToken _);

		// Act + Assert
		Assert.Throws<ArgumentException>(() => monitor.CancelAt("test.stage", out CancellationToken _));
	}

	#endregion
}
