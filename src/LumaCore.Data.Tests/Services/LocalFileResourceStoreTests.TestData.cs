// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Data.Tests.Services;

// Common test data.
public sealed partial class LocalFileResourceStoreTests
{
	/// <summary>
	/// Storage paths that must be rejected by <c>ResolveSafePath</c> on every public API:
	/// relative escape sequences and rooted absolute paths. Each row carries a short scenario
	/// label so test output stays readable.
	/// </summary>
	public static TheoryData<string> EscapingPaths()
	{
		var data = new TheoryData<string>
		{
			// Single-step relative escape — the simplest traversal attack.
			"../escape.bin",

			// Multi-step escape — confirms the check isn't fooled by depth.
			"../../etc/passwd",

			// Nested path that ultimately escapes — combine() collapses it before the check.
			"sub/../../escape.bin"
		};

		// Rooted absolute path — Path.Combine() with a rooted second argument throws away the first,
		// so the result lands wherever the attacker chose. ResolveSafePath must catch this via
		// Path.IsPathRooted(relative). Use a platform-appropriate rooted path so the test runs
		// identically on Windows agents and POSIX CI.
		data.Add(OperatingSystem.IsWindows() ? @"C:\Windows\System32\evil.bin" : "/etc/evil.bin");

		return data;
	}
}
