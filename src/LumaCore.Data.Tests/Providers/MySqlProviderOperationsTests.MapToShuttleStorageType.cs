// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class MySqlProviderOperationsTests
{
	/// <summary>
	/// Test data for <see cref="MapToShuttleStorageType_WhenCalled_ReturnsCorrectStorageType"/>.
	/// Covers all type mapping branches including the default TEXT fallback.
	/// </summary>
	public static TheoryData<string, string, string> MapToShuttleStorageType_TestData() => new()
	{
		// --- TEXT ---
		// StartsWith: varchar with length
		{ "varchar(255)", "varchar(255)", "TEXT" },
		// StartsWith: char with length
		{ "char(36)", "char(36)", "TEXT" },
		// Exact match: text
		{ "text", "text", "TEXT" },
		// Exact match: longtext
		{ "longtext", "longtext", "TEXT" },
		// Exact match: mediumtext
		{ "mediumtext", "mediumtext", "TEXT" },
		// Exact match: tinytext
		{ "tinytext", "tinytext", "TEXT" },
		// Exact match: enum
		{ "enum", "enum", "TEXT" },
		// Exact match: set
		{ "set", "set", "TEXT" },
		// Exact match: datetime
		{ "datetime", "datetime", "TEXT" },
		// Exact match: timestamp
		{ "timestamp", "timestamp", "TEXT" },
		// Exact match: date
		{ "date", "date", "TEXT" },
		// Exact match: time
		{ "time", "time", "TEXT" },
		// Exact match: year
		{ "year", "year", "TEXT" },

		// --- INTEGER ---
		// Exact match: int
		{ "int", "int", "INTEGER" },
		// Exact match: integer
		{ "integer", "integer", "INTEGER" },
		// Exact match: smallint
		{ "smallint", "smallint", "INTEGER" },
		// Exact match: bigint
		{ "bigint", "bigint", "INTEGER" },
		// Exact match: mediumint
		{ "mediumint", "mediumint", "INTEGER" },
		// Exact match: tinyint
		{ "tinyint", "tinyint", "INTEGER" },
		// Exact match: boolean → INTEGER
		{ "boolean", "boolean", "INTEGER" },
		// Exact match: bool → INTEGER (alias for boolean)
		{ "bool", "bool", "INTEGER" },

		// --- REAL ---
		// Exact match: float
		{ "float", "float", "REAL" },
		// Exact match: double
		{ "double", "double", "REAL" },
		// Exact match: real
		{ "real", "real", "REAL" },

		// --- NUMERIC ---
		// StartsWith: decimal with precision/scale
		{ "decimal(10,2)", "decimal(10,2)", "NUMERIC" },
		// StartsWith: numeric with precision/scale
		{ "numeric(18,4)", "numeric(18,4)", "NUMERIC" },

		// --- BLOB ---
		// Exact match: blob
		{ "blob", "blob", "BLOB" },
		// Exact match: longblob
		{ "longblob", "longblob", "BLOB" },
		// Exact match: mediumblob
		{ "mediumblob", "mediumblob", "BLOB" },
		// Exact match: tinyblob
		{ "tinyblob", "tinyblob", "BLOB" },
		// StartsWith: varbinary with length
		{ "varbinary(255)", "varbinary(255)", "BLOB" },
		// StartsWith: binary with length
		{ "binary(16)", "binary(16)", "BLOB" },

		// --- Default fallback ---
		// Unknown type falls back to TEXT
		{ "Unknown type (json)", "json", "TEXT" },

		// --- Case insensitivity ---
		// Uppercase input is lowered internally
		{ "Case insensitive (INT)", "INT", "INTEGER" }
	};

	/// <summary>
	/// Verifies that <see cref="MySqlProviderOperations.MapToShuttleStorageType"/> maps
	/// MySQL-specific type names to the correct SQLite storage types.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The MySQL type name to map.</param>
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
		var sut = new MySqlProviderOperations();

		// Act
		string result = sut.MapToShuttleStorageType(input);

		// Assert
		Assert.Equal(expected, result);
	}
}
