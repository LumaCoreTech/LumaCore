// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.Health;

/// <summary>
/// Health status of a single backend subsystem (e.g., database, vector store, LLM backend).
/// </summary>
/// <remarks>
///     <para>
///     Each entry in <see cref="ApiHealthReadyResponse.Components"/> is an instance of this record,
///     keyed by the subsystem name. The <see cref="Status"/> values mirror the top-level
///     <see cref="ApiHealthReadyResponse.Status"/> vocabulary where applicable.
///     </para>
///     <para>
///         <b>Status values (database):</b>
///     </para>
///     <list type="bullet">
///         <item><c>"ready"</c> — The subsystem is fully operational.</item>
///         <item><c>"initializing"</c> — Initialization has not started or is in progress.</item>
///         <item><c>"failed"</c> — Initialization or a runtime operation failed.</item>
///         <item><c>"disconnected"</c> — Connection was lost at runtime.</item>
///     </list>
/// </remarks>
/// <param name="Status">
/// A short textual indicator of the subsystem's health state (e.g., <c>"ready"</c>, <c>"failed"</c>).
/// </param>
/// <param name="Message">
/// An optional human-readable message providing additional context about the subsystem's current state.
/// <see langword="null"/> when the subsystem is fully operational.
/// </param>
public sealed record ApiHealthComponentStatus(string Status, string? Message);
