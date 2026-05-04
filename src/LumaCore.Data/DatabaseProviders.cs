// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data;

/// <summary>
/// Database provider identifiers used throughout LumaCore.
/// </summary>
/// <remarks>
/// These constants define the supported database providers for LumaCore.
/// Use these constants instead of string literals to ensure type safety and enable refactoring.
/// </remarks>
public static class DatabaseProviders
{
	/// <summary>
	/// SQLite database provider.
	/// </summary>
	/// <remarks>
	/// SQLite is a lightweight, file-based database ideal for development and small-scale deployments.
	/// </remarks>
	public const string Sqlite = "sqlite";

	/// <summary>
	/// PostgreSQL database provider.
	/// </summary>
	/// <remarks>
	/// PostgreSQL is a powerful, open-source relational database with advanced features.
	/// </remarks>
	public const string PostgreSql = "postgresql";

	/// <summary>
	/// Microsoft SQL Server database provider.
	/// </summary>
	/// <remarks>
	/// SQL Server is Microsoft's enterprise relational database management system.
	/// </remarks>
	public const string SqlServer = "sqlserver";

	/// <summary>
	/// MySQL/MariaDB database provider (requires <c>Pomelo.EntityFrameworkCore.MySql</c>).
	/// </summary>
	/// <remarks>
	///     <para>
	///     MySQL and MariaDB are popular open-source relational databases.
	///     This provider uses <c>Pomelo.EntityFrameworkCore.MySql</c> for optimal performance.
	///     </para>
	///     <para>
	///     <b>Temporarily unavailable:</b> <c>Pomelo.EntityFrameworkCore.MySql</c> has not yet released an
	///     EF Core 10 compatible version. The constant is kept so that configuration, tooling, and the
	///     <see cref="Providers.MySqlProviderOperations"/> implementation remain ready for re-activation.
	///     MySQL is excluded from <see cref="GetSupportedProviders"/>, is hard-rejected by
	///     <see cref="Providers.DatabaseProviderFactory.GetProvider"/>, and is hard-rejected at DbContext
	///     configuration time. Track progress at
	///     <see href="https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues"/>.
	///     </para>
	/// </remarks>
	public const string MySql = "mysql";

	/// <summary>
	/// Gets a comma-separated list of all currently supported (enabled) database providers.
	/// </summary>
	/// <returns>A string containing all enabled provider identifiers, separated by commas and spaces.</returns>
	/// <remarks>
	/// Use this method for error messages and user-facing documentation to ensure consistency.
	/// Providers that are known but temporarily unavailable (e.g., <see cref="MySql"/>) are excluded.
	/// </remarks>
	public static string GetSupportedProviders() => $"{Sqlite}, {PostgreSql}, {SqlServer}";
}
