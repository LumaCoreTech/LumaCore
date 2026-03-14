// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.DataPort.Models;

/// <summary>
/// Provides a detailed status report for long-running DataPort operations.
/// Separates overall progress (e.g., tables) from detailed progress (e.g., rows within a table).
/// </summary>
public class DataPortProgressReport
{
	// --- Overall Progress (e.g., Table 5 of 10) ---

	/// <summary>
	/// Gets the total number of main steps (e.g., 10 tables).
	/// </summary>
	public int OverallTotalSteps { get; init; }

	/// <summary>
	/// Gets the currently processed main step (e.g., Table 5).
	/// </summary>
	public int OverallCurrentStep { get; init; }

	/// <summary>
	/// Gets a message for the overall status (e.g., "Importing table 'Users'...")
	/// </summary>
	public string OverallMessage { get; init; } = string.Empty;

	/// <summary>
	/// Gets the overall progress percentage (0-100).
	/// </summary>
	public double OverallPercentage =>
		OverallTotalSteps == 0 ? 0 : (double)OverallCurrentStep / OverallTotalSteps * 100;

	// --- Detailed Progress (e.g., Row 500,000 of 1,000,000) ---

	/// <summary>
	/// Gets the estimated total number of sub-steps (e.g., 1,000,000 rows).<br/>
	/// <see langword="null"/>, if unknown.
	/// </summary>
	public long? DetailedTotalSteps { get; init; }

	/// <summary>
	/// Gets the currently processed sub-step (e.g., 500,000 rows).<br/>
	/// <see langword="null"/>, if not applicable.
	/// </summary>
	public long? DetailedCurrentStep { get; init; }

	/// <summary>
	/// Gets a message for the detailed status (e.g., "500,000 rows processed").<br/>
	/// <see langword="null"/>, if not applicable.
	/// </summary>
	public string? DetailedMessage { get; init; }

	/// <summary>
	/// Gets the detailed progress percentage (0-100).<br/>
	/// <see langword="null"/>, if the total count is unknown.
	/// </summary>
	public double? DetailedPercentage
	{
		get
		{
			if (DetailedTotalSteps is not > 0)
			{
				return null; // We don't know the total, so no percentage.
			}

			if (DetailedCurrentStep.GetValueOrDefault() >= DetailedTotalSteps.Value)
			{
				return 100.0; // CAP at 100%
			}

			return (double?)DetailedCurrentStep.GetValueOrDefault() / DetailedTotalSteps.Value * 100;
		}
	}
}
