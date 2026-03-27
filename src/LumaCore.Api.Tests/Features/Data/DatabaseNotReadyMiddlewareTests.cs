// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Data;

using Xunit;

namespace LumaCore.Api.Tests.Features.Data;

// Database readiness gate: from healthy pass-through to differentiated 503 responses.
//
// These tests exercise DatabaseNotReadyMiddleware by sending actual HTTP requests through a
// TestServer-backed pipeline and inspecting status codes, headers, and ProblemDetails bodies:
//
//   1. Pass-through: database ready → 200 (DatabaseReady_ApiRequest_ReturnsOk),
//      non-API paths bypass the gate entirely (NonApiRequest_WhenDatabaseNotReady_PassesThrough),
//      health endpoints are always allowed (see IsHealthEndpoint).
//
//   2. Transient states: NotStarted, InProgress, Disconnected, and Failed+Transient all produce
//      503 with Retry-After and the service-unavailable error type (see InvokeAsync).
//
//   3. Non-retryable failures: Failed+ConfigurationRequired → database-configuration-required,
//      Failed+ManualInterventionRequired → database-failed — both without Retry-After
//      (see InvokeAsync).
//
//   4. Health endpoint bypass: infrastructure probes (/health, /health/ready) and versioned API
//      health endpoints (/api/v1/health) always pass through. False positives like
//      /api/v1/users/health-records are correctly rejected (see IsHealthEndpoint).
//
// For ServiceRegistration tests, see ServiceRegistrationTests.

/// <summary>
/// Integration tests for <see cref="DatabaseNotReadyMiddleware"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify the HTTP-level behavior of the database readiness middleware: status codes,
///     <c>Retry-After</c> headers, and RFC 7807 ProblemDetails response bodies for each combination of
///     <see cref="LumaCore.Data.Initialization.DatabaseInitializationState"/> and
///     <see cref="LumaCore.Data.Initialization.DatabaseFailureCategory"/>.
///     </para>
///     <para>
///     The test harness uses <see cref="Infrastructure.MiddlewareTestHarness"/> with a minimal probe
///     endpoint (<c>GET /api/v1/probe</c>). No database or authentication is required — only a
///     <see cref="LumaCore.Data.Initialization.DatabaseInitializationStatus"/> singleton whose state is
///     controlled directly via its <c>internal</c> setters.
///     </para>
/// </remarks>
[Trait("Category", "Data")]
public sealed partial class DatabaseNotReadyMiddlewareTests;
