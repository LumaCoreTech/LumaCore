// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.ApiVersioning;

using Microsoft.AspNetCore.Builder;

using Xunit;

namespace LumaCore.Api.Tests.Features.ApiVersioning;

/// <summary>
/// Integration tests for <see cref="ApiVersionValidation"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify that the startup versioning validation correctly enforces explicit
///     <c>MapToApiVersion()</c> declarations on all versioned API endpoints. Each test creates a minimal
///     <see cref="WebApplication"/> with specific endpoint configurations to simulate realistic startup conditions.
///     </para>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>
///         ValidateExplicitApiVersionMappings — endpoint version mapping enforcement at startup.
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "ApiVersioning")]
public sealed partial class ApiVersionValidationTests;
