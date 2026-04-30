// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core;

/// <summary>
/// Provides helper methods for logging, including sanitization of user-derived text to prevent log forging.
/// </summary>
public static class LogHelpers
{
	/// <summary>
	/// Sanitizes user-derived text before writing to logs to prevent log forging.
	/// </summary>
	/// <param name="value">The value to sanitize.</param>
	/// <returns>A log-safe single-line value.</returns>
	/// <remarks>
	/// This method replaces newline characters with their escaped representations to ensure that log entries
	/// remain single-line and cannot be forged by injecting newlines.
	/// </remarks>
	public static string SanitizeText(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return string.Empty;

		return value
			.Replace("\r", "\\r", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal);
	}
}
