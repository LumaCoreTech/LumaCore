// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

/// <summary>
/// Unit tests for <see cref="DefaultAsyncWaitQueue{T}"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify correctness of the wait queue implementation including
///     enqueue, dequeue, and cancellation behavior.
///     </para>
///     <para>
///     Test files are organized by public API member:
///     <list type="bullet">
///         <item><c>DefaultAsyncWaitQueueTests.Enqueue.cs</c> — <see cref="DefaultAsyncWaitQueue{T}.Enqueue"/> tests</item>
///         <item><c>DefaultAsyncWaitQueueTests.Dequeue.cs</c> — <see cref="DefaultAsyncWaitQueue{T}.Dequeue"/> tests</item>
///         <item><c>DefaultAsyncWaitQueueTests.DequeueAll.cs</c> — <see cref="DefaultAsyncWaitQueue{T}.DequeueAll"/> tests</item>
///         <item><c>DefaultAsyncWaitQueueTests.TryCancel.cs</c> — <see cref="DefaultAsyncWaitQueue{T}.TryCancel"/> tests</item>
///         <item><c>DefaultAsyncWaitQueueTests.CancelAll.cs</c> — <see cref="DefaultAsyncWaitQueue{T}.CancelAll"/> tests</item>
///         <item><c>DefaultAsyncWaitQueueTests.Properties.cs</c> — <see cref="DefaultAsyncWaitQueue{T}.IsEmpty"/> tests</item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "Threading")]
public partial class DefaultAsyncWaitQueueTests;
