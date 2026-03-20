// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.Providers;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// PostgreSQL implementation of schema discovery for <see cref="IDatabaseTestOperations"/>.
/// Queries <c>information_schema</c> and <c>pg_catalog</c> for table and index metadata.
/// </summary>
sealed class PostgreSqlDatabaseTestOperations : RelationalDatabaseTestOperations
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PostgreSqlDatabaseTestOperations"/> class.
	/// </summary>
	/// <param name="providerOperations">The PostgreSQL provider operations for identifier quoting.</param>
	public PostgreSqlDatabaseTestOperations(IDatabaseProviderOperations providerOperations)
		: base(providerOperations) { }

	/// <inheritdoc/>
	/// <remarks>
	/// Queries <c>information_schema.tables</c> for base tables in the <c>public</c> schema, excluding
	/// EF Core infrastructure tables (<c>__EFMigrations*</c>).
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
				SELECT table_name FROM information_schema.tables
				WHERE table_schema = 'public'
				  AND table_type = 'BASE TABLE'
				  AND table_name NOT LIKE '\_\_EF%' ESCAPE '\'
				ORDER BY table_name
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
	/// Queries <c>pg_catalog</c> for indexes in the <c>public</c> schema, LEFT JOINing
	/// <c>pg_constraint</c> to exclude indexes that back a constraint (PK, UNIQUE, FK) — retaining only
	/// standalone indexes. Also excludes indexes on EF Core infrastructure tables (<c>__EFMigrations*</c>).
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
				SELECT ic.relname
				FROM pg_index ix
				JOIN pg_class ic ON ic.oid = ix.indexrelid
				JOIN pg_class tc ON tc.oid = ix.indrelid
				JOIN pg_namespace n ON n.oid = ic.relnamespace
				LEFT JOIN pg_constraint con ON con.conindid = ix.indexrelid
				WHERE n.nspname = 'public'
				  AND con.oid IS NULL
				  AND tc.relname NOT LIKE '\_\_EF%' ESCAPE '\'
				ORDER BY ic.relname
				""";

			return await ReadStringColumnAsync(cmd, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}
}
