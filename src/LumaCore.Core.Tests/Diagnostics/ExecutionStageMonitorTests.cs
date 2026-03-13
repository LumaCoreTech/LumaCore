// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

using Xunit;

namespace LumaCore.Core.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ExecutionStageMonitor"/>.
/// </summary>
/// <remarks>
///     <para>
///     Test files are organized by public API member:
///     </para>
///     <list type="bullet">
///         <item><c>ExecutionStageMonitorTests.CancelAt.cs</c> — <see cref="ExecutionStageMonitor.CancelAt"/> tests</item>
///         <item><c>ExecutionStageMonitorTests.ThrowAt.cs</c> — <see cref="ExecutionStageMonitor.ThrowAt"/> tests</item>
///         <item><c>ExecutionStageMonitorTests.OnStage.cs</c> — <see cref="ExecutionStageMonitor.OnStage"/> tests</item>
///     </list>
/// </remarks>
[Trait("Category", "Diagnostics")]
public sealed partial class ExecutionStageMonitorTests
{
	#region Configure()

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.Configure"/> returns a non-null instance.
	/// </summary>
	[Fact]
	public void Configure_WhenCalled_ReturnsNonNullInstance()
	{
		// Act
		using ExecutionStageMonitor monitor = ExecutionStageMonitor.Configure();

		// Assert
		Assert.NotNull(monitor);
	}

	#endregion

	#region ReportStage()

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ReportStage"/> does not throw when no monitor
	/// is active — the production-code hot path must be safe unconditionally.
	/// </summary>
	[Fact]
	public void ReportStage_WhenNoMonitorActive_DoesNotThrow()
	{
		// Act + Assert — no exception
		ExecutionStageMonitor.ReportStage("some.stage");
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ReportStage"/> does not execute any action when the
	/// reported stage does not match any configured stage.
	/// </summary>
	[Fact]
	public void ReportStage_WhenStageNotConfigured_DoesNotExecuteAction()
	{
		// Arrange
		bool executed = false;
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.OnStage("expected.stage", () => executed = true);

		// Act
		ExecutionStageMonitor.ReportStage("other.stage");

		// Assert
		Assert.False(executed);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.ReportStage"/> executes only the action for the
	/// matching stage when multiple stages are configured.
	/// </summary>
	[Fact]
	public void ReportStage_WhenMultipleStagesConfigured_ExecutesOnlyMatchingAction()
	{
		// Arrange
		bool stageAExecuted = false;
		bool stageBExecuted = false;
		using ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.OnStage("stage.a", () => stageAExecuted = true)
			.OnStage("stage.b", () => stageBExecuted = true);

		// Act
		ExecutionStageMonitor.ReportStage("stage.a");

		// Assert
		Assert.True(stageAExecuted);
		Assert.False(stageBExecuted);
	}

	#endregion

	#region Dispose()

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.Dispose"/> clears the ambient reference so that
	/// subsequent <see cref="ExecutionStageMonitor.ReportStage"/> calls become no-ops.
	/// </summary>
	[Fact]
	public void Dispose_WhenCalled_ClearsAmbientReference()
	{
		// Arrange
		bool executed = false;
		ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.OnStage("test.stage", () => executed = true);

		// Act
		monitor.Dispose();
		ExecutionStageMonitor.ReportStage("test.stage");

		// Assert
		Assert.False(executed);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.Dispose"/> disposes the
	/// <see cref="CancellationTokenSource"/> instances created by <see cref="ExecutionStageMonitor.CancelAt"/>.
	/// </summary>
	/// <remarks>
	/// Uses <see cref="CancellationToken.WaitHandle"/> as the disposal probe because
	/// <see cref="CancellationToken.Register(Action)"/> no longer throws
	/// <see cref="ObjectDisposedException"/> in .NET 7+ (it returns a default registration instead).
	/// <see cref="CancellationToken.WaitHandle"/> still calls <c>ThrowIfDisposed()</c> internally.
	/// </remarks>
	[Fact]
	public void Dispose_WhenCancelAtConfigured_DisposesTokenSources()
	{
		// Arrange
		ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.CancelAt("test.stage", out CancellationToken token);

		// Act
		monitor.Dispose();

		// Assert — accessing WaitHandle throws after the backing CTS is disposed.
		Assert.Throws<ObjectDisposedException>(() => _ = token.WaitHandle);
	}

	/// <summary>
	/// Verifies that <see cref="ExecutionStageMonitor.Dispose"/> can be called multiple times
	/// without throwing.
	/// </summary>
	[Fact]
	public void Dispose_WhenCalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		ExecutionStageMonitor monitor = ExecutionStageMonitor
			.Configure()
			.CancelAt("test.stage", out CancellationToken _);

		// Act + Assert
		monitor.Dispose();
		monitor.Dispose(); // Should not throw.
	}

	#endregion
}
