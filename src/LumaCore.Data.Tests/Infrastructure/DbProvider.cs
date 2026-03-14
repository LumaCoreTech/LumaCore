// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Database provider selection for integration-style data tests.
/// </summary>
public enum DbProvider
{
	/// <summary>
	/// SQLite in-memory provider (fast, self-contained tests; not identical to production).
	/// </summary>
	SqliteInMemory,

	/// <summary>
	/// SQLite file-based provider (matches production behavior; temporary file per test run).
	/// </summary>
	Sqlite,

	/// <summary>
	/// PostgreSQL provider (requires an external database).
	/// </summary>
	PostgreSql,

	/// <summary>
	/// SQL Server provider (requires an external database).
	/// </summary>
	SqlServer,

	/// <summary>
	/// MySQL/MariaDB provider (currently not supported; retained for wiring/forward compatibility).
	/// </summary>
	MySql
}
