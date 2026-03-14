// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class PostgreSqlProviderOperationsTests
{
	/// <summary>
	/// Test data for <see cref="MapToShuttleStorageType_WhenCalled_ReturnsCorrectStorageType"/>.
	/// Covers all type mapping branches including the default TEXT fallback.
	/// </summary>
	public static TheoryData<string, string, string> MapToShuttleStorageType_TestData() => new()
	{
		// --- TEXT ---
		// Exact match: text
		{ "text", "text", "TEXT" },
		// StartsWith: varchar with length
		{ "varchar(255)", "varchar(255)", "TEXT" },
		// StartsWith: character varying (matches StartsWith 'char')
		{ "character varying", "character varying", "TEXT" },
		// Timestamp with time zone (StartsWith 'timestamp')
		{ "timestamp with time zone", "timestamp with time zone", "TEXT" },
		// Exact match: timestamptz
		{ "timestamptz", "timestamptz", "TEXT" },
		// Exact match: date
		{ "date", "date", "TEXT" },
		// Exact match: time
		{ "time", "time", "TEXT" },
		// Exact match: uuid
		{ "uuid", "uuid", "TEXT" },

		// --- INTEGER ---
		// Exact match: integer
		{ "integer", "integer", "INTEGER" },
		// Exact match: int (alias)
		{ "int", "int", "INTEGER" },
		// Exact match: int4 (alias)
		{ "int4", "int4", "INTEGER" },
		// Exact match: smallint
		{ "smallint", "smallint", "INTEGER" },
		// Exact match: bigint
		{ "bigint", "bigint", "INTEGER" },
		// Exact match: int8 (alias)
		{ "int8", "int8", "INTEGER" },
		// Exact match: serial (auto-increment alias)
		{ "serial", "serial", "INTEGER" },
		// Exact match: bigserial
		{ "bigserial", "bigserial", "INTEGER" },
		// Exact match: boolean → INTEGER (0/1)
		{ "boolean", "boolean", "INTEGER" },

		// --- REAL ---
		// Exact match: real
		{ "real", "real", "REAL" },
		// Exact match: double precision
		{ "double precision", "double precision", "REAL" },
		// Exact match: float
		{ "float", "float", "REAL" },

		// --- NUMERIC ---
		// StartsWith: numeric with precision/scale
		{ "numeric(18,2)", "numeric(18,2)", "NUMERIC" },
		// StartsWith: decimal with precision/scale
		{ "decimal(10,2)", "decimal(10,2)", "NUMERIC" },
		// Exact match: money
		{ "money", "money", "NUMERIC" },

		// --- BLOB ---
		// Exact match: bytea
		{ "bytea", "bytea", "BLOB" },

		// --- Default fallback ---
		// Unknown type falls back to TEXT
		{ "Unknown type (json)", "json", "TEXT" },

		// --- Case insensitivity ---
		// Uppercase input is lowered internally
		{ "Case insensitive (INTEGER)", "INTEGER", "INTEGER" }
	};

	/// <summary>
	/// Verifies that <see cref="PostgreSqlProviderOperations.MapToShuttleStorageType"/> maps
	/// PostgreSQL-specific type names to the correct SQLite storage types.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The PostgreSQL type name to map.</param>
	/// <param name="expected">The expected SQLite storage type.</param>
	[Theory]
	[MemberData(nameof(MapToShuttleStorageType_TestData))]
	public void MapToShuttleStorageType_WhenCalled_ReturnsCorrectStorageType(
		string scenario,
		string input,
		string expected)
	{
		_ = scenario;

		// Arrange
		var sut = new PostgreSqlProviderOperations();

		// Act
		string result = sut.MapToShuttleStorageType(input);

		// Assert
		Assert.Equal(expected, result);
	}
}
