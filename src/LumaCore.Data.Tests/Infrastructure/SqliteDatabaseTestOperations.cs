// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.Providers;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// SQLite implementation of schema discovery for <see cref="IDatabaseTestOperations"/>.
/// Queries <c>sqlite_master</c> for table and index metadata.
/// </summary>
sealed class SqliteDatabaseTestOperations : RelationalDatabaseTestOperations
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteDatabaseTestOperations"/> class.
	/// </summary>
	/// <param name="providerOperations">The SQLite provider operations for identifier quoting.</param>
	public SqliteDatabaseTestOperations(IDatabaseProviderOperations providerOperations)
		: base(providerOperations) { }

	/// <inheritdoc/>
	/// <remarks>
	/// Queries <c>sqlite_master</c> for rows where <c>type = 'table'</c>, excluding SQLite internal tables
	/// (<c>sqlite_*</c>) and EF Core infrastructure tables (<c>__EFMigrations*</c>).
	/// </remarks>
	public override async Task<string[]> GetUserTableNamesAsync(
		DbConnection      connection,
		CancellationToken cancellationToken)
	{
		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText =
				"""
				SELECT name FROM sqlite_master
				WHERE type = 'table'
				  AND name NOT LIKE 'sqlite_%'
				  AND name NOT LIKE '\_\_EF%' ESCAPE '\'
				ORDER BY name
				""";

			return await ReadStringColumnAsync(cmd, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Queries <c>sqlite_master</c> for rows where <c>type = 'index'</c>, excluding auto-generated indexes
	/// for PRIMARY KEY and UNIQUE constraints (<c>sqlite_autoindex_*</c>).
	/// </remarks>
	public override async Task<string[]> GetExplicitIndexNamesAsync(
		DbConnection      connection,
		CancellationToken cancellationToken)
	{
		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText =
				"""
				SELECT name FROM sqlite_master
				WHERE type = 'index'
				  AND name NOT LIKE 'sqlite_autoindex_%'
				ORDER BY name
				""";

			return await ReadStringColumnAsync(cmd, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}
}
