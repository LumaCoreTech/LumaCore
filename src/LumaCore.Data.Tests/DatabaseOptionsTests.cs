// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Data.Tests;

/// <summary>
/// Unit tests for <see cref="DatabaseOptions"/> and its nested option types.
/// </summary>
/// <remarks>
/// These tests verify the null-guard init setters on sub-option properties
/// (<see cref="DatabaseOptions.AutoMigration"/>, <see cref="DatabaseOptions.Recovery"/>,
/// <see cref="DatabaseOptions.UserDeletion"/>), default values, and the constant
/// <see cref="DatabaseOptions.SectionName"/>.
/// </remarks>
public sealed partial class DatabaseOptionsTests
{
	#region Constructor

	/// <summary>
	/// Verifies that a default-constructed <see cref="DatabaseOptions"/> instance has the expected property defaults
	/// across all scalar properties and nested option types.
	/// </summary>
	[Fact]
	public void Constructor_Initially_HasExpectedDefaults()
	{
		// Arrange + Act
		var sut = new DatabaseOptions();

		// Assert — scalar properties
		Assert.True(sut.AutoCreate);
		Assert.True(sut.CleanupConversationsWithNoUsersOnStartup);
		Assert.Equal("Data Source=lumacore.db", sut.ConnectionString);
		Assert.Equal("SELECT 1", sut.HealthQuery);
		Assert.False(sut.PreferCompiledHotPathQueries);
		Assert.Equal("sqlite", sut.Provider);
		Assert.False(sut.StoreFullPrompts);
		Assert.Equal(string.Empty, sut.EncryptionKey);
		Assert.Empty(sut.PreviousEncryptionKeys);
		Assert.False(sut.RequireSnapshotIsolationForExport);

		// Assert — nested option defaults
		AssertAutoMigrationDefaults(sut.AutoMigration);
		AssertRecoveryDefaults(sut.Recovery);
		AssertUserDeletionDefaults(sut.UserDeletion);
	}

	#endregion

	#region SectionName

	/// <summary>
	/// Verifies that <see cref="DatabaseOptions.SectionName"/> is <c>"Database"</c>.
	/// </summary>
	[Fact]
	public void SectionName_Always_ReturnsDatabase()
	{
		// Act + Assert
		Assert.Equal("Database", DatabaseOptions.SectionName);
	}

	#endregion

	#region AutoMigration

	/// <summary>
	/// Verifies that setting <see cref="DatabaseOptions.AutoMigration"/> to a non-null instance stores the value.
	/// </summary>
	[Fact]
	public void AutoMigration_WhenSetToNonNull_StoresValue()
	{
		// Arrange
		var expected = new DatabaseOptions.AutoMigrationOptions { Enabled = false };

		// Act
		var sut = new DatabaseOptions { AutoMigration = expected };

		// Assert
		Assert.Same(expected, sut.AutoMigration);
	}

	/// <summary>
	/// Verifies that setting <see cref="DatabaseOptions.AutoMigration"/> to <see langword="null"/> throws
	/// <see cref="ArgumentNullException"/>.
	/// </summary>
	[Fact]
	public void AutoMigration_WhenSetToNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new DatabaseOptions { AutoMigration = null! });
		Assert.Equal("value", ex.ParamName);
	}

	#endregion

	#region Recovery

	/// <summary>
	/// Verifies that setting <see cref="DatabaseOptions.Recovery"/> to a non-null instance stores the value.
	/// </summary>
	[Fact]
	public void Recovery_WhenSetToNonNull_StoresValue()
	{
		// Arrange
		var expected = new DatabaseOptions.RecoveryOptions { Enabled = false };

		// Act
		var sut = new DatabaseOptions { Recovery = expected };

		// Assert
		Assert.Same(expected, sut.Recovery);
	}

	/// <summary>
	/// Verifies that setting <see cref="DatabaseOptions.Recovery"/> to <see langword="null"/> throws
	/// <see cref="ArgumentNullException"/>.
	/// </summary>
	[Fact]
	public void Recovery_WhenSetToNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new DatabaseOptions { Recovery = null! });
		Assert.Equal("value", ex.ParamName);
	}

	#endregion

	#region UserDeletion

	/// <summary>
	/// Verifies that setting <see cref="DatabaseOptions.UserDeletion"/> to a non-null instance stores the value.
	/// </summary>
	[Fact]
	public void UserDeletion_WhenSetToNonNull_StoresValue()
	{
		// Arrange
		var expected = new DatabaseOptions.UserDeletionOptions { RedactMessages = false };

		// Act
		var sut = new DatabaseOptions { UserDeletion = expected };

		// Assert
		Assert.Same(expected, sut.UserDeletion);
	}

	/// <summary>
	/// Verifies that setting <see cref="DatabaseOptions.UserDeletion"/> to <see langword="null"/> throws
	/// <see cref="ArgumentNullException"/>.
	/// </summary>
	[Fact]
	public void UserDeletion_WhenSetToNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new DatabaseOptions { UserDeletion = null! });
		Assert.Equal("value", ex.ParamName);
	}

	#endregion
}
