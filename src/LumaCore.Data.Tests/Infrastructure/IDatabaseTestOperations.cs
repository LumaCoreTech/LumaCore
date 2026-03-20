// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.Providers;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Abstracts low-level database manipulation operations needed by integration tests that cannot be expressed
/// through EF Core alone (e.g., operating on non-entity tables or creating arbitrary schema objects).
/// </summary>
/// <remarks>
///     <para>
///     This interface separates test-specific database operations from the production
///     <see cref="IDatabaseProviderOperations"/> interface, keeping the production contract clean while
///     centralizing provider-specific logic in a single implementation per database paradigm.
///     </para>
///     <para>
///     For all supported relational providers, <see cref="RelationalDatabaseTestOperations"/> provides a
///     standard SQL implementation that uses <see cref="IDatabaseProviderOperations.QuoteIdentifier"/> for
///     dialect-safe identifier quoting.
///     </para>
/// </remarks>
interface IDatabaseTestOperations
{
	/// <summary>
	/// Deletes all rows from the specified table.
	/// </summary>
	/// <param name="connection">An open database connection.</param>
	/// <param name="tableName">The unquoted table name.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	Task DeleteAllRowsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken);

	/// <summary>
	/// Returns the number of rows in the specified table.
	/// </summary>
	/// <param name="connection">An open database connection.</param>
	/// <param name="tableName">The unquoted table name.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The row count.</returns>
	Task<long> CountRowsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken);

	/// <summary>
	/// Creates a minimal table with a single <c>INTEGER NOT NULL PRIMARY KEY</c> column named <c>Id</c>.
	/// </summary>
	/// <param name="connection">An open database connection.</param>
	/// <param name="tableName">The unquoted table name to create.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// Used to create schema objects that conflict with EF Core migrations, enabling tests for migration
	/// failure and recovery scenarios.
	/// </remarks>
	Task CreateMinimalTableAsync(DbConnection connection, string tableName, CancellationToken cancellationToken);

	/// <summary>
	/// Returns the names of all user-defined tables, excluding EF Core infrastructure tables
	/// (<c>__EFMigrationsHistory</c>, <c>__EFMigrationsLock</c>) and provider-internal tables.
	/// </summary>
	/// <param name="connection">An open database connection.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An alphabetically sorted array of user table names.</returns>
	/// <remarks>
	/// This is a discovery-based operation — it returns whatever tables exist, making it suitable for
	/// asserting exact schema state after migrations.
	/// </remarks>
	Task<string[]> GetUserTableNamesAsync(DbConnection connection, CancellationToken cancellationToken);

	/// <summary>
	/// Returns the names of all explicitly-created indexes, excluding auto-generated indexes for
	/// PRIMARY KEY and UNIQUE constraints as well as indexes on EF Core infrastructure tables.
	/// </summary>
	/// <param name="connection">An open database connection.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An alphabetically sorted array of explicit index names.</returns>
	/// <remarks>
	/// This is a discovery-based operation — it returns whatever explicit indexes exist. Each provider
	/// determines "auto-generated" differently; see the implementation for provider-specific filtering.
	/// </remarks>
	Task<string[]> GetExplicitIndexNamesAsync(DbConnection connection, CancellationToken cancellationToken);
}
