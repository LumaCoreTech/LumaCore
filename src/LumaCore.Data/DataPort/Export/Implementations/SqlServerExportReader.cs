// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Runtime.CompilerServices;

using LumaCore.Data.DataPort.Models;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using static LumaCore.Data.DataPort.SqlIdentifierHelper;

namespace LumaCore.Data.DataPort.Export.Implementations;

/// <summary>
/// Reads database content from SQL Server for export purposes.
/// </summary>
/// <remarks>
/// This reader uses SQL Server's snapshot isolation to ensure a consistent view.
/// It uses 'sys.dm_db_partition_stats' to get fast row count estimates.
/// </remarks>
public sealed class SqlServerExportReader : IDataExportReader
{
	private readonly ILogger               mLogger;
	private readonly string                mConnectionString;
	private readonly bool                  mRequireSnapshotIsolation;
	private readonly Func<string, string>? mShuttleTypeMapper;
	private          SqlConnection?        mConnection;
	private          SqlTransaction?       mTransaction;
	private          bool                  mDisposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerExportReader"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger to use.</param>
	/// <param name="requireSnapshotIsolation">
	/// When <see langword="true"/>, the reader will throw an exception if snapshot isolation is not enabled.
	/// When <see langword="false"/>, it will fall back to Read Committed with a warning.
	/// </param>
	/// <param name="shuttleTypeMapper">
	/// An optional function that maps SQL Server type names to SQLite shuttle storage types.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="connectionString"/> or <paramref name="logger"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="connectionString"/> is empty or consists only of white-space characters.
	/// </exception>
	public SqlServerExportReader(
		string                connectionString,
		ILogger               logger,
		bool                  requireSnapshotIsolation = false,
		Func<string, string>? shuttleTypeMapper        = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(logger);
		mConnectionString = connectionString;
		mLogger = logger;
		mRequireSnapshotIsolation = requireSnapshotIsolation;
		mShuttleTypeMapper = shuttleTypeMapper;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		mDisposed = true;

		// Dispose transaction.
		// This closes the transaction automatically inducing a rollback that does not do any harm as we only read.
		if (mTransaction != null)
		{
			await mTransaction.DisposeAsync().ConfigureAwait(false);
			mTransaction = null;
		}

		// Dispose connection.
		if (mConnection != null)
		{
			await mConnection.DisposeAsync().ConfigureAwait(false);
			mConnection = null;
		}
	}

	/// <inheritdoc/>
	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();

		// Prevent re-initialization.
		if (mConnection != null)
			throw new InvalidOperationException("Reader has already been initialized.");

		// Open connection.
		mConnection = new SqlConnection(mConnectionString);
		await mConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Check if snapshot isolation is enabled.
		bool snapshotEnabled = await IsSnapshotIsolationEnabledAsync(cancellationToken).ConfigureAwait(false);

