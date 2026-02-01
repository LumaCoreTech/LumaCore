// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

/// <summary>
/// Unit tests for <see cref="AsyncManualResetEvent"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify correctness of the async manual-reset event implementation including construction,
///     signaling, reset, waiting, and cancellation behavior.
///     </para>
///     <para>
///     Test files are organized by public API member:
///     <list type="bullet">
///         <item><c>AsyncManualResetEventTests.Construction.cs</c> — Constructor tests</item>
///         <item><c>AsyncManualResetEventTests.Properties.cs</c> — Property tests</item>
///         <item><c>AsyncManualResetEventTests.Set.cs</c> — <see cref="AsyncManualResetEvent.Set()"/> tests</item>
///         <item><c>AsyncManualResetEventTests.Reset.cs</c> — <see cref="AsyncManualResetEvent.Reset()"/> tests</item>
///         <item><c>AsyncManualResetEventTests.WaitAsync.cs</c> — <see cref="AsyncManualResetEvent.WaitAsync()"/> tests</item>
///         <item><c>AsyncManualResetEventTests.Wait.cs</c> — <see cref="AsyncManualResetEvent.Wait()"/> tests</item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "Threading")]
public partial class AsyncManualResetEventTests;
