// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Runtime.CompilerServices;

using LumaCore.Data.DataPort.Models;

//using LumaCore.Data.Services.DataPort.Models;

// TODO: Re-enable when Pomelo releases EF Core 10 compatible version.
// Track: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues
//using MySqlConnector;

namespace LumaCore.Data.DataPort.Export.Implementations;

/// <summary>
/// Reads database content from MySQL for export purposes.
/// </summary>
/// <remarks>
/// This reader uses Repeatable Read isolation for consistent snapshots (on InnoDB).
/// It uses 'information_schema.TABLES' to get row count estimates.
/// </remarks>
public sealed class MySqlExportReader : IDataExportReader
{
	private readonly string mConnectionString;

	private bool mDisposed;

	/// <summary>
	/// Message used by <see cref="NotSupportedException"/> until Pomelo releases an EF Core 10 compatible provider.
	/// </summary>
	private const string NotSupportedMessage =
		"MySQL data export is not yet available. Pomelo.EntityFrameworkCore.MySql has not released an " +
		"EF Core 10 compatible version. Track progress at: " +
		"https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues";

	//private readonly Func<string, string>? mShuttleTypeMapper;
	//private          MySqlConnection?  mConnection;
	//private          MySqlTransaction? mTransaction;

	/// <summary>
	/// Initializes a new instance of the <see cref="MySqlExportReader"/> class.
	/// </summary>
	/// <param name="connectionString">The MySQL connection string.</param>
	/// <param name="shuttleTypeMapper">
	/// An optional function that maps MySQL type names to SQLite shuttle storage types.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="connectionString"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="connectionString"/> is empty or consists only of white-space characters.
	/// </exception>
	public MySqlExportReader(string connectionString, Func<string, string>? shuttleTypeMapper = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		mConnectionString = connectionString;
		//mShuttleTypeMapper = shuttleTypeMapper;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		mDisposed = true;

		//// Dispose transaction.
		//// This closes the transaction automatically inducing a rollback that does not do any harm as we only read.
		//if (mTransaction != null)
		//{
		//	await mTransaction.DisposeAsync().ConfigureAwait(false);
		//	mTransaction = null;
		//}

		//// Dispose connection.
		//if (mConnection != null)
		//{
		//	await mConnection.DisposeAsync().ConfigureAwait(false);
		//	mConnection = null;
		//}
	}

	/// <inheritdoc/>
	public Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException(NotSupportedMessage);

		//// Prevent re-initialization.
		//if (mConnection != null)
		//	throw new InvalidOperationException("Reader has already been initialized.");

		//// Open connection.
		//mConnection = new MySqlConnection(mConnectionString);
		//await mConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

		//// Use Repeatable Read for a consistent snapshot (InnoDB).
		//mTransaction = await mConnection
		//	               .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
		//	               .ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException(NotSupportedMessage);

		//// Validate initialization.
		//if (mConnection == null || mTransaction == null)
		//	throw new InvalidOperationException("Reader not initialized.");

		//var tables = new List<string>();

		//// Query user tables from information_schema excluding migration history.
		//string database = mConnection.Database;
		//await using var cmd = new MySqlCommand(
		//	"""
		//	SELECT table_name
		//	FROM information_schema.tables
		//	WHERE table_schema = @database
		//	  AND table_type = 'BASE TABLE'
		//	  AND table_name != '__EFMigrationsHistory'
		//	ORDER BY table_name
		//	""",
		//	mConnection,
		//	mTransaction);
		//cmd.Parameters.AddWithValue("@database", database);
		//await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		//while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		//{
		//	tables.Add(reader.GetString(0));
		//}

