// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

/// <summary>
/// Unit tests for <see cref="TokenRevocationService"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>Construction — Null guards and valid instantiation.</item>
///         <item>RevokeAsync — From input validation through persistence, idempotency, and cache coherence.</item>
///         <item>
///         IsRevokedAsync — From input validation through lookup results, caching semantics,
///         and multi-instance propagation.
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Auth")]
public sealed partial class TokenRevocationServiceTests;
