// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// A <see cref="DelegatingHandler"/> that tracks backend health based on HTTP request outcomes.
/// </summary>
/// <remarks>
///     <para>
///     This handler updates <see cref="BackendHealthState"/> after each request:
///     successful responses mark the backend as healthy, while network errors or 5xx responses
///     mark it as unhealthy.
///     </para>
///     <para>
///     The health state is observed by UI components like <c>BackendHealthIndicator</c> to provide
///     real-time feedback without polling.
///     </para>
/// </remarks>
public sealed class HealthTrackingHandler : DelegatingHandler
{
	private readonly BackendHealthState mHealthState;

	/// <summary>
	/// Initializes a new instance of the <see cref="HealthTrackingHandler"/> class.
	/// </summary>
	/// <param name="healthState">The shared health state to update based on request outcomes.</param>
	public HealthTrackingHandler(BackendHealthState healthState)
	{
		mHealthState = healthState;
	}

	/// <inheritdoc/>
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken  cancellationToken)
	{
		try
		{
			HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

			// Update health state based on response.
			if ((int)response.StatusCode >= 500)
			{
				// Server error — backend is having problems.
				mHealthState.MarkUnhealthy();
			}
			else
			{
				// Any non-5xx response means the backend is reachable and responding.
				// This includes 4xx errors which are client errors, not backend health issues.
				mHealthState.MarkHealthy();
			}

			return response;
		}
		catch (HttpRequestException)
		{
			// Network error — backend unreachable.
			mHealthState.MarkUnhealthy();
			throw;
		}
		catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Request was cancelled by caller — don't change health state.
			// We can't confirm anything about backend health.
			throw;
		}
		catch (TaskCanceledException)
		{
			// HttpClient.Timeout exceeded — treat as unhealthy.
			mHealthState.MarkUnhealthy();
			throw;
		}
	}
}
