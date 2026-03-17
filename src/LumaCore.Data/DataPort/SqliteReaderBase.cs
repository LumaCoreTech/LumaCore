// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Runtime.CompilerServices;

using LumaCore.Core.Diagnostics;
using LumaCore.Data.DataPort.Models;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

using static LumaCore.Data.DataPort.SqlIdentifierHelper;

namespace LumaCore.Data.DataPort;

/// <summary>
/// Abstract base class providing shared SQLite read mechanics for both production database exports and shuttle
/// file reads.
/// </summary>
/// <remarks>
///     <para>
///     This class encapsulates the common SQLite operations: opening a connection with a serializable transaction,
///     reading column metadata via <c>PRAGMA table_info</c>, streaming rows via <c>SELECT *</c>, and querying
///     EF Core migration history. It is agnostic of the specific schema (production vs. shuttle) and delegates
///     schema-specific logic to derived classes.
///     </para>
///     <para>
///     Derived classes must call <see cref="InitializeCoreAsync"/> from their <c>InitializeAsync</c>
///     implementation. The base class handles robust cleanup on initialization failure: if an exception occurs
///     after the connection is opened but before initialization completes, the connection and transaction are
///     disposed automatically.
///     </para>
///     <para>
///     Instances are not thread-safe. Callers must not invoke methods concurrently on the same instance.
///     </para>
/// </remarks>
public abstract class SqliteReaderBase : IAsyncDisposable
{
	private readonly string                mConnectionString;
	private readonly Func<string, string>? mShuttleTypeMapper;
	private          bool                  mDisposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteReaderBase"/> class.
	/// </summary>
	/// <param name="connectionString">The SQLite connection string.</param>
	/// <param name="shuttleTypeMapper">
	/// An optional function that maps provider-specific database types to SQLite shuttle storage types.
	/// When <see langword="null"/>, <see cref="ColumnDefinition.ShuttleStorageType"/> is not populated.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="connectionString"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="connectionString"/> is empty or consists only of white-space characters.
	/// </exception>
	protected SqliteReaderBase(string connectionString, Func<string, string>? shuttleTypeMapper = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		mConnectionString = connectionString;
		mShuttleTypeMapper = shuttleTypeMapper;
	}

	/// <summary>
	/// Disposes the underlying connection and transaction.
	/// </summary>
	/// <remarks>
	/// After disposal, any further attempts to read tables or enumerate previously obtained row streams will
	/// result in an exception.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		mDisposed = true;

		SqliteTransaction? transaction = Transaction;
		SqliteConnection? connection = Connection;
		Transaction = null;
		Connection = null;

		if (transaction != null)
			await transaction.DisposeAsync().ConfigureAwait(false);

		if (connection != null)
			await connection.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Gets a value indicating whether the reader has been successfully initialized via
	/// <see cref="InitializeCoreAsync"/> and has not yet been disposed.
	/// </summary>
	/// <remarks>
	/// Returns <see langword="false"/> both before initialization and after disposal, because
	/// <see cref="DisposeAsync"/> releases the underlying connection.
	/// </remarks>
	public bool IsInitialized => Connection is not null;

	/// <summary>
	/// Gets the underlying SQLite connection, or <see langword="null"/> if not yet initialized or already disposed.
	/// </summary>
	protected SqliteConnection? Connection { get; private set; }

	/// <summary>
	/// Gets the snapshot transaction, or <see langword="null"/> if not yet initialized or already disposed.
	/// </summary>
	protected SqliteTransaction? Transaction { get; private set; }

	/// <summary>
	/// Opens the SQLite connection, begins a serializable transaction, and invokes
	/// <see cref="OnInitializedAsync"/> for derived-class validation.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The reader has already been initialized.</exception>
	/// <remarks>
	/// If an exception occurs during initialization (including inside <see cref="OnInitializedAsync"/>),
	/// the connection and transaction are disposed before the exception propagates.
	/// </remarks>
	protected async Task InitializeCoreAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (Connection != null)
			throw new InvalidOperationException("Reader has already been initialized.");

		SqliteConnection? connection = null;
		SqliteTransaction? transaction = null;

		try
		{
			connection = new SqliteConnection(mConnectionString);
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			transaction = (SqliteTransaction)await connection
				                                 .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
				                                 .ConfigureAwait(false);

			await OnInitializedAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

			Connection = connection;
			Transaction = transaction;

			// Null out locals so the finally block doesn't dispose them.
			connection = null;
			transaction = null;
		}
		finally
		{
			if (transaction != null)
				await transaction.DisposeAsync().ConfigureAwait(false);

			if (connection != null)
				await connection.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Called after the connection and transaction are opened, but before they are assigned to class fields.
	/// Override to perform format-specific validation (e.g., shuttle completion marker checks).
	/// </summary>
	/// <param name="connection">The opened SQLite connection.</param>
	/// <param name="transaction">The active serializable transaction.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// If this method throws, the connection and transaction are disposed automatically by
	/// <see cref="InitializeCoreAsync"/>. The default implementation does nothing.
	/// </remarks>
	protected virtual Task OnInitializedAsync(
		SqliteConnection  connection,
		SqliteTransaction transaction,
		CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if the reader has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	protected void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);
	}

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if the reader has been disposed, or
	/// <see cref="InvalidOperationException"/> if it has not been initialized.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	/// The reader is not initialized. Call <c>InitializeAsync()</c> first.
	/// </exception>
	protected void ThrowIfNotInitialized()
	{
		ThrowIfDisposed();

		if (Connection is null || Transaction is null)
			throw new InvalidOperationException("Reader is not initialized. Call InitializeAsync() first.");
	}

