// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Response containing identity information about the LumaCore instance.
/// </summary>
/// <remarks>
///     <para>
///     This response provides static runtime context to identify which LumaCore instance is being accessed. For
///     runtime metrics (memory, GC, thread pool), use the <c>/api/v1/system/metrics</c> endpoint instead.
///     </para>
///     <para>
///     <b>Typical use:</b> Verify deployment version, environment, and instance identity. Useful for correlating
///     logs and debugging multi-instance deployments.
///     </para>
/// </remarks>
/// <param name="Environment">
/// The current environment name (e.g., <c>Development</c>, <c>Production</c>).
/// </param>
/// <param name="Version">
/// The assembly version (e.g., <c>1.0.0.0</c>).
/// </param>
/// <param name="InformationalVersion">
/// The full version including prerelease tag and build metadata (e.g., <c>1.0.0-alpha+abc123de</c>).
/// </param>
/// <param name="MachineName">
/// The name of the machine running the LumaCore instance.
/// </param>
/// <param name="UtcNow">
/// The current UTC time on the server.
/// </param>
public sealed record SystemInfoResponse(
	string   Environment,
	string?  Version,
	string?  InformationalVersion,
	string   MachineName,
	DateTime UtcNow);
