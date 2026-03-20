// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.Providers;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// SQL Server implementation of schema discovery for <see cref="IDatabaseTestOperations"/>.
/// Queries <c>INFORMATION_SCHEMA</c> and <c>sys.*</c> views for table and index metadata.
/// </summary>
sealed class SqlServerDatabaseTestOperations : RelationalDatabaseTestOperations
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDatabaseTestOperations"/> class.
	/// </summary>
	/// <param name="providerOperations">The SQL Server provider operations for identifier quoting.</param>
	public SqlServerDatabaseTestOperations(IDatabaseProviderOperations providerOperations)
		: base(providerOperations) { }

	/// <inheritdoc/>
	/// <remarks>
	/// Queries <c>INFORMATION_SCHEMA.TABLES</c> for base tables in the <c>dbo</c> schema, excluding
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
				SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
				WHERE TABLE_SCHEMA = 'dbo'
				  AND TABLE_TYPE = 'BASE TABLE'
				  AND TABLE_NAME NOT LIKE '\_\_EF%' ESCAPE '\'
				ORDER BY TABLE_NAME
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
	/// Queries <c>sys.indexes</c> joined with <c>sys.tables</c>, excluding primary key indexes
	/// (<c>is_primary_key = 1</c>), unique constraint indexes (<c>is_unique_constraint = 1</c>),
	/// system tables (<c>is_ms_shipped = 1</c>), and indexes on EF Core infrastructure tables
	/// (<c>__EFMigrations*</c>).
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
				SELECT i.name
				FROM sys.indexes i
				INNER JOIN sys.tables t ON i.object_id = t.object_id
				WHERE t.is_ms_shipped = 0
				  AND t.name NOT LIKE '\_\_EF%' ESCAPE '\'
				  AND i.name IS NOT NULL
				  AND i.is_primary_key = 0
				  AND i.is_unique_constraint = 0
				ORDER BY i.name
				""";

			return await ReadStringColumnAsync(cmd, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}
}
