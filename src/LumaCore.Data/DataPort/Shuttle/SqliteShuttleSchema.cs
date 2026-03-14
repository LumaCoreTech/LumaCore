// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.Immutable;

namespace LumaCore.Data.DataPort.Shuttle;

/// <summary>
/// Provides shared schema constants for SQLite-based LumaCore Shuttle files.
/// </summary>
/// <remarks>
///     <para>
///     This type centralizes magic string identifiers that are used by both
///     <see cref="SqliteShuttleWriter"/> and <see cref="SqliteShuttleReader"/> to describe
///     the layout of the shuttle database and its completion marker.
///     </para>
/// </remarks>
static class SqliteShuttleSchema
{
	/// <summary>
	/// The file extension for LumaCore Shuttle files (including the leading dot).
	/// </summary>
	/// <remarks>
	/// The extension <c>.shuttle.sqlite</c> indicates a SQLite-based LumaCore Shuttle container.
	/// Use this constant when creating or searching for shuttle files to ensure consistency.
	/// </remarks>
	internal const string FileExtension = ".shuttle.sqlite";

	/// <summary>
	/// The file search pattern for finding LumaCore Shuttle files in a directory.
	/// </summary>
	internal const string FileSearchPattern = "*" + FileExtension;

	/// <summary>
	/// Name of the table that stores general backup metadata.
	/// </summary>
	internal const string BackupInfoTableName = "__Shuttle_BackupInfo";

	/// <summary>
	/// Name of the table that stores Entity Framework Core migration history.
	/// </summary>
	internal const string MigrationsTableName = "__EFMigrationsHistory";

	/// <summary>
	/// Metadata key that stores the export completion status in the backup info table.
	/// </summary>
	internal const string ExportStatusKey = "ExportStatus";

	/// <summary>
	/// Value that identifies a successfully completed export.
	/// </summary>
	internal const string CompletedValue = "Completed";

	/// <summary>
	/// Metadata key that stores the LumaCore Shuttle format version.
	/// </summary>
	internal const string ShuttleFormatVersionKey = "ShuttleFormatVersion";

	/// <summary>
	/// Metadata key that stores the unique identity of a shuttle file.
	/// </summary>
	/// <remarks>
	/// The shuttle ID is a GUID written during <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/>
	/// so that only successfully completed exports receive an identity. It is used by the import pipeline
	/// to match checkpoint records against the shuttle file being imported, enabling crash-safe resume.
	/// </remarks>
	internal const string ShuttleIdKey = "ShuttleId";

	/// <summary>
	/// Metadata key that stores the UTC timestamp of when the export was created (ISO 8601 round-trip format).
	/// </summary>
	/// <remarks>
	/// Written automatically by <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/> using
	/// the <see cref="TimeProvider"/> supplied at construction time. Read by
	/// <see cref="SqliteShuttleReader.GetCreatedUtcAsync"/> for backup retention decisions.
	/// </remarks>
	internal const string CreatedUtcKey = "CreatedUtc";

	/// <summary>
	/// Current LumaCore Shuttle format version.
	/// </summary>
	internal const int CurrentShuttleFormatVersion = 1;

	/// <summary>
	/// Gets a set of all reserved metadata keys used by the LumaCore Shuttle format.
	/// </summary>
	internal static readonly ImmutableHashSet<string> ReservedMetadataKeys = ImmutableHashSet.Create(
		StringComparer.OrdinalIgnoreCase,
		ExportStatusKey,
		ShuttleFormatVersionKey,
		ShuttleIdKey,
		CreatedUtcKey);
}
