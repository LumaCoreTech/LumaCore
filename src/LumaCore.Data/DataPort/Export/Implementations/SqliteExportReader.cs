// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Models;

using Microsoft.Data.Sqlite;

namespace LumaCore.Data.DataPort.Export.Implementations;

/// <summary>
/// Reads database content from a production SQLite database for export purposes.
/// </summary>
/// <remarks>
///     <para>
///     SQLite has limited transaction isolation semantics, but the serializable transaction opened by the base
///     class provides a stable snapshot for reads against the same connection.
///     </para>
///     <para>
///     This reader exports only user tables and excludes the EF Core migration history table
///     (<c>__EFMigrationsHistory</c>) and internal SQLite tables.
///     </para>
/// </remarks>
public sealed class SqliteExportReader : SqliteReaderBase, IDataExportReader
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteExportReader"/> class.
	/// </summary>
	/// <param name="connectionString">The SQLite connection string.</param>
	/// <param name="shuttleTypeMapper">
	/// An optional function that maps SQLite type names to shuttle storage types.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="connectionString"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="connectionString"/> is empty or consists only of white-space characters.
	/// </exception>
	public SqliteExportReader(string connectionString, Func<string, string>? shuttleTypeMapper = null)
		: base(connectionString, shuttleTypeMapper) { }

	/// <inheritdoc/>
	public Task InitializeAsync(CancellationToken cancellationToken = default) =>
		InitializeCoreAsync(cancellationToken);

	/// <inheritdoc/>
	public async Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		var tables = new List<string>();

		SqliteCommand cmd = Connection!.CreateCommand();
		try
		{
			cmd.Transaction = Transaction;
			cmd.CommandText =
				"""
				SELECT name
				FROM sqlite_master
				WHERE type = 'table'
				  AND name != '__EFMigrationsHistory'
				  AND name NOT LIKE 'sqlite_%'
				ORDER BY name
				""";

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

			return tables;
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public Task<TableSnapshot> ReadTableAsync(string tableName, CancellationToken cancellationToken = default) =>
		ReadTableSnapshotAsync(tableName, logger: null, cancellationToken);

	/// <inheritdoc/>
	public Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default) =>
		ReadMigrationHistoryAsync("__EFMigrationsHistory", cancellationToken);
}
