// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.System;

partial class MetricsAggregator
{
	/// <summary>
	/// Comparer that sorts section names alphabetically, but always places <c>_errors</c> last.
	/// </summary>
	private sealed class ErrorsLastComparer : IComparer<string>
	{
		public static readonly ErrorsLastComparer Instance = new();

		public int Compare(string? x, string? y)
		{
			if (x == "_errors") return y == "_errors" ? 0 : 1;
			if (y == "_errors") return -1;
			return StringComparer.OrdinalIgnoreCase.Compare(x, y);
		}
	}
}
