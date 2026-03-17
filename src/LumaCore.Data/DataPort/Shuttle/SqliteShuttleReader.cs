// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;

using LumaCore.Core;
using LumaCore.Data.DataPort.Models;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

using static LumaCore.Data.DataPort.SqlIdentifierHelper;

namespace LumaCore.Data.DataPort.Shuttle;

/// <summary>
/// Reads data exports from a LumaCore Shuttle file (SQLite-based) with optimized performance.
/// </summary>
/// <remarks>
///     <para>
///     This class reads the schema and streams data row-by-row from a LumaCore Shuttle file,
///     providing <see cref="TableSnapshot"/> instances to the import process. It uses <c>COUNT(*)</c> to
///     obtain an exact row count for progress reporting.
///     </para>
///     <para>
///     Instances of this reader are not thread-safe. Tables should be read sequentially; callers must not
///     invoke methods concurrently on the same instance.
///     </para>
///     <para>
///     The reader is designed to consume shuttle files produced and finalized by the matching
///     <see cref="SqliteShuttleWriter"/>. During <see cref="InitializeAsync"/>, it validates a format-specific
///     completion marker and will refuse to open files that do not represent a fully completed export.
///     </para>
/// </remarks>
public sealed class SqliteShuttleReader : SqliteReaderBase, IShuttleReader
{
	private readonly ILogger mLogger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteShuttleReader"/> class.
	/// </summary>
	/// <param name="filePath">The path to the shuttle file.</param>
	/// <param name="logger">The logger for progress reporting.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="filePath"/> or <paramref name="logger"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="filePath"/> is empty, consists only of white-space characters, contains characters that are
	/// invalid on the current operating system, or contains a path segment that exceeds 255 characters.
	/// </exception>
	public SqliteShuttleReader(string filePath, ILogger logger)
		: base(BuildConnectionString(filePath))
	{
		ArgumentNullException.ThrowIfNull(logger);
		mLogger = logger;
	}

	/// <inheritdoc/>
	/// <exception cref="InvalidOperationException">
	///     <para>The reader has already been initialized.</para>
	///     <para>- or -</para>
	///     <para>The shuttle file does not contain valid completion markers or the format version is not supported.</para>
	/// </exception>
	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			await InitializeCoreAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			mLogger.LogError(
				ex,
				"Shuttle file validation failed while checking completion markers — the file will be treated as invalid");

