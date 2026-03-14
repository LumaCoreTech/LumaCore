// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.DataPort;

/// <summary>
/// Exception that is thrown when the migration history stored in a shuttle file does not match the migration history of
/// the target database.
/// </summary>
public sealed class DataPortSchemaMismatchException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DataPortSchemaMismatchException"/> class.
	/// </summary>
	/// <param name="shuttleMigrationHistory">The migration history stored in the shuttle file.</param>
	/// <param name="targetMigrationHistory">The migration history stored in the target database.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="shuttleMigrationHistory"/> or <paramref name="targetMigrationHistory"/> is <see langword="null"/>.
	/// </exception>
	public DataPortSchemaMismatchException(
		IReadOnlyList<MigrationInfo> shuttleMigrationHistory,
		IReadOnlyList<MigrationInfo> targetMigrationHistory)
		: base(CreateMessage(shuttleMigrationHistory, targetMigrationHistory, out int? firstMismatchIndex))
	{
		// Note: Null checks are performed by CreateMessage() which is called first.
		ShuttleMigrationHistory = shuttleMigrationHistory;
		TargetMigrationHistory = targetMigrationHistory;
		FirstMismatchIndex = firstMismatchIndex;
	}

	/// <summary>
	/// Gets the migration history from the shuttle file.
	/// </summary>
	public IReadOnlyList<MigrationInfo> ShuttleMigrationHistory { get; }

	/// <summary>
	/// Gets the migration history from the target database.
	/// </summary>
	public IReadOnlyList<MigrationInfo> TargetMigrationHistory { get; }

	/// <summary>
	/// Gets the index of the first mismatch between <see cref="ShuttleMigrationHistory"/> and
	/// <see cref="TargetMigrationHistory"/>, or <see langword="null"/> if the mismatch is caused by different lengths.
	/// </summary>
	public int? FirstMismatchIndex { get; }

	/// <summary>
	/// Compares two migration history sequences and generates a message describing any schema version mismatch.
	/// </summary>
	/// <param name="shuttleMigrationHistory">
	/// The migration history sequence to compare, typically representing the current or source state.
	/// </param>
	/// <param name="targetMigrationHistory">
	/// The migration history sequence to compare against, typically representing the target or expected state.
	/// </param>
	/// <param name="firstMismatchIndex">
	/// When this method returns, contains the zero-based index of the first migration where the histories differ,
	/// or <see langword="null"/> if the mismatch is caused by different history lengths.
	/// </param>
	/// <returns>
	/// A message describing the schema version mismatch. If a mismatch is found at a specific index, the message includes
	/// the index; otherwise, the message indicates whether the mismatch is due to differing history lengths or a general
	/// schema version mismatch.
	/// </returns>
	private static string CreateMessage(
		IReadOnlyList<MigrationInfo> shuttleMigrationHistory,
		IReadOnlyList<MigrationInfo> targetMigrationHistory,
		out int?                     firstMismatchIndex)
	{
		ArgumentNullException.ThrowIfNull(shuttleMigrationHistory);
		ArgumentNullException.ThrowIfNull(targetMigrationHistory);

		int min = Math.Min(shuttleMigrationHistory.Count, targetMigrationHistory.Count);
		for (int i = 0; i < min; i++)
		{
			if (!Equals(shuttleMigrationHistory[i], targetMigrationHistory[i]))
			{
				firstMismatchIndex = i;
				return $"Schema version mismatch at migration index {i}.";
			}
		}

		firstMismatchIndex = null;
		return shuttleMigrationHistory.Count == targetMigrationHistory.Count
			       ? "Schema version mismatch."
			       : "Schema version mismatch due to different migration history length.";
	}
}
