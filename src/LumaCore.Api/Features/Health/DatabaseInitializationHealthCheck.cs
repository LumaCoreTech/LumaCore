// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LumaCore.Api.Features.Health;

/// <summary>
/// A health check that reports the status of database availability.
/// </summary>
/// <remarks>
///     <para>
///     This health check queries the <see cref="DatabaseInitializationStatus"/> singleton to determine whether the
///     database is available. It is registered as part of the standard ASP.NET Core health check infrastructure and
///     contributes to the aggregated health status at <c>/health</c>.
///     </para>
///     <para>
///         <b>Status mapping:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="DatabaseInitializationState.Completed"/> → <see cref="HealthStatus.Healthy"/>
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="DatabaseInitializationState.InProgress"/> or
///             <see cref="DatabaseInitializationState.NotStarted"/>
///             → <see cref="HealthStatus.Degraded"/>
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="DatabaseInitializationState.Failed"/> or <see cref="DatabaseInitializationState.Disconnected"/>
///             → <see cref="HealthStatus.Unhealthy"/>
///             </description>
///         </item>
///     </list>
/// </remarks>
sealed class DatabaseInitializationHealthCheck : IHealthCheck
{
	private readonly DatabaseInitializationStatus mInitializationStatus;

	/// <summary>
	/// Initializes a new instance of the <see cref="DatabaseInitializationHealthCheck"/> class.
	/// </summary>
	/// <param name="initializationStatus">The database initialization status service.</param>
	public DatabaseInitializationHealthCheck(DatabaseInitializationStatus initializationStatus)
	{
		mInitializationStatus = initializationStatus;
	}

	/// <summary>
	/// Checks the database status and returns the corresponding health result.
	/// </summary>
	/// <param name="context">The health check context.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A <see cref="HealthCheckResult"/> representing the current database status.</returns>
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken  cancellationToken = default)
	{
		HealthCheckResult result = mInitializationStatus.State switch
		{
			DatabaseInitializationState.Completed =>
				HealthCheckResult.Healthy("Database is available."),

			DatabaseInitializationState.InProgress =>
				HealthCheckResult.Degraded("Database initialization is in progress."),

			DatabaseInitializationState.NotStarted =>
				HealthCheckResult.Degraded("Database initialization has not started yet."),

			DatabaseInitializationState.Failed =>
				HealthCheckResult.Unhealthy(
					mInitializationStatus.ShouldRetry
						? $"Database initialization failed (attempt {mInitializationStatus.ConsecutiveFailureCount}/{DatabaseInitializationStatus.MaxConsecutiveFailures}, retrying): {mInitializationStatus.FailureMessage}"
						: $"Database initialization failed (giving up after {mInitializationStatus.ConsecutiveFailureCount} attempts): {mInitializationStatus.FailureMessage}",
					mInitializationStatus.FailureException),

			DatabaseInitializationState.Disconnected =>
				HealthCheckResult.Unhealthy(
					$"Database connection lost: {mInitializationStatus.FailureMessage}",
					mInitializationStatus.FailureException),

			var _ => HealthCheckResult.Unhealthy("Unknown database state.")
		};

		return Task.FromResult(result);
	}
}
