// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

/// <summary>
/// Unit tests for <see cref="RestoreCheckpointData"/>.
/// </summary>
[Trait("Category", "Initialization")]
public sealed class RestoreCheckpointDataTests
{
	#region Constructor

	/// <summary>
	/// Verifies that the constructor stores all positional parameters correctly.
	/// </summary>
	[Fact]
	public void Constructor_WhenValid_SetsAllProperties()
	{
		// Arrange + Act
		var sut = new RestoreCheckpointData(
			ShuttleId: "d290f1ee-6c54-4b01-90e6-d701748f0851",
			BaselineMigrationId: "20260315135847_InitialCreate",
			Phase: RestoreCheckpointData.PhaseImport,
			StartedUtc: "2026-06-15T12:30:45.0000000Z");

		// Assert
		Assert.Equal("d290f1ee-6c54-4b01-90e6-d701748f0851", sut.ShuttleId);
		Assert.Equal("20260315135847_InitialCreate", sut.BaselineMigrationId);
		Assert.Equal(RestoreCheckpointData.PhaseImport, sut.Phase);
		Assert.Equal("2026-06-15T12:30:45.0000000Z", sut.StartedUtc);
	}

	#endregion

	#region Phase Constants

	/// <summary>
	/// Verifies that <see cref="RestoreCheckpointData.PhaseSchemaCleanup"/> has the expected persisted value.
	/// This value is stored in the database and must remain stable across versions.
	/// </summary>
	[Fact]
	public void PhaseSchemaCleanup_Always_HasExpectedValue()
	{
		// Act + Assert
		Assert.Equal("schema_cleanup", RestoreCheckpointData.PhaseSchemaCleanup);
	}

	/// <summary>
	/// Verifies that <see cref="RestoreCheckpointData.PhaseMigration"/> has the expected persisted value.
	/// This value is stored in the database and must remain stable across versions.
	/// </summary>
	[Fact]
	public void PhaseMigration_Always_HasExpectedValue()
	{
		// Act + Assert
		Assert.Equal("migration", RestoreCheckpointData.PhaseMigration);
	}

	/// <summary>
	/// Verifies that <see cref="RestoreCheckpointData.PhaseImport"/> has the expected persisted value.
	/// This value is stored in the database and must remain stable across versions.
	/// </summary>
	[Fact]
	public void PhaseImport_Always_HasExpectedValue()
	{
		// Act + Assert
		Assert.Equal("import", RestoreCheckpointData.PhaseImport);
	}

	#endregion

	#region Record Equality

	/// <summary>
	/// Verifies that two <see cref="RestoreCheckpointData"/> instances with identical values are considered equal
	/// and produce the same hash code.
	/// </summary>
	[Fact]
	public void Equals_WhenSameValues_ReturnsTrue()
	{
		// Arrange
		var a = new RestoreCheckpointData("id-1", "migration-1", "import", "2026-06-15T00:00:00Z");
		var b = new RestoreCheckpointData("id-1", "migration-1", "import", "2026-06-15T00:00:00Z");

		// Act + Assert
		Assert.Equal(a, b);
		Assert.Equal(a.GetHashCode(), b.GetHashCode());
	}

	/// <summary>
	/// Verifies that <see cref="RestoreCheckpointData.GetHashCode"/> returns the same value across multiple
	/// invocations on the same instance.
	/// </summary>
	[Fact]
	public void GetHashCode_WhenCalledMultipleTimes_ReturnsStableValue()
	{
		// Arrange
		var sut = new RestoreCheckpointData("id-1", "migration-1", "import", "2026-06-15T00:00:00Z");

		// Act
		int hash1 = sut.GetHashCode();
		int hash2 = sut.GetHashCode();

		// Assert
		Assert.Equal(hash1, hash2);
	}

	/// <summary>
	/// Verifies that two <see cref="RestoreCheckpointData"/> instances with different values are not equal.
	/// </summary>
	[Fact]
	public void Equals_WhenDifferentValues_ReturnsFalse()
	{
		// Arrange
		var a = new RestoreCheckpointData("id-1", "migration-1", "import", "2026-06-15T00:00:00Z");
		var b = new RestoreCheckpointData("id-2", "migration-1", "import", "2026-06-15T00:00:00Z");

		// Act + Assert
		Assert.NotEqual(a, b);
	}

	#endregion
}
