// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

/// <summary>
/// Unit tests for <see cref="TaskCompletionSourceExtensions"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify correctness of the TaskCompletionSource extension methods including
///     completion propagation and async task source creation.
///     </para>
///     <para>
///     Test files are organized by method:
///     <list type="bullet">
///         <item>
///         <c>TaskCompletionSourceExtensionsTests.TryCompleteFromCompletedTask.cs</c> —
///         <see cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult,TSourceResult}"/> tests
///         </item>
///         <item>
///         <c>TaskCompletionSourceExtensionsTests.CreateAsyncTaskSource.cs</c> —
///         <see cref="TaskCompletionSourceExtensions.CreateAsyncTaskSource{TResult}"/> tests
///         </item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "Threading")]
public partial class TaskCompletionSourceExtensionsTests;
