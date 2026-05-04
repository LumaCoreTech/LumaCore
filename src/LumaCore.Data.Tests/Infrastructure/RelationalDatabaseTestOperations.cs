// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;
using System.Globalization;

using LumaCore.Data.Providers;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Abstract base for <see cref="IDatabaseTestOperations"/> implementations on relational databases.
/// </summary>
/// <remarks>
///     <para>
///     Basic DML operations (<c>DELETE FROM</c>, <c>SELECT COUNT(*)</c>, <c>CREATE TABLE</c>) are implemented
///     here using standard SQL — the only dialect variation (identifier quoting) is handled by the injected
///     <see cref="IDatabaseProviderOperations"/>.
///     </para>
///     <para>
///     Schema discovery operations (<see cref="GetUserTableNamesAsync"/> and
///     <see cref="GetExplicitIndexNamesAsync"/>) are abstract and implemented by provider-specific subclasses
///     (<see cref="SqliteDatabaseTestOperations"/>, <see cref="PostgreSqlDatabaseTestOperations"/>,
///     <see cref="SqlServerDatabaseTestOperations"/>).
///     </para>
/// </remarks>
abstract class RelationalDatabaseTestOperations : IDatabaseTestOperations
{
	/// <summary>
	/// The provider-specific operations used for identifier quoting.
	/// </summary>
	private readonly IDatabaseProviderOperations mProviderOperations;

	/// <summary>
	/// Initializes a new instance of a <see cref="RelationalDatabaseTestOperations"/> subclass.
	/// </summary>
	/// <param name="providerOperations">
	/// The provider-specific operations used for dialect-safe identifier quoting.
	/// </param>
	protected RelationalDatabaseTestOperations(IDatabaseProviderOperations providerOperations)
	{
		mProviderOperations = providerOperations;
	}

	/// <summary>
	/// Creates the appropriate provider-specific <see cref="IDatabaseTestOperations"/> implementation
	/// based on <see cref="IDatabaseProviderOperations.ProviderName"/>.
	/// </summary>
	/// <param name="providerOperations">The production provider operations for the target database.</param>
	/// <returns>A provider-specific test operations instance.</returns>
	/// <exception cref="NotSupportedException">
	/// <paramref name="providerOperations"/> targets an unsupported provider.
	/// </exception>
	public static IDatabaseTestOperations Create(IDatabaseProviderOperations providerOperations)
	{
		return providerOperations.ProviderName switch
		{
			// SQLite in-memory and file-based variants share the same SQL dialect,
			// so they use the same test operations implementation.
			DatabaseProviders.Sqlite     => new SqliteDatabaseTestOperations(providerOperations),
			DatabaseProviders.PostgreSql => new PostgreSqlDatabaseTestOperations(providerOperations),
			DatabaseProviders.SqlServer  => new SqlServerDatabaseTestOperations(providerOperations),
			var _ => throw new NotSupportedException(
				         $"No test operations implementation for provider '{providerOperations.ProviderName}'.")
		};
	}

	/// <inheritdoc/>
	public async Task DeleteAllRowsAsync(
		DbConnection      connection,
		string            tableName,
		CancellationToken cancellationToken)
	{
		string quoted = mProviderOperations.QuoteIdentifier(tableName);
		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = $"DELETE FROM {quoted}";
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task<long> CountRowsAsync(
		DbConnection      connection,
		string            tableName,
		CancellationToken cancellationToken)
	{
		string quoted = mProviderOperations.QuoteIdentifier(tableName);
		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = $"SELECT COUNT(*) FROM {quoted}";
			object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			return Convert.ToInt64(result, CultureInfo.InvariantCulture);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task CreateMinimalTableAsync(
		DbConnection      connection,
		string            tableName,
		CancellationToken cancellationToken)
	{
		string quotedTable = mProviderOperations.QuoteIdentifier(tableName);
		string quotedColumn = mProviderOperations.QuoteIdentifier("Id");
		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = $"CREATE TABLE {quotedTable} ({quotedColumn} INTEGER NOT NULL PRIMARY KEY)";
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public abstract Task<string[]> GetUserTableNamesAsync(DbConnection connection, CancellationToken cancellationToken);

	/// <inheritdoc/>
	public abstract Task<string[]> GetExplicitIndexNamesAsync(
		DbConnection      connection,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes the command and reads all values from the first column as strings.
	/// </summary>
	/// <param name="cmd">A command with <see cref="DbCommand.CommandText"/> already set.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An array of string values from the first column of each row.</returns>
	protected static async Task<string[]> ReadStringColumnAsync(DbCommand cmd, CancellationToken cancellationToken)
	{
		DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var values = new List<string>();
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				values.Add(reader.GetString(0));
			}

			return [.. values];
		}
		finally
		{
			await reader.DisposeAsync().ConfigureAwait(false);
		}
	}
}
