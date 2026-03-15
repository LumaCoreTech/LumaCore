// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.Health;

/// <summary>
/// Readiness indicator for the backend, reporting whether the service is ready to handle requests.
/// </summary>
/// <remarks>
///     <para>
///     Unlike <see cref="ApiHealthLiveResponse"/> (which is a pure connectivity check), this response
///     reflects the actual operational readiness of the backend — including per-subsystem health status.
///     </para>
///     <para>
///         <b>Top-level status values:</b>
///     </para>
///     <list type="bullet">
///         <item><c>"ready"</c> — All subsystems are fully operational.</item>
///         <item><c>"degraded"</c> — One or more subsystems are not ready (see <see cref="Components"/>).</item>
///     </list>
///     <para>
///     The <see cref="Components"/> dictionary provides per-subsystem detail so that operators and the UI
///     can display exactly <b>which</b> subsystem is healthy or degraded without inspecting server logs.
///     </para>
/// </remarks>
/// <param name="Status">
/// Aggregate readiness state: <c>"ready"</c> when all components are operational, <c>"degraded"</c> otherwise.
/// </param>
/// <param name="Components">
/// Per-subsystem health detail, keyed by subsystem name (e.g., <c>"database"</c>). Each value contains the
/// subsystem's individual <see cref="ApiHealthComponentStatus.Status"/> and an optional
/// <see cref="ApiHealthComponentStatus.Message"/>.
/// </param>
public sealed record ApiHealthReadyResponse(
	string                                       Status,
	Dictionary<string, ApiHealthComponentStatus> Components);
