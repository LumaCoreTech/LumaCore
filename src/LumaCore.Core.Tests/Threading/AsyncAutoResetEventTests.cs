// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

/// <summary>
/// Unit tests for <see cref="AsyncAutoResetEvent"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify correctness of the async auto-reset event implementation including construction,
///     signaling, waiting, and cancellation behavior.
///     </para>
///     <para>
///     Test files are organized by public API member:
///     <list type="bullet">
///         <item><c>AsyncAutoResetEventTests.Construction.cs</c> — Constructor tests</item>
///         <item><c>AsyncAutoResetEventTests.Properties.cs</c> — Property tests</item>
///         <item><c>AsyncAutoResetEventTests.Set.cs</c> — <see cref="AsyncAutoResetEvent.Set()"/> tests</item>
///         <item><c>AsyncAutoResetEventTests.WaitAsync.cs</c> — <see cref="AsyncAutoResetEvent.WaitAsync()"/> tests</item>
///         <item><c>AsyncAutoResetEventTests.Wait.cs</c> — <see cref="AsyncAutoResetEvent.Wait()"/> tests</item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "Threading")]
public partial class AsyncAutoResetEventTests;
