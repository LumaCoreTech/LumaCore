// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using TaskExtensions = LumaCore.Core.Threading.TaskExtensions;

namespace LumaCore.Core.Tests.Threading;

/// <summary>
/// Unit tests for <see cref="TaskExtensions"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify correctness of the Task extension methods including
///     synchronous waiting, task combinators, and fire-and-forget patterns.
///     </para>
///     <para>
///     Test files are organized by method group:
///     <list type="bullet">
///         <item>
///         <c>TaskExtensionsTests.WaitAndUnwrapException.cs</c> —
///         <see cref="TaskExtensions.WaitAndUnwrapException(Task)"/> and overloads
///         </item>
///         <item>
///         <c>TaskExtensionsTests.WaitWithoutException.cs</c> —
///         <see cref="TaskExtensions.WaitWithoutException(Task)"/> and overloads
///         </item>
///         <item>
///         <c>TaskExtensionsTests.WhenAny.cs</c> —
///         <see cref="TaskExtensions.WhenAny(IEnumerable{Task})"/> and overloads
///         </item>
///         <item>
///         <c>TaskExtensionsTests.WhenAll.cs</c> —
///         <see cref="TaskExtensions.WhenAll(IEnumerable{Task})"/> and overloads
///         </item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "Threading")]
public partial class TaskExtensionsTests;
