// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// An ambient monitor that executes a pluggable action when a named execution stage is reported,
/// enabling deterministic fault injection, cancellation testing, and diagnostics without modifying
/// method signatures.
/// </summary>
/// <remarks>
///     <para>
///     This class uses <see cref="AsyncLocal{T}"/> to propagate the monitor through asynchronous control
///     flow — the same mechanism used by <see cref="Activity.Current"/>.
///     </para>
///     <para>
///     <b>Production overhead:</b> When no monitor is active (the common case), <see cref="ReportStage"/>
///     performs a single <see cref="AsyncLocal{T}"/> read and a <see langword="null"/> check — effectively
///     zero cost.
///     </para>
///     <para>
///     <b>Usage in production code</b> (1-liner per checkpoint):
///     <code>ExecutionStageMonitor.ReportStage("MyMethod.BeforeQuery");</code>
///     </para>
///     <para>
///     <b>Usage in tests</b> — cancel at a specific stage:
///     <code>
///     using var monitor = ExecutionStageMonitor
///         .Configure()
///         .CancelAt("MyMethod.BeforeQuery", out var token);
///     await Assert.ThrowsAsync&lt;OperationCanceledException&gt;(() =&gt; sut.RunAsync(token));
///     </code>
///     </para>
///     <para>
///     <b>Usage in tests</b> — inject a fault to exercise error-handling paths:
///     <code>
///     using var monitor = ExecutionStageMonitor
///         .Configure()
///         .ThrowAt("MyMethod.BeforeQuery", new IOException("disk failure"));
///     var result = await sut.RunAsync();
///     Assert.True(result.UsedFallback); // graceful degradation
///     </code>
///     </para>
///     <para>
///     <b>Usage in tests</b> — multiple stages with independent cancellation tokens:
///     <code>
///     using var monitor = ExecutionStageMonitor
///         .Configure()
///         .CancelAt("Pipeline.StageA", out var tokenA)
///         .CancelAt("Pipeline.StageB", out var tokenB)
///         .OnStage("Pipeline.StageC", () =&gt; counter++);
///     </code>
///     </para>
///     <para>
///     <b>Nesting is not supported.</b> Calling <see cref="Configure"/> while another monitor is
///     already active in the same async flow throws <see cref="InvalidOperationException"/>. This is
///     a deliberate design decision: nested monitors raise non-trivial questions about precedence,
///     order of side-effects, and visibility, and the only legitimate use case (a single fault-injection
///     scope per test) does not require it. A surfaced nesting exception almost always indicates a
///     leaked monitor from a previous test or helper that forgot to dispose.
///     </para>
/// </remarks>
public sealed class ExecutionStageMonitor : IDisposable
{
	/// <summary>
	/// Ambient storage for the currently active monitor instance. Uses <see cref="AsyncLocal{T}"/>
	/// to flow through asynchronous continuations without explicit parameter passing.
	/// </summary>
	private static readonly AsyncLocal<ExecutionStageMonitor?> sCurrent = new();

	/// <summary>
	/// Maps stage names to the actions that should be executed when the stage is reported.
	/// Uses <see cref="StringComparer.Ordinal"/> for fast, case-sensitive lookups.
	/// </summary>
	private readonly Dictionary<string, Action> mStageActions = new(StringComparer.Ordinal);

	/// <summary>
	/// Token sources created by <see cref="CancelAt"/> calls. Each <see cref="CancelAt"/> creates its
	/// own <see cref="CancellationTokenSource"/> whose token is exposed to the caller via an
	/// <see langword="out"/> parameter. All sources are disposed in <see cref="Dispose"/>.
	/// </summary>
	private readonly List<CancellationTokenSource> mOwnedTokenSources = [];

	/// <summary>
	/// Initializes an empty monitor and sets itself as the ambient instance for the current async flow.
	/// </summary>
	/// <remarks>
	/// Callers must reach this constructor exclusively through <see cref="Configure"/>, which performs
	/// the nesting check. Direct instantiation is prevented by the <see langword="private"/> accessor.
	/// </remarks>
	private ExecutionStageMonitor()
	{
		sCurrent.Value = this;
	}

	/// <summary>
	/// Creates a new <see cref="ExecutionStageMonitor"/> and sets it as the ambient instance.
	/// Chain <see cref="CancelAt"/>, <see cref="ThrowAt"/>, or <see cref="OnStage"/> to configure
	/// the stages to monitor.
	/// </summary>
	/// <returns>A disposable monitor ready for fluent stage configuration.</returns>
	/// <exception cref="InvalidOperationException">
	/// Another <see cref="ExecutionStageMonitor"/> is already active in the current async flow.
	/// Nesting is not supported — dispose the existing instance before configuring a new one.
	/// </exception>
	public static ExecutionStageMonitor Configure()
	{
		if (sCurrent.Value is not null)
		{
			throw new InvalidOperationException(
				"An ExecutionStageMonitor is already active in the current async flow. " +
				"Nested monitors are not supported — dispose the existing instance before configuring a new one. " +
				"If this surfaces in tests, it usually indicates a leaked monitor from a previous test or " +
				"helper that forgot to dispose.");
		}

		return new ExecutionStageMonitor();
	}

