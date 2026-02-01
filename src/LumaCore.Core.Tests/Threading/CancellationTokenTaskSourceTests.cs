// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

/// <summary>
/// Unit tests for <see cref="CancellationTokenTaskSource{T}"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify correctness of the cancellation token task source implementation including
///     construction, task behavior, disposal, and edge cases.
///     </para>
///     <para>
///     Test files are organized by public API member:
///     <list type="bullet">
///         <item><c>CancellationTokenTaskSourceTests.Construction.cs</c> — Constructor tests</item>
///         <item>
///         <c>CancellationTokenTaskSourceTests.Task.cs</c> — <see cref="CancellationTokenTaskSource{T}.Task"/>
///         property tests
///         </item>
///         <item>
///         <c>CancellationTokenTaskSourceTests.Dispose.cs</c> — <see cref="CancellationTokenTaskSource{T}.Dispose"/>
///         method tests
///         </item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "Threading")]
public partial class CancellationTokenTaskSourceTests;
