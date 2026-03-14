// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.DataPort;

/// <summary>
/// Provides helper methods for quoting SQL identifiers across different database providers.
/// </summary>
static class SqlIdentifierHelper
{
	/// <summary>
	/// Quotes an identifier (table or column name) for use in an SQLite query.
	/// </summary>
	/// <param name="identifier">The identifier to quote.</param>
	/// <returns>The quoted identifier with embedded double-quotes escaped.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="identifier"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// SQLite uses double-quotes for identifier quoting. Any embedded double-quotes
	/// in the identifier are escaped by doubling them.
	/// </remarks>
	internal static string QuoteSqlite(string identifier)
	{
		ArgumentNullException.ThrowIfNull(identifier);
		return $"\"{identifier.Replace("\"", "\"\"")}\"";
	}

	/// <summary>
	/// Quotes an identifier (table or column name) for use in a PostgreSQL query.
	/// </summary>
	/// <param name="identifier">The identifier to quote.</param>
	/// <returns>The quoted identifier with embedded double-quotes escaped.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="identifier"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// PostgreSQL uses double-quotes for identifier quoting. Any embedded double-quotes
	/// in the identifier are escaped by doubling them.
	/// </remarks>
	internal static string QuotePostgres(string identifier)
	{
		ArgumentNullException.ThrowIfNull(identifier);
		return $"\"{identifier.Replace("\"", "\"\"")}\"";
	}

	/// <summary>
	/// Quotes an identifier (table or column name) for use in a SQL Server query.
	/// </summary>
	/// <param name="identifier">The identifier to quote.</param>
	/// <returns>The quoted identifier with embedded brackets escaped.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="identifier"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// SQL Server uses square brackets for identifier quoting. Any embedded closing
	/// brackets in the identifier are escaped by doubling them.
	/// </remarks>
	internal static string QuoteSqlServer(string identifier)
	{
		ArgumentNullException.ThrowIfNull(identifier);
		return $"[{identifier.Replace("]", "]]")}]";
	}

	/// <summary>
	/// Quotes an identifier (table or column name) for use in a MySQL query.
	/// </summary>
	/// <param name="identifier">The identifier to quote.</param>
	/// <returns>The quoted identifier with embedded backticks escaped.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="identifier"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// MySQL uses backticks for identifier quoting. Any embedded backticks
	/// in the identifier are escaped by doubling them.
	/// </remarks>
	internal static string QuoteMySql(string identifier)
	{
		ArgumentNullException.ThrowIfNull(identifier);
		return $"`{identifier.Replace("`", "``")}`";
	}
}
