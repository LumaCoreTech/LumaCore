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
///     The state is updated passively based on actual API traffic — no polling required.
///     Components can subscribe to <see cref="OnHealthChanged"/> to react to state changes.
///     </para>
/// </remarks>
public sealed class BackendHealthState
{
	/// <summary>
	/// Gets a value indicating whether the backend is currently considered healthy.
	/// </summary>
	/// <remarks>
	/// <see langword="true"/> after successful API requests; <see langword="false"/> after network errors
	/// or server errors (5xx responses).
	/// </remarks>
	public bool IsHealthy { get; private set; } = true;

	/// <summary>
	/// Gets the timestamp of the last health state update.
	/// </summary>
	public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;

	/// <summary>
	/// Marks the backend as healthy after a successful API request.
	/// </summary>
	/// <remarks>
	/// Only fires <see cref="OnHealthChanged"/> if the state actually changed from unhealthy to healthy.
	/// </remarks>
	public void MarkHealthy()
	{
		LastUpdated = DateTime.UtcNow;

		if (IsHealthy)
			return;

		IsHealthy = true;
		OnHealthChanged?.Invoke();
	}

	/// <summary>
	/// Marks the backend as unhealthy after a failed API request.
	/// </summary>
	/// <remarks>
	/// Only fires <see cref="OnHealthChanged"/> if the state actually changed from healthy to unhealthy.
	/// </remarks>
	public void MarkUnhealthy()
	{
		LastUpdated = DateTime.UtcNow;

		if (!IsHealthy)
			return;

		IsHealthy = false;
		OnHealthChanged?.Invoke();
	}

	/// <summary>
	/// Fired when the health state changes. Subscribers should call <c>StateHasChanged()</c> to re-render.
	/// </summary>
	public event Action? OnHealthChanged;
}
