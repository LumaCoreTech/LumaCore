// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// Tracks the observed health state of the backend API based on HTTP request outcomes.
/// </summary>
/// <remarks>
///     <para>
///     This service acts as a shared state container that is updated by <see cref="JwtAuthorizationHandler"/>
///     based on HTTP request success/failure, and observed by UI components like <c>BackendHealthIndicator</c>.
///     </para>
///     <para>
///     The service tracks three states:
///     </para>
///     <list type="bullet">
///         <item><b>Healthy</b> — The backend is reachable and operationally ready.</item>
///         <item><b>NotReady</b> — The backend is reachable but not operationally ready (e.g., database initializing).</item>
///         <item><b>Unhealthy</b> — The backend is not reachable.</item>
///     </list>
///     <para>
///     The state is updated passively based on actual API traffic (via <see cref="JwtAuthorizationHandler"/>)
///     and actively by health probe components. Subscribers can react to state changes via
///     <see cref="OnHealthChanged"/>.
///     </para>
/// </remarks>
public sealed class BackendHealthState
{
	/// <summary>
	/// Internal representation of the three possible backend states.
	/// </summary>
	private enum State
	{
		Healthy,
		NotReady,
		Unhealthy
	}

	/// <summary>
	/// The current backend health state.
	/// </summary>
	private State mState = State.Healthy;

	/// <summary>
	/// Gets a value indicating whether the backend is currently considered healthy (reachable and ready).
	/// </summary>
	/// <remarks>
	/// <see langword="true"/> after successful API requests; <see langword="false"/> when the backend is
	/// unreachable or reachable but not operationally ready.
	/// </remarks>
	public bool IsHealthy => mState == State.Healthy;

	/// <summary>
	/// Gets a value indicating whether the backend is reachable but not operationally ready.
	/// </summary>
	/// <remarks>
	/// <see langword="true"/> when the liveness probe succeeds but the readiness probe reports that the
	/// backend cannot yet serve requests (e.g., database initialization in progress or failed).
	/// </remarks>
	public bool IsNotReady => mState == State.NotReady;

	/// <summary>
	/// Gets the timestamp of the last health state update.
	/// </summary>
	public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;

	/// <summary>
	/// Marks the backend as healthy (reachable and operationally ready).
	/// </summary>
	/// <remarks>
	/// Only fires <see cref="OnHealthChanged"/> if the state actually changed.
	/// </remarks>
	public void MarkHealthy()
	{
		LastUpdated = DateTime.UtcNow;

		if (mState == State.Healthy)
			return;

		mState = State.Healthy;
		OnHealthChanged?.Invoke();
	}

	/// <summary>
	/// Marks the backend as reachable but not operationally ready.
	/// </summary>
	/// <remarks>
	/// Only fires <see cref="OnHealthChanged"/> if the state actually changed. This state indicates that
	/// the liveness probe succeeded but the readiness probe reported that the backend cannot yet serve
	/// requests (e.g., database initialization in progress or failed).
	/// </remarks>
	public void MarkNotReady()
	{
		LastUpdated = DateTime.UtcNow;

		if (mState == State.NotReady)
			return;

		mState = State.NotReady;
		OnHealthChanged?.Invoke();
	}

	/// <summary>
	/// Marks the backend as unhealthy (not reachable).
	/// </summary>
	/// <remarks>
	/// Only fires <see cref="OnHealthChanged"/> if the state actually changed.
	/// </remarks>
	public void MarkUnhealthy()
	{
		LastUpdated = DateTime.UtcNow;

		if (mState == State.Unhealthy)
			return;

		mState = State.Unhealthy;
		OnHealthChanged?.Invoke();
	}

	/// <summary>
	/// Fired when the health state changes. Subscribers should call <c>StateHasChanged()</c> to re-render.
	/// </summary>
	public event Action? OnHealthChanged;
}
