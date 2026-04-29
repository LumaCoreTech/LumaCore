// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

using Xunit;

namespace LumaCore.Core.Tests.Diagnostics;

public sealed partial class ExecutionStageMonitorTests
{
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

		// Act
		var ex = Assert.Throws<ArgumentNullException>(() => monitor.OnStage(null!, () => { }));

		// Assert
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

		// Act
		var ex = Assert.Throws<ArgumentException>(() => monitor.OnStage("", () => { }));

		// Assert
		Assert.Equal("stage", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.OnStage"/> throws
	/// <see cref="ArgumentException"/> when the stage name is whitespace.
	/// </summary>
	[Fact]
	public void OnStage_WhenStageIsWhitespace_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act
		var ex = Assert.Throws<ArgumentException>(() => monitor.OnStage("   ", () => { }));

		// Assert
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

		// Act
		var ex = Assert.Throws<ArgumentNullException>(() => monitor.OnStage("test.stage", null!));

		// Assert
		Assert.Equal("action", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.OnStage"/> throws an
	/// <see cref="ArgumentException"/> with <c>ParamName="stage"</c> and an API-owned message
	/// when the same stage name is registered twice.
	/// </summary>
	[Fact]
	public void OnStage_WhenDuplicateStage_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.OnStage("test.stage", () => { });

		// Act
		var ex = Assert.Throws<ArgumentException>(() => monitor.OnStage("test.stage", () => { }));

		// Assert
		Assert.Equal("stage", ex.ParamName);
		// Assert only the production-controlled message body. ArgumentException.Message appends a
		// "(Parameter '...')" suffix from a localized BCL resource, which differs on non-English systems.
		Assert.StartsWith(
			"Stage 'test.stage' is already configured. Each stage name may only be registered once per monitor.",
			ex.Message,
			StringComparison.Ordinal);
	}
}