		//return tables;
	}

	/// <inheritdoc/>
	public async Task<TableSnapshot> ReadTableAsync(string tableName, CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException(NotSupportedMessage);

		//// Validate initialization.
		//if (mConnection == null || mTransaction == null)
		//	throw new InvalidOperationException("Reader not initialized.");

		//// Get column definitions.
		//List<ColumnDefinition> columns = await GetColumnsAsync(tableName, cancellationToken).ConfigureAwait(false);

		//// Get estimated row count from information_schema (fast, but an estimate).
		//long estimatedRows = -1;
		//try
		//{
		//	// TABLE_ROWS is a rough estimate for InnoDB, but instantly available.
		//	string dbName = mConnection.Database;
		//	string sql =
		//		"SELECT TABLE_ROWS FROM information_schema.TABLES WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tbl;";
		//	await using var cmd = new MySqlCommand(sql, mConnection, mTransaction);
		//	cmd.Parameters.AddWithValue("@db", dbName);
		//	cmd.Parameters.AddWithValue("@tbl", tableName);
		//	object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		//	if (result != null && result != DBNull.Value)
		//	{
		//		estimatedRows = Convert.ToInt64(result);
		//	}
		//}
		//catch
		//{
		//	estimatedRows = -1; // Fallback
		//}

		//// Return the snapshot with streaming rows.
		//return new TableSnapshot
		//{
		//	Name = tableName,
		//	Columns = columns,
		//	EstimatedRowCount = estimatedRows,
		//	Rows = ReadRowsAsync(tableName, cancellationToken)
		//};
	}

	/// <inheritdoc/>
	public async Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException(NotSupportedMessage);

		//// Validate initialization.
		//if (mConnection == null || mTransaction == null)
		//	throw new InvalidOperationException("Reader not initialized.");

		//var migrations = new List<(string, string)>();
		//string database = mConnection.Database;

		//// Check if table exists.
		//await using var checkCmd = new MySqlCommand(
		//	"""
		//	SELECT COUNT(*)
		//	FROM information_schema.tables
		//	WHERE table_schema = @database
		//	  AND table_name = '__EFMigrationsHistory'
		//	""",
		//	mConnection,
		//	mTransaction);
		//checkCmd.Parameters.AddWithValue("@database", database);
		//long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
		//if (count == 0) return migrations; // Empty list.

		//// Read migration history.
		//await using var cmd = new MySqlCommand(
		//	"SELECT `MigrationId`, `ProductVersion` FROM `__EFMigrationsHistory` ORDER BY `MigrationId`",
		//	mConnection,
		//	mTransaction);
		//await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		//while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		//{
		//	migrations.Add((reader.GetString(0), reader.GetString(1)));
		//}

		//return migrations;
	}

	/// <summary>
	/// Retrieves column definitions for the specified table.
	/// </summary>
	/// <param name="tableName">The name of the table.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of <see cref="ColumnDefinition"/> objects representing the table's columns.
	/// </returns>
	private async Task<List<ColumnDefinition>> GetColumnsAsync(string tableName, CancellationToken cancellationToken)
	{
		throw new NotSupportedException(NotSupportedMessage);

		//var columns = new List<ColumnDefinition>();
		//string database = mConnection!.Database;

		//// Get column definitions from information_schema.
		//await using var cmd = new MySqlCommand(
		//	"""
		//	SELECT
		//	    c.column_name,
		//	    c.data_type,
		//	    c.is_nullable,
		//	    c.column_key
		//	FROM information_schema.columns c
		//	    WHERE c.table_schema = @database
		//	      AND c.table_name = @tableName
		//	ORDER BY c.ordinal_position
		//	""",
		//	mConnection,
		//	mTransaction);
		//cmd.Parameters.AddWithValue("@database", database);
		//cmd.Parameters.AddWithValue("@tableName", tableName);
		//await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		//while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		//{
		//	columns.Add(
		//		new ColumnDefinition
		//		{
		//			Name = reader.GetString(0),
		//			DbType = reader.GetString(1),
		//			IsNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase),
		//			IsPrimaryKey = reader.GetString(3).Equals("PRI", StringComparison.OrdinalIgnoreCase)
		//		});
		//}

		//return columns;
	}

	/// <summary>
	/// Streams rows from the specified table as an asynchronous enumerable.
	/// </summary>
	/// <param name="tableName">The name of the table to read rows from.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// An asynchronous enumerable of object arrays, each representing a row in the table.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="tableName"/> is <see langword="null"/>.</exception>
	private async IAsyncEnumerable<object?[]> ReadRowsAsync(
		string                                     tableName,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		throw new NotSupportedException(NotSupportedMessage);
		yield break;
		//ArgumentNullException.ThrowIfNull(tableName);

		//// Query all rows from the table.
		//// Use SqlIdentifierHelper for safe MySQL identifier quoting.
		//await using var cmd = new MySqlCommand($"SELECT * FROM {QuoteMySql(tableName)}", mConnection, mTransaction);
		//await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		//int fieldCount = reader.FieldCount;
		//while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		//{
		//	object?[] row = new object?[fieldCount];
		//	for (int i = 0; i < fieldCount; i++)
		//	{
		//		object value = reader.GetValue(i);       // Reads the value synchronously (very fast).
		//		row[i] = value is DBNull ? null : value; // Convert DBNull to null for consistency.
		//	}
		//	yield return row;
		//}
	}
}
