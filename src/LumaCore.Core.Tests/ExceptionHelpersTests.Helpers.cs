// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Tests;

public partial class ExceptionHelpersTests
{
	/// <summary>
	/// Helper method that throws an <see cref="InvalidOperationException"/> to create a real stack trace.
	/// </summary>
	private static void ThrowOriginalException()
	{
		throw new InvalidOperationException("Original exception from helper method");
	}
}
