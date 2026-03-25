// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

/// <summary>
/// Integration tests for <see cref="AuthorizationValidation"/>.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="AuthorizationValidation"/> is a <b>startup configuration guard</b>, not a runtime
///     authorization mechanism. It runs once during application startup and verifies that every versioned API
///     endpoint has an explicit <c>RequireAuthorization()</c> or <c>AllowAnonymous()</c> declaration —
///     preventing developers from accidentally leaving an endpoint without a conscious security decision.
///     </para>
///     <para>
///     The validator uses two criteria:
///     </para>
///     <list type="number">
///         <item>
///         <b>Scope (route prefix):</b> Only endpoints whose route starts with <c>/api/v</c> are inspected.
///         Infrastructure endpoints (e.g., <c>/health</c>) are outside this scope and never checked.
///         </item>
///         <item>
///         <b>Compliance (endpoint metadata):</b> Each in-scope endpoint must carry
///         <see cref="IAuthorizeData"/> or <see cref="IAllowAnonymous"/> metadata. Missing metadata triggers
///         a startup exception listing the offending endpoints.
///         </item>
///     </list>
///     <para>
///     Each test creates a minimal <see cref="WebApplication"/> with specific endpoint configurations to
///     simulate realistic startup conditions.
///     </para>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>
///         ValidateExplicitAuthorizationPolicies — endpoint authorization enforcement at startup.
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Auth")]
public sealed partial class AuthorizationValidationTests;
