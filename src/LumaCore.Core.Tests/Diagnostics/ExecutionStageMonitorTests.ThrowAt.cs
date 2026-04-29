// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

using Xunit;

namespace LumaCore.Core.Tests.Diagnostics;

public sealed partial class ExecutionStageMonitorTests
{
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

		// Act
		var ex = Assert.Throws<InvalidOperationException>(() => ExecutionStageMonitor.ReportStage("test.stage"));

		// Assert
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

		// Act
		var ex = Assert.Throws<ArgumentNullException>(() => monitor.ThrowAt(null!, new InvalidOperationException()));

		// Assert
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

		// Act
		var ex = Assert.Throws<ArgumentException>(() => monitor.ThrowAt("", new InvalidOperationException()));

		// Assert
		Assert.Equal("stage", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ThrowAt"/> throws
	/// <see cref="ArgumentException"/> when the stage name is whitespace.
	/// </summary>
	[Fact]
	public void ThrowAt_WhenStageIsWhitespace_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Act
		var ex = Assert.Throws<ArgumentException>(() => monitor.ThrowAt("   ", new InvalidOperationException()));

		// Assert
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

		// Act
		var ex = Assert.Throws<ArgumentNullException>(() => monitor.ThrowAt("test.stage", null!));

		// Assert
		Assert.Equal("exception", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ThrowAt"/> throws an
	/// <see cref="ArgumentException"/> with <c>ParamName="stage"</c> and an API-owned message
	/// when the same stage name is registered twice.
	/// </summary>
	[Fact]
	public void ThrowAt_WhenDuplicateStage_ThrowsArgumentException()
	{
		// Arrange
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.ThrowAt("test.stage", new InvalidOperationException());

		// Act
		var ex = Assert.Throws<ArgumentException>(() => monitor.ThrowAt("test.stage", new InvalidOperationException()));

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
