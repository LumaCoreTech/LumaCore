// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

using Xunit;

namespace LumaCore.Core.Tests.Diagnostics;

public sealed partial class ExecutionStageMonitorTests
{
	#region OnStage()

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.OnStage"/> executes the configured action
	/// when the matching stage is reported via <see cref="ExecutionStageMonitor.ReportStage"/>.
	/// </summary>
	[Fact]
	public void OnStage_WhenStageReported_ExecutesAction()
	{
		// Arrange
		int callCount = 0;
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.OnStage("test.stage", () => callCount++);

		// Act
		ExecutionStageMonitor.ReportStage("test.stage");

		// Assert
		Assert.Equal(1, callCount);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.OnStage"/> throws
	/// <see cref="ArgumentNullException"/> when the stage name is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void OnStage_WhenStageIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => monitor.OnStage(null!, () => { }));
		Assert.Equal("stage", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.OnStage"/> throws
	/// <see cref="ArgumentException"/> when the stage name is empty.
	/// </summary>
	[Fact]
	public void OnStage_WhenStageIsEmpty_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => monitor.OnStage("", () => { }));
		Assert.Equal("stage", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.OnStage"/> throws
	/// <see cref="ArgumentNullException"/> when the action is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void OnStage_WhenActionIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => monitor.OnStage("test.stage", null!));
		Assert.Equal("action", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.OnStage"/> throws
	/// <see cref="ArgumentException"/> when the same stage name is registered twice.
	/// </summary>
	[Fact]
	public void OnStage_WhenDuplicateStage_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.OnStage("test.stage", () => { });

		// Act + Assert
		Assert.Throws<ArgumentException>(() => monitor.OnStage("test.stage", () => { }));
	}

	#endregion
}
