// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace LumaCore.Core.Collections;

[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
[DebuggerTypeProxy(typeof(Deque<>.DebugView))]
public sealed partial class Deque<T>
{
	/// <summary>
	/// Provides a debugger-friendly view of the deque contents.
	/// </summary>
	/// <remarks>
	/// This class is used by the debugger to display the deque elements in a flat array format,
	/// hiding the internal circular buffer complexity.
	/// </remarks>
	[DebuggerNonUserCode]
	[ExcludeFromCodeCoverage]
	private sealed class DebugView(Deque<T> deque)
	{
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public IEnumerable<T> Items => deque.ToArray();
	}
}
