// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class SqliteProviderOperationsTests
{
	/// <summary>
	/// Test data for <see cref="MapToShuttleStorageType_WhenCalled_ReturnsInputUnchanged"/>.
	/// SQLite types are already valid Shuttle storage types, so the method is an identity mapping.
	/// All five SQLite type affinities are covered plus arbitrary inputs to verify pass-through.
	/// </summary>
	public static TheoryData<string, string> MapToShuttleStorageType_TestData() => new()
	{
		// The five SQLite type affinities — these ARE the Shuttle storage types
		{ "Type affinity: TEXT", "TEXT" },
		{ "Type affinity: INTEGER", "INTEGER" },
		{ "Type affinity: REAL", "REAL" },
		{ "Type affinity: NUMERIC", "NUMERIC" },
		{ "Type affinity: BLOB", "BLOB" },

		// Arbitrary type names pass through unchanged (identity mapping, no transformation)
		{ "Arbitrary: VARCHAR(255)", "VARCHAR(255)" },
		{ "Arbitrary: BOOLEAN", "BOOLEAN" },
		{ "Arbitrary: unknown_type", "unknown_type" }
	};

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.MapToShuttleStorageType"/> returns the input unchanged
	/// because SQLite types are already valid Shuttle storage types (identity mapping).
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The SQLite type name to map (also the expected return value).</param>
	[Theory]
	[MemberData(nameof(MapToShuttleStorageType_TestData))]
	public void MapToShuttleStorageType_WhenCalled_ReturnsInputUnchanged(string scenario, string input)
	{
		_ = scenario;

		// Arrange
		var sut = new SqliteProviderOperations();

		// Act
		string result = sut.MapToShuttleStorageType(input);

		// Assert
		Assert.Equal(input, result);
	}
}