			throw;
		}
	}

	/// <inheritdoc/>
	protected override async Task OnInitializedAsync(
		SqliteConnection  connection,
		SqliteTransaction transaction,
		CancellationToken cancellationToken)
	{
		bool isValid = await VerifyCompletionMarkerAsync(connection, transaction, cancellationToken)
			               .ConfigureAwait(false);
		if (!isValid)
		{
			throw new InvalidOperationException(
				"Shuttle file validation failed. The shuttle does not contain the expected completion markers " +
				"or the format version is not supported.");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     Executes <c>PRAGMA integrity_check</c> on the underlying SQLite database. This validates the
	///     B-tree structure, page consistency, and index integrity of every page in the file. The result
	///     is a single row containing <c>"ok"</c> on success, or one or more rows describing the errors found.
	///     </para>
	///     <para>
	///     This check is intentionally <b>not</b> part of <see cref="InitializeAsync"/> because it reads every
	///     data page and can be slow on large shuttle files. It should be called explicitly before destructive
	///     operations (e.g., dropping the target database schema during a restore) where the cost is justified.
	///     </para>
	/// </remarks>
	public async Task ValidateIntegrityAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		// Log file size so operators can estimate how long the check will take.
		// PRAGMA integrity_check reads every page — a few seconds for small files, minutes for multi-GB.
		string dataSource = Connection!.DataSource;
		long fileSizeBytes = File.Exists(dataSource) ? new FileInfo(dataSource).Length : -1;
		if (fileSizeBytes >= 0)
		{
			double fileSizeMb = fileSizeBytes / (1024.0 * 1024.0);
			mLogger.LogInformation(
				"Running integrity check on shuttle file ({ShuttleFileSizeMb:F1} MB)...",
				fileSizeMb);
		}
		else
		{
			mLogger.LogInformation("Running integrity check on shuttle file...");
		}

		// Execute PRAGMA integrity_check and collect errors.
		// If the result is "ok", the file is valid.
		// If there are errors, they are returned as separate rows.
		List<string>? errors = null;
		SqliteCommand cmd = Connection.CreateCommand();
		try
		{
			cmd.Transaction = Transaction;
			cmd.CommandText = "PRAGMA integrity_check";

			SqliteDataReader reader = await cmd
				                          .ExecuteReaderAsync(cancellationToken)
				                          .ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					string result = reader.GetString(0);
					if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
					{
						errors ??= [];
						errors.Add(result);
					}
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		// If there are errors, log them and throw.
		// Limit the exception message to the first 10 errors for readability.
		if (errors is { Count: > 0 })
		{
			foreach (string error in errors)
			{
				mLogger.LogError("Shuttle integrity error: {IntegrityError}", error);
			}

			string summary = string.Join("; ", errors.Take(10));
			if (errors.Count > 10)
				summary += $"; ... and {errors.Count - 10} more error(s)";

			throw new InvalidDataException(
				$"Shuttle file integrity check failed with {errors.Count} error(s): {summary}");
		}

		// The shuttle file passed the integrity check.
		mLogger.LogInformation("Shuttle file integrity check passed");
	}

	/// <inheritdoc/>
	public async Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		var tables = new List<string>();

		// Query all user tables excluding internal SQLite tables and shuttle-internal tables.
		SqliteCommand cmd = Connection!.CreateCommand();
		try
		{
			cmd.Transaction = Transaction;
			cmd.CommandText =
				"""
				SELECT name
				FROM sqlite_master
				WHERE type = 'table'
				  AND name != @MigrationsTable
				  AND name NOT GLOB '__Shuttle_*'
				  AND name NOT LIKE 'sqlite_%'
				ORDER BY name
				""";
			cmd.Parameters.AddWithValue("@MigrationsTable", SqliteShuttleSchema.MigrationsTableName);

			SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					tables.Add(reader.GetString(0));
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		return tables;
	}

	/// <inheritdoc/>
	public Task<TableSnapshot> ReadTableAsync(string tableName, CancellationToken cancellationToken = default) =>
		ReadTableSnapshotAsync(tableName, mLogger, cancellationToken);

	/// <inheritdoc/>
	public Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default) =>
		ReadMigrationHistoryAsync(SqliteShuttleSchema.MigrationsTableName, cancellationToken);

	/// <inheritdoc/>
	public async Task<Dictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		// Abort if backup info table does not exist.
		SqliteCommand checkCmd = Connection!.CreateCommand();
		try
		{
			checkCmd.Transaction = Transaction;
			checkCmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @BackupInfoTable";
			checkCmd.Parameters.AddWithValue("@BackupInfoTable", SqliteShuttleSchema.BackupInfoTableName);
			object? exists = await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (exists is null or DBNull) return metadata;
		}
		finally
		{
			await checkCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Read metadata entries.
		SqliteCommand cmd = Connection.CreateCommand();
		try
		{
			cmd.Transaction = Transaction;
			cmd.CommandText =
				$"SELECT {QuoteSqlite("key")}, {QuoteSqlite("value")} FROM {QuoteSqlite(SqliteShuttleSchema.BackupInfoTableName)}";
			SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					string key = reader.GetString(0);
					string value = reader.GetString(1);
					metadata[key] = value;
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		return metadata;
	}

	/// <inheritdoc/>
	public async Task<DateTimeOffset?> GetCreatedUtcAsync(CancellationToken cancellationToken = default)
	{
		Dictionary<string, string> metadata = await GetMetadataAsync(cancellationToken).ConfigureAwait(false);

		if (!metadata.TryGetValue(SqliteShuttleSchema.CreatedUtcKey, out string? value))
			return null;

		if (DateTimeOffset.TryParse(
			    value,
			    CultureInfo.InvariantCulture,
			    DateTimeStyles.RoundtripKind,
			    out DateTimeOffset result))
		{
			return result;
		}

		return null;
	}

	/// <summary>
	/// Builds a read-only SQLite connection string for the specified shuttle file path.
	/// </summary>
	/// <param name="filePath">The path to the shuttle file.</param>
	/// <returns>A SQLite connection string configured for read-only access.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="filePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="filePath"/> is empty, consists only of white-space characters, contains characters that are
	/// invalid on the current operating system, or contains a path segment that exceeds 255 characters.
	/// </exception>
	private static string BuildConnectionString(string filePath)
	{
		FilePathValidator.Validate(filePath);

		return new SqliteConnectionStringBuilder
		{
			DataSource = filePath,
			Mode = SqliteOpenMode.ReadOnly,
			Cache = SqliteCacheMode.Private,
			Pooling = false // Ensure file handle is released immediately on dispose
		}.ToString();
	}

	/// <summary>
	/// Verifies that the shuttle file contains the expected completion markers for status and format version.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This method first checks whether the backup info table defined by
	///     <see cref="SqliteShuttleSchema.BackupInfoTableName"/> exists at all. If the table is missing,
	///     the export is considered incomplete.
	///     </para>
	///     <para>
	///     If the table exists, it queries the entries for
	///     <see cref="SqliteShuttleSchema.ExportStatusKey"/> and
	///     <see cref="SqliteShuttleSchema.ShuttleFormatVersionKey"/> and verifies that the stored values
	///     correspond to <see cref="SqliteShuttleSchema.CompletedValue"/> and
	///     <see cref="SqliteShuttleSchema.CurrentShuttleFormatVersion"/>, respectively.
	///     </para>
	///     <para>
	///     The method returns <see langword="true"/> only if both markers are present and valid, i.e. the
	///     shuttle file has been finalized by the matching writer implementation; otherwise it returns
	///     <see langword="false"/>.
	///     </para>
	/// </remarks>
	private async Task<bool> VerifyCompletionMarkerAsync(
		SqliteConnection  connection,
		SqliteTransaction transaction,
		CancellationToken cancellationToken)
	{
		// Check if the backup info table defined by SqliteShuttleSchema.BackupInfoTableName exists at all.
		bool tableExists = false;
		SqliteCommand checkCmd = connection.CreateCommand();
		try
		{
			checkCmd.Transaction = transaction;
			checkCmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @BackupInfoTable";
			checkCmd.Parameters.AddWithValue("@BackupInfoTable", SqliteShuttleSchema.BackupInfoTableName);
			object? exists = await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (exists != null && exists != DBNull.Value)
			{
				tableExists = true;
			}
		}
		finally
		{
			await checkCmd.DisposeAsync().ConfigureAwait(false);
		}

		// If the table doesn't exist, the marker can't exist.
		if (!tableExists)
			return false;

		// The table *does* exist, so now we check for the markers.
		bool statusOk = false;
		bool versionOk = false;
		SqliteCommand cmd = connection.CreateCommand();
		try
		{
			cmd.Transaction = transaction;
			cmd.CommandText =
				$"""
				 SELECT {QuoteSqlite("key")}, {QuoteSqlite("value")}
				 FROM {QuoteSqlite(SqliteShuttleSchema.BackupInfoTableName)}
				 WHERE {QuoteSqlite("key")} = @StatusKey
				    OR {QuoteSqlite("key")} = @VersionKey
				 """;
			cmd.Parameters.AddWithValue("@StatusKey", SqliteShuttleSchema.ExportStatusKey);
			cmd.Parameters.AddWithValue("@VersionKey", SqliteShuttleSchema.ShuttleFormatVersionKey);

			SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					string key = reader.GetString(0);
					string value = reader.GetString(1);

					if (key == SqliteShuttleSchema.ExportStatusKey && value == SqliteShuttleSchema.CompletedValue)
					{
						statusOk = true;
					}
					else if (key == SqliteShuttleSchema.ShuttleFormatVersionKey &&
					         int.TryParse(value, out int version) &&
					         version == SqliteShuttleSchema.CurrentShuttleFormatVersion)
					{
						versionOk = true;
					}
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		return statusOk && versionOk;
	}
}