		// Begin transaction with appropriate isolation level.
		if (snapshotEnabled)
		{
			// Use snapshot isolation for a consistent view.
			mTransaction = (SqlTransaction)await mConnection.BeginTransactionAsync(
					                               IsolationLevel.Snapshot,
					                               cancellationToken)
				                               .ConfigureAwait(false);
		}
		else if (mRequireSnapshotIsolation)
		{
			// Strict mode: Fail instead of falling back to a weaker isolation level.
			throw new InvalidOperationException(
				"Snapshot isolation is required for consistent exports but is not enabled on this database. " +
				"Enable it with: ALTER DATABASE [YourDatabase] SET ALLOW_SNAPSHOT_ISOLATION ON; " +
				"Or set Database:RequireSnapshotIsolationForExport to false to allow fallback to Read Committed.");
		}
		else
		{
			// Fallback to Read Committed.
			// Note: This may not guarantee a fully consistent snapshot if data is being modified concurrently.
			mTransaction = (SqlTransaction)await mConnection.BeginTransactionAsync(
					                               IsolationLevel.ReadCommitted,
					                               cancellationToken)
				                               .ConfigureAwait(false);

			mLogger.LogWarning(
				"Snapshot isolation is not enabled — falling back to Read Committed, export may be inconsistent if data is modified concurrently");
		}
	}

	/// <inheritdoc/>
	public async Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		var tables = new List<string>();

		// Query user tables from INFORMATION_SCHEMA.
		var cmd = new SqlCommand(
			"""
			SELECT TABLE_NAME
			FROM INFORMATION_SCHEMA.TABLES
			WHERE TABLE_TYPE = 'BASE TABLE'
			  AND TABLE_NAME != '__EFMigrationsHistory'
			ORDER BY TABLE_NAME
			""",
			mConnection,
			mTransaction);
		try
		{
			SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
	public async Task<TableSnapshot> ReadTableAsync(string tableName, CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		// Get column definitions.
		List<ColumnDefinition> columns = await GetColumnsAsync(tableName, cancellationToken).ConfigureAwait(false);

		// Get estimated row count from partition stats (fast and accurate).
		long estimatedRows = -1;
		try
		{
			// Sum the row_count of all partitions for the table (index_id < 2 means heap or clustered index).
			string sql =
				"SELECT SUM(row_count) FROM sys.dm_db_partition_stats WHERE object_id = OBJECT_ID(@tableName) AND (index_id < 2);";
			var cmd = new SqlCommand(sql, mConnection, mTransaction);
			try
			{
				cmd.Parameters.AddWithValue("@tableName", tableName);
				object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				if (result != null && result != DBNull.Value)
				{
					estimatedRows = Convert.ToInt64(result);
				}
			}
			finally
			{
				await cmd.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch
		{
			estimatedRows = -1; // Fallback
		}

		// Return snapshot with streaming rows.
		return new TableSnapshot
		{
			Name = tableName,
			Columns = columns,
			EstimatedRowCount = estimatedRows,
			Rows = ReadRowsAsync(tableName, cancellationToken)
		};
	}

	/// <inheritdoc/>
	public async Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		var migrations = new List<MigrationInfo>();

		// Check if table exists.
		var checkCmd = new SqlCommand(
			"""
			SELECT CASE WHEN EXISTS (
				SELECT * FROM INFORMATION_SCHEMA.TABLES
				WHERE TABLE_NAME = '__EFMigrationsHistory'
			) THEN 1 ELSE 0 END
			""",
			mConnection,
			mTransaction);
		try
		{
			bool exists = Convert.ToBoolean(await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
			if (!exists) return migrations; // Empty list.
		}
		finally
		{
			await checkCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Read migration history.
		var cmd = new SqlCommand(
			"SELECT [MigrationId], [ProductVersion] FROM [__EFMigrationsHistory] ORDER BY [MigrationId]",
			mConnection,
			mTransaction);
		try
		{
			SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					migrations.Add(new MigrationInfo(reader.GetString(0), reader.GetString(1)));
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}

			return migrations;
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Checks if snapshot isolation is enabled on the database.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if snapshot isolation is enabled;<br/>
	/// otherwise, <see langword="false"/>.
	/// </returns>
	private async Task<bool> IsSnapshotIsolationEnabledAsync(CancellationToken cancellationToken)
	{
		// This command must run *outside* any transaction
		var cmd = new SqlCommand(
			"SELECT snapshot_isolation_state FROM sys.databases WHERE name = DB_NAME()",
			mConnection);
		try
		{
			object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			return result != null && Convert.ToBoolean(result);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
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
		var columns = new List<ColumnDefinition>();

		// Query column definitions from INFORMATION_SCHEMA.
		var cmd = new SqlCommand(
			"""
			SELECT c.COLUMN_NAME, c.DATA_TYPE
			FROM INFORMATION_SCHEMA.COLUMNS c
			WHERE c.TABLE_NAME = @tableName
			ORDER BY c.ORDINAL_POSITION
			""",
			mConnection,
			mTransaction);
		try
		{
			cmd.Parameters.AddWithValue("@tableName", tableName);
			SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					string dbType = reader.GetString(1);
					columns.Add(
						new ColumnDefinition
						{
							Name = reader.GetString(0),
							DbType = dbType,
							ShuttleStorageType = mShuttleTypeMapper?.Invoke(dbType)
						});
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}

			return columns;
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
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
		ArgumentNullException.ThrowIfNull(tableName);

		// Query all rows from the table.
		// Use SqlIdentifierHelper for safe SQL Server identifier quoting.
		var cmd = new SqlCommand($"SELECT * FROM {QuoteSqlServer(tableName)}", mConnection, mTransaction);
		try
		{
			SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				int fieldCount = reader.FieldCount;
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					object?[] row = new object?[fieldCount];
					for (int i = 0; i < fieldCount; i++)
					{
						object value = reader.GetValue(i);       // Reads the value synchronously (very fast).
						row[i] = value is DBNull ? null : value; // Convert DBNull to null for consistency.
					}
					yield return row;
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
	}

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if the reader has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(mDisposed, this);

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if the reader has been disposed, or
	/// <see cref="InvalidOperationException"/> if it has not been initialized.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The reader is not initialized.</exception>
	private void ThrowIfNotInitialized()
	{
		ThrowIfDisposed();

		if (mConnection is null || mTransaction is null)
			throw new InvalidOperationException("Reader not initialized.");
	}
}
