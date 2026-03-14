// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class SqlServerProviderOperationsTests
{
	/// <summary>
	/// Test data for <see cref="MapToShuttleStorageType_WhenCalled_ReturnsCorrectStorageType"/>.
	/// Covers all type mapping branches including the default TEXT fallback.
	/// </summary>
	public static TheoryData<string, string, string> MapToShuttleStorageType_TestData() => new()
	{
		// --- TEXT ---
		// StartsWith: nvarchar (Unicode variable-length)
		{ "nvarchar(max)", "nvarchar(max)", "TEXT" },
		// StartsWith: nchar (Unicode fixed-length)
		{ "nchar(10)", "nchar(10)", "TEXT" },
		// StartsWith: varchar (non-Unicode variable-length)
		{ "varchar(255)", "varchar(255)", "TEXT" },
		// StartsWith: char (non-Unicode fixed-length)
		{ "char(10)", "char(10)", "TEXT" },
		// Exact match: text (legacy)
		{ "text", "text", "TEXT" },
		// Exact match: ntext (legacy Unicode)
		{ "ntext", "ntext", "TEXT" },
		// Exact match: xml
		{ "xml", "xml", "TEXT" },
		// StartsWith: datetime (legacy, deprecated — 3.33ms precision, replaced by datetime2)
		{ "datetime", "datetime", "TEXT" },
		// StartsWith: datetime2 (modern replacement with 100ns precision)
		{ "datetime2", "datetime2", "TEXT" },
		// Exact match: datetimeoffset
		{ "datetimeoffset", "datetimeoffset", "TEXT" },
		// Exact match: date
		{ "date", "date", "TEXT" },
		// Exact match: time
		{ "time", "time", "TEXT" },
		// Exact match: smalldatetime
		{ "smalldatetime", "smalldatetime", "TEXT" },
		// Exact match: uniqueidentifier (GUID)
		{ "uniqueidentifier", "uniqueidentifier", "TEXT" },

		// --- INTEGER ---
		// Exact match: int
		{ "int", "int", "INTEGER" },
		// Exact match: smallint
		{ "smallint", "smallint", "INTEGER" },
		// Exact match: bigint
		{ "bigint", "bigint", "INTEGER" },
		// Exact match: tinyint
		{ "tinyint", "tinyint", "INTEGER" },
		// Exact match: bit → INTEGER (0/1)
		{ "bit", "bit", "INTEGER" },

		// --- REAL ---
		// Exact match: real
		{ "real", "real", "REAL" },
		// Exact match: float
		{ "float", "float", "REAL" },

		// --- NUMERIC ---
		// StartsWith: decimal with precision/scale
		{ "decimal(18,2)", "decimal(18,2)", "NUMERIC" },
		// StartsWith: numeric with precision/scale
		{ "numeric(10,4)", "numeric(10,4)", "NUMERIC" },
		// Exact match: money
		{ "money", "money", "NUMERIC" },
		// Exact match: smallmoney
		{ "smallmoney", "smallmoney", "NUMERIC" },

		// --- BLOB ---
		// StartsWith: varbinary
		{ "varbinary(max)", "varbinary(max)", "BLOB" },
		// StartsWith: binary (fixed-length)
		{ "binary(16)", "binary(16)", "BLOB" },
		// Exact match: image (legacy)
		{ "image", "image", "BLOB" },

		// --- Default fallback ---
		// Unknown type falls back to TEXT
		{ "Unknown type (hierarchyid)", "hierarchyid", "TEXT" },

		// --- Case insensitivity ---
		// Uppercase input is lowered internally
		{ "Case insensitive (INT)", "INT", "INTEGER" }
	};

	/// <summary>
	/// Verifies that <see cref="SqlServerProviderOperations.MapToShuttleStorageType"/> maps
	/// SQL Server-specific type names to the correct SQLite storage types.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The SQL Server type name to map.</param>
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
		var sut = new SqlServerProviderOperations();

		// Act
		string result = sut.MapToShuttleStorageType(input);

		// Assert
		Assert.Equal(expected, result);
	}
}
