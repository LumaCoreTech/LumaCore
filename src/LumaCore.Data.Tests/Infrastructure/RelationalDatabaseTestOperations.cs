// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.Providers;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Implements <see cref="IDatabaseTestOperations"/> for relational databases using standard SQL with
/// provider-agnostic identifier quoting via <see cref="IDatabaseProviderOperations.QuoteIdentifier"/>.
/// </summary>
/// <remarks>
/// This single implementation covers all supported relational providers (SQLite, PostgreSQL, SQL Server, MySQL)
/// because the SQL statements used (<c>DELETE FROM</c>, <c>SELECT COUNT(*)</c>, <c>CREATE TABLE</c>) are part
/// of the SQL standard and the only dialect variation — identifier quoting — is handled by the injected
/// <see cref="IDatabaseProviderOperations"/>.
/// </remarks>
sealed class RelationalDatabaseTestOperations : IDatabaseTestOperations
{
	/// <summary>
	/// The provider-specific operations used for identifier quoting.
	/// </summary>
	private readonly IDatabaseProviderOperations mProviderOperations;

	/// <summary>
	/// Initializes a new instance of the <see cref="RelationalDatabaseTestOperations"/> class.
	/// </summary>
	/// <param name="providerOperations">
	/// The provider-specific operations used for dialect-safe identifier quoting.
	/// </param>
	public RelationalDatabaseTestOperations(IDatabaseProviderOperations providerOperations)
	{
		mProviderOperations = providerOperations;
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
			return Convert.ToInt64(result);
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
}
