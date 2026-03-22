// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Core.Tests;

/// <summary>
/// Unit tests for <see cref="LifecycleManagement"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify the lifecycle management pattern including initialization, shutdown, disposal, and operation
///     tracking. Since <see cref="LifecycleManagement"/> is abstract, tests use <c>TestableLifecycleManagement</c> as a
///     concrete implementation.
///     </para>
///     <para>
///     Test files are organized by scenario type:
///     <list type="bullet">
///         <item><c>LifecycleManagementTests.Construction.cs</c> — Constructor tests (lifecycle-independent)</item>
///         <item><c>LifecycleManagementTests.Scenarios.cs</c> — Complete lifecycle flows (happy path)</item>
///         <item><c>LifecycleManagementTests.ErrorHandling.cs</c> — Exception handling and invalid state transitions</item>
///         <item><c>LifecycleManagementTests.Concurrency.cs</c> — Parallel access and pending operation handling</item>
///         <item><c>LifecycleManagementTests.Helpers.cs</c> — Test helper classes and shared utilities</item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "Core")]
public partial class LifecycleManagementTests;
