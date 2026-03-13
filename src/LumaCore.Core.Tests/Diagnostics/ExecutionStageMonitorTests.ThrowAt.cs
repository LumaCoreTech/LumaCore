// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

using Xunit;

namespace LumaCore.Core.Tests.Diagnostics;

public sealed partial class ExecutionStageMonitorTests
{
	#region ThrowAt()

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ThrowAt"/> throws the configured exception
	/// when the matching stage is reported via <see cref="ExecutionStageMonitor.ReportStage"/>.
	/// </summary>
	[Fact]
	public void ThrowAt_WhenStageReported_ThrowsConfiguredException()
	{
		// Arrange
		var expected = new InvalidOperationException("test fault");
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.ThrowAt("test.stage", expected);

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => ExecutionStageMonitor.ReportStage("test.stage"));
		Assert.Same(expected, ex);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ThrowAt"/> throws
	/// <see cref="ArgumentNullException"/> when the stage name is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void ThrowAt_WhenStageIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => monitor.ThrowAt(null!, new InvalidOperationException()));
		Assert.Equal("stage", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ThrowAt"/> throws
	/// <see cref="ArgumentException"/> when the stage name is empty.
	/// </summary>
	[Fact]
	public void ThrowAt_WhenStageIsEmpty_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => monitor.ThrowAt("", new InvalidOperationException()));
		Assert.Equal("stage", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ThrowAt"/> throws
	/// <see cref="ArgumentNullException"/> when the exception is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void ThrowAt_WhenExceptionIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => monitor.ThrowAt("test.stage", null!));
		Assert.Equal("exception", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ThrowAt"/> throws
	/// <see cref="ArgumentException"/> when the same stage name is registered twice.
	/// </summary>
	[Fact]
	public void ThrowAt_WhenDuplicateStage_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.ThrowAt("test.stage", new InvalidOperationException());

		// Act + Assert
		Assert.Throws<ArgumentException>(() => monitor.ThrowAt("test.stage", new InvalidOperationException()));
	}

	#endregion
}