	/// <summary>
	/// Retrieves column definitions for the specified table using <c>PRAGMA table_info</c>.
	/// </summary>
	/// <param name="tableName">The name of the table.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of <see cref="ColumnDefinition"/> objects, or an empty list if the table does not exist.
	/// </returns>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The reader is not initialized.</exception>
	protected async Task<List<ColumnDefinition>> GetColumnsAsync(string tableName, CancellationToken cancellationToken)
	{
		ThrowIfNotInitialized();

		var columns = new List<ColumnDefinition>();

		SqliteCommand cmd = Connection!.CreateCommand();
		try
		{
			cmd.Transaction = Transaction;
			cmd.CommandText = $"PRAGMA table_info({QuoteSqlite(tableName)})";
			SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					string dbType = reader.GetString(reader.GetOrdinal("type"));
					columns.Add(
						new ColumnDefinition
						{
							Name = reader.GetString(reader.GetOrdinal("name")),
							DbType = dbType,
							ShuttleStorageType = mShuttleTypeMapper?.Invoke(dbType)
						});
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

		return columns;
	}

	/// <summary>
	/// Streams rows from the specified table as an asynchronous enumerable.
	/// </summary>
	/// <param name="tableName">The name of the table.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An asynchronous enumerable of object arrays representing rows.</returns>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The reader is not initialized.</exception>
	/// <remarks>
	/// Each array represents a single row where elements correspond to the table columns in ordinal order.
	/// <see cref="DBNull"/> values are converted to <see langword="null"/>.
	/// </remarks>
	protected async IAsyncEnumerable<object?[]> ReadRowsAsync(
		string                                     tableName,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ThrowIfNotInitialized();

		SqliteCommand cmd = Connection!.CreateCommand();
		try
		{
			cmd.Transaction = Transaction;
			cmd.CommandText = $"SELECT * FROM {QuoteSqlite(tableName)}";
			SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				int fieldCount = reader.FieldCount;
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					object?[] row = new object?[fieldCount];
					for (int i = 0; i < fieldCount; i++)
					{
						object value = reader.GetValue(i);
						row[i] = value is DBNull ? null : value;
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
	/// Reads a table's schema and data into a <see cref="TableSnapshot"/> with an estimated row count.
	/// </summary>
	/// <param name="tableName">The name of the table to read.</param>
	/// <param name="logger">
	/// An optional logger. When provided, a warning is logged if the row count query fails.
	/// When <see langword="null"/>, the failure is silently ignored.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A <see cref="TableSnapshot"/> with schema, estimated row count, and a lazy row stream.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="tableName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="tableName"/> is empty or consists only of white-space characters.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The reader is not initialized.</exception>
	/// <exception cref="KeyNotFoundException">
	/// The table specified by <paramref name="tableName"/> does not exist in the underlying database.
	/// </exception>
	protected async Task<TableSnapshot> ReadTableSnapshotAsync(
		string            tableName,
		ILogger?          logger,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ThrowIfNotInitialized();

		List<ColumnDefinition> columns = await GetColumnsAsync(tableName, cancellationToken).ConfigureAwait(false);
		if (columns.Count == 0)
			throw new KeyNotFoundException($"Table '{tableName}' does not exist.");

		long rowCount = -1;
		try
		{
			ExecutionStageMonitor.ReportStage("ReadTable.CountRows");
			SqliteCommand countCmd = Connection!.CreateCommand();
			try
			{
				countCmd.Transaction = Transaction;
				countCmd.CommandText = $"SELECT COUNT(*) FROM {QuoteSqlite(tableName)}";
				object? result = await countCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				if (result is not null and not DBNull)
					rowCount = Convert.ToInt64(result);
			}
			finally
			{
				await countCmd.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger?.LogWarning(
				ex,
				"Failed to retrieve row count for table '{TableName}' — progress reporting will use an unknown row count",
				tableName);
			rowCount = -1;
		}

		return new TableSnapshot
		{
			Name = tableName,
			Columns = columns,
			EstimatedRowCount = rowCount,
			Rows = ReadRowsAsync(tableName, cancellationToken)
		};
	}

	/// <summary>
	/// Reads EF Core migration history from the specified migrations table.
	/// </summary>
	/// <param name="migrationsTableName">
	/// The name of the migrations table (e.g., <c>__EFMigrationsHistory</c> for production databases or the
	/// shuttle-specific migrations table name).
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of <see cref="MigrationInfo"/> entries ordered by migration ID, or an empty list if the migrations
	/// table does not exist.
	/// </returns>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The reader is not initialized.</exception>
	protected async Task<List<MigrationInfo>> ReadMigrationHistoryAsync(
		string            migrationsTableName,
		CancellationToken cancellationToken)
	{
		ThrowIfNotInitialized();

		var migrations = new List<MigrationInfo>();

		// Check if the migrations table exists.
		SqliteCommand checkCmd = Connection!.CreateCommand();
		try
		{
			checkCmd.Transaction = Transaction;
			checkCmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @TableName";
			checkCmd.Parameters.AddWithValue("@TableName", migrationsTableName);
			object? exists = await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (exists is null or DBNull)
				return migrations;
		}
		finally
		{
			await checkCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Read migration entries.
		SqliteCommand cmd = Connection.CreateCommand();
		try
		{
			cmd.Transaction = Transaction;
			cmd.CommandText =
				$"""
				 SELECT {QuoteSqlite("MigrationId")}, {QuoteSqlite("ProductVersion")}
				 FROM {QuoteSqlite(migrationsTableName)}
				 ORDER BY {QuoteSqlite("MigrationId")}
				 """;

			SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		return migrations;
	}
}