	/// <summary>
	/// Reports a named execution stage. If an ambient monitor is listening for this stage, its action
	/// is executed. When no monitor is active, this is a no-op with near-zero overhead.
	/// </summary>
	/// <param name="stage">The stage name to report.</param>
	public static void ReportStage(string stage)
	{
		sCurrent.Value?.OnStageReported(stage);
	}

	/// <summary>
	/// Registers a stage that cancels a dedicated <see cref="CancellationToken"/> when reported.
	/// Each call creates its own <see cref="CancellationTokenSource"/> — multiple <see cref="CancelAt"/>
	/// stages produce independent tokens.
	/// </summary>
	/// <param name="stage">The stage name that triggers cancellation.</param>
	/// <param name="token">Receives the <see cref="CancellationToken"/> that will be cancelled when this stage fires.</param>
	/// <returns>This monitor instance for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	///     <para><paramref name="stage"/> is empty or consists only of white-space characters.</para>
	///     <para>- or -</para>
	///     <para>A stage with the same name has already been configured.</para>
	/// </exception>
	/// <exception cref="ArgumentNullException"><paramref name="stage"/> is <see langword="null"/>.</exception>
	public ExecutionStageMonitor CancelAt(string stage, out CancellationToken token)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stage);
		EnsureStageNotConfigured(stage);

		var cts = new CancellationTokenSource();
		mOwnedTokenSources.Add(cts);
		mStageActions.Add(stage, cts.Cancel);
		token = cts.Token;
		return this;
	}

	/// <summary>
	/// Registers a stage that throws the specified <paramref name="exception"/> when reported.
	/// This enables deterministic testing of error-handling paths (e.g., catch blocks for
	/// non-cancellation exceptions).
	/// </summary>
	/// <param name="stage">The stage name that triggers the throw.</param>
	/// <param name="exception">The exception instance to throw.</param>
	/// <returns>This monitor instance for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	///     <para><paramref name="stage"/> is empty or consists only of white-space characters.</para>
	///     <para>- or -</para>
	///     <para>A stage with the same name has already been configured.</para>
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="stage"/> or <paramref name="exception"/> is <see langword="null"/>.
	/// </exception>
	public ExecutionStageMonitor ThrowAt(string stage, Exception exception)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stage);
		ArgumentNullException.ThrowIfNull(exception);
		EnsureStageNotConfigured(stage);

		mStageActions.Add(stage, () => throw exception);
		return this;
	}

	/// <summary>
	/// Registers a stage that executes the specified <paramref name="action"/> when reported.
	/// </summary>
	/// <param name="stage">The stage name that triggers the action.</param>
	/// <param name="action">The action to execute when the stage is reported.</param>
	/// <returns>This monitor instance for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	///     <para><paramref name="stage"/> is empty or consists only of white-space characters.</para>
	///     <para>- or -</para>
	///     <para>A stage with the same name has already been configured.</para>
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="stage"/> or <paramref name="action"/> is <see langword="null"/>.
	/// </exception>
	public ExecutionStageMonitor OnStage(string stage, Action action)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stage);
		ArgumentNullException.ThrowIfNull(action);
		EnsureStageNotConfigured(stage);

		mStageActions.Add(stage, action);
		return this;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Clears the ambient slot for the current async flow if this instance is currently active.
	/// Idempotent: a second call is a no-op for the ambient slot but still runs the (already-disposed)
	/// <see cref="CancellationTokenSource"/> dispose loop, which is itself idempotent.
	/// </remarks>
	public void Dispose()
	{
		if (sCurrent.Value == this)
			sCurrent.Value = null;

		foreach (CancellationTokenSource cts in mOwnedTokenSources)
		{
			cts.Dispose();
		}
	}

	/// <summary>
	/// Looks up the reported <paramref name="stage"/> in the configured stage actions and executes
	/// the matching action if found. No-op for unconfigured stages.
	/// </summary>
	/// <param name="stage">The stage name to look up.</param>
	private void OnStageReported(string stage)
	{
		if (mStageActions.TryGetValue(stage, out Action? action))
			action();
	}

	/// <summary>
	/// Throws an <see cref="ArgumentException"/> with <see cref="ArgumentException.ParamName"/> set to
	/// <c>"stage"</c> when <paramref name="stage"/> is already registered. Centralises the duplicate
	/// check so that <see cref="CancelAt"/>, <see cref="ThrowAt"/> and <see cref="OnStage"/> share an
	/// identical, API-owned exception (instead of leaking the BCL <c>Dictionary.Add</c> message which
	/// would expose <c>ParamName="key"</c>).
	/// </summary>
	/// <param name="stage">The stage name to check; assumed to be non-null and non-whitespace.</param>
	/// <exception cref="ArgumentException">
	/// A stage with the same name has already been configured on this monitor.
	/// </exception>
	private void EnsureStageNotConfigured(string stage)
	{
		if (mStageActions.ContainsKey(stage))
		{
			throw new ArgumentException(
				$"Stage '{stage}' is already configured. Each stage name may only be registered once per monitor.",
				nameof(stage));
		}
	}
}
