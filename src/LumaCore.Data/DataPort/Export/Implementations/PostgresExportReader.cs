// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Runtime.CompilerServices;

using LumaCore.Data.DataPort.Models;

using Npgsql;

using static LumaCore.Data.DataPort.SqlIdentifierHelper;

namespace LumaCore.Data.DataPort.Export.Implementations;

/// <summary>
/// Reads database content from PostgreSQL for export purposes.
/// </summary>
/// <remarks>
///     <para>
///     This reader establishes a transaction with Repeatable Read isolation to ensure a consistent snapshot.
///     It uses <c>pg_class</c> to get fast row count estimates.
///     </para>
///     <para>
///     All queries are scoped to a single PostgreSQL schema (default: <c>public</c>). The schema is specified
///     at construction time and affects table discovery, column metadata, row streaming, migration history,
///     and row count estimates. Tables in other schemas are not exported.
///     </para>
/// </remarks>
public sealed class PostgresExportReader : IDataExportReader
{
	private readonly string                mConnectionString;
	private readonly string                mSchema;
	private readonly Func<string, string>? mShuttleTypeMapper;
	private          NpgsqlConnection?     mConnection;
	private          NpgsqlTransaction?    mTransaction;
	private          bool                  mDisposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresExportReader"/> class.
	/// </summary>
	/// <param name="connectionString">The PostgreSQL connection string.</param>
	/// <param name="schema">
	/// The PostgreSQL schema to export tables from. Defaults to <c>public</c>, which is the default schema
	/// used by EF Core's Npgsql provider. Since PostgreSQL 15, unprivileged users no longer have
	/// <c>CREATE</c> rights on the <c>public</c> schema by default — specify the application schema
	/// explicitly when the database uses a non-default schema.
	/// </param>
	/// <param name="shuttleTypeMapper">
	/// An optional function that maps PostgreSQL type names to SQLite shuttle storage types.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="connectionString"/> or <paramref name="schema"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="connectionString"/> or <paramref name="schema"/> is empty or consists only of
	/// white-space characters.
	/// </exception>
	public PostgresExportReader(
		string                connectionString,
		string                schema            = "public",
		Func<string, string>? shuttleTypeMapper = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		mConnectionString = connectionString;
		mSchema = schema;
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
		mConnection = new NpgsqlConnection(mConnectionString);
		await mConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Start transaction with Repeatable Read isolation for consistent snapshot.
		mTransaction = await mConnection.BeginTransactionAsync(
				               IsolationLevel.RepeatableRead,
				               cancellationToken)
			               .ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		// Query user tables from information_schema.
		var tables = new List<string>();
		var cmd = new NpgsqlCommand(
			"""
			SELECT table_name
			FROM information_schema.tables
			WHERE table_schema = @schema
			  AND table_type = 'BASE TABLE'
			  AND table_name != '__EFMigrationsHistory'
			ORDER BY table_name
			""",
			mConnection,
			mTransaction);
		cmd.Parameters.AddWithValue("schema", mSchema);
		try
		{
			NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

		// Get estimated row count from pg_class (fast statistics).
		long estimatedRows = -1;
		try
		{
			var cmd = new NpgsqlCommand(
				"""
				SELECT c.reltuples::bigint
				FROM pg_class c
				JOIN pg_namespace n ON n.oid = c.relnamespace
				WHERE c.relname = @tableName
				  AND c.relkind = 'r'
				  AND n.nspname = @schema
				""",
				mConnection,
				mTransaction);
			try
			{
				cmd.Parameters.AddWithValue("tableName", tableName);
				cmd.Parameters.AddWithValue("schema", mSchema);
				object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				if (result != null && result != DBNull.Value)
				{
					estimatedRows = (long)result;
					if (estimatedRows < 0) estimatedRows = 0; // Sanity check if ANALYZE never ran
				}
			}
			finally
			{
				await cmd.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch
		{
			estimatedRows = -1; // Fallback on error
		}

		// Return the snapshot with streaming rows.
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
		var checkCmd = new NpgsqlCommand(
			"""
			SELECT EXISTS (
				SELECT FROM information_schema.tables
				WHERE table_schema = @schema
				  AND table_name = '__EFMigrationsHistory'
			)
			""",
			mConnection,
			mTransaction);
		checkCmd.Parameters.AddWithValue("schema", mSchema);
		try
		{
			bool? exists = (bool?)await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (exists != true) return migrations; // Empty list.
		}
		finally
		{
			await checkCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Read migration history.
		var cmd = new NpgsqlCommand(
			$"""
			 SELECT "MigrationId", "ProductVersion"
			 FROM {QuotePostgres(mSchema)}."__EFMigrationsHistory"
			 ORDER BY "MigrationId"
			 """,
			mConnection,
			mTransaction);
		try
		{
			NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

		// Get column info along with primary key status.
		var cmd = new NpgsqlCommand(
			"""
			SELECT
				c.column_name,
				c.data_type,
				c.is_nullable,
				CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key
			FROM information_schema.columns c
			LEFT JOIN (
				SELECT ku.column_name
				FROM information_schema.table_constraints tc
				JOIN information_schema.key_column_usage ku
					ON tc.constraint_name = ku.constraint_name
					AND tc.table_schema = ku.table_schema
				WHERE tc.constraint_type = 'PRIMARY KEY'
				  AND tc.table_schema = @schema
				  AND tc.table_name = @tableName
			) pk ON c.column_name = pk.column_name
			WHERE c.table_schema = @schema
			  AND c.table_name = @tableName
			ORDER BY c.ordinal_position
			""",
			mConnection,
			mTransaction);
		try
		{
			cmd.Parameters.AddWithValue("tableName", tableName);
			cmd.Parameters.AddWithValue("schema", mSchema);
			NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
							ShuttleStorageType = mShuttleTypeMapper?.Invoke(dbType),
							IsNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase),
							IsPrimaryKey = reader.GetBoolean(3)
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
		var cmd = new NpgsqlCommand(
			$"SELECT * FROM {QuotePostgres(mSchema)}.{QuotePostgres(tableName)}",
			mConnection,
			mTransaction);
		try
		{
			NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
