// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections;

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

/// <summary>
/// Unit tests for <see cref="Deque{T}"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify correctness of the double-ended queue implementation including
///     construction, element access, insertion, removal, and interface implementations.
///     </para>
///     <para>
///     Test files are organized by public API member (one file per method, properties grouped):
///     <list type="bullet">
///         <item><c>DequeTests.AddToBack.cs</c> — <see cref="Deque{T}.AddToBack"/> method tests</item>
///         <item><c>DequeTests.AddToFront.cs</c> — <see cref="Deque{T}.AddToFront"/> method tests</item>
///         <item><c>DequeTests.Clear.cs</c> — <see cref="Deque{T}.Clear"/> method tests</item>
///         <item><c>DequeTests.Construction.cs</c> — Constructor tests (including <see cref="Deque{T}(ReadOnlySpan{T})"/>)</item>
///         <item>
///         <c>DequeTests.CopyTo.cs</c> — <see cref="Deque{T}.CopyTo(Span{T})"/>,
///         <see cref="Deque{T}.CopyTo(T[], int)"/>, and <see cref="Deque{T}.TryCopyTo"/> tests
///         </item>
///         <item><c>DequeTests.Enumerator.cs</c> — Enumerator tests</item>
///         <item><c>DequeTests.Helpers.cs</c> — Shared assertion helpers (e.g., <c>AssertDequeState</c>)</item>
///         <item><c>DequeTests.IList.cs</c> — <see cref="IList"/> interface tests</item>
///         <item><c>DequeTests.IListGeneric.cs</c> — <see cref="IList{T}"/> interface tests</item>
///         <item><c>DequeTests.Indexer.cs</c> — Indexer access tests</item>
///         <item>
///         <c>DequeTests.InsertRange.cs</c> — <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> and
///         <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> tests
///         </item>
///         <item>
///         <c>DequeTests.PeekBack.cs</c> — <see cref="Deque{T}.PeekBack"/> and <see cref="Deque{T}.TryPeekBack"/> tests
///         </item>
///         <item>
///         <c>DequeTests.PeekFront.cs</c> — <see cref="Deque{T}.PeekFront"/> and <see cref="Deque{T}.TryPeekFront"/> tests
///         </item>
///         <item>
///         <c>DequeTests.Properties.cs</c> — <see cref="Deque{T}.Capacity"/>, <see cref="Deque{T}.IsEmpty"/>, and
///         <see cref="Deque{T}.IsFull"/> tests
///         </item>
///         <item>
///         <c>DequeTests.RemoveFromBack.cs</c> — <see cref="Deque{T}.RemoveFromBack"/> and
///         <see cref="Deque{T}.TryRemoveFromBack"/> tests
///         </item>
///         <item>
///         <c>DequeTests.RemoveFromFront.cs</c> — <see cref="Deque{T}.RemoveFromFront"/> and
///         <see cref="Deque{T}.TryRemoveFromFront"/> tests
///         </item>
///         <item><c>DequeTests.RemoveRange.cs</c> — <see cref="Deque{T}.RemoveRange"/> method tests</item>
///         <item><c>DequeTests.ToArray.cs</c> — <see cref="Deque{T}.ToArray"/> method tests</item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "Unit")]
public partial class DequeTests { }
