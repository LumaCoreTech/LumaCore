// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

/// <summary>
/// Unit tests for <see cref="JwtTokenFactory"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>
///         CreateToken — Token structure, claims embedding, lifetime control, and signature validation.
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Auth")]
public sealed partial class JwtTokenFactoryTests;
