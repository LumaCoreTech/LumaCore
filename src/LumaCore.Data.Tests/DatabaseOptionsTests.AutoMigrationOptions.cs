// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using Xunit;

namespace LumaCore.Data.Tests;

public sealed partial class DatabaseOptionsTests
{
	/// <summary>
	/// Verifies that a default-constructed <see cref="DatabaseOptions.AutoMigrationOptions"/> instance has the expected
	/// property defaults.
	/// </summary>
	[Fact]
	public void AutoMigrationOptions_Constructor_Initially_HasExpectedDefaults()
	{
		// Arrange + Act
		var sut = new DatabaseOptions.AutoMigrationOptions();

		// Assert
		AssertAutoMigrationDefaults(sut);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseOptions.Validate"/> succeeds when
	/// <see cref="DatabaseOptions.AutoMigrationOptions.RestoreOnFailure"/> is <see langword="true"/>
	/// and <see cref="DatabaseOptions.AutoMigrationOptions.CreateBackupBeforeMigration"/> is also
	/// <see langword="true"/> (the default). This is the valid configuration.
	/// </summary>
	[Fact]
	public void Validate_WhenRestoreOnFailureWithBackupEnabled_Succeeds()
	{
		// Arrange — defaults: both RestoreOnFailure and CreateBackupBeforeMigration are true.
		var sut = new DatabaseOptions();

		// Act
		List<ValidationResult> results = [..sut.Validate(new ValidationContext(sut))];

		// Assert
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseOptions.Validate"/> succeeds when both
	/// <see cref="DatabaseOptions.AutoMigrationOptions.RestoreOnFailure"/> and
	/// <see cref="DatabaseOptions.AutoMigrationOptions.CreateBackupBeforeMigration"/> are
	/// <see langword="false"/>. Disabling both is a valid (explicit opt-out) configuration.
	/// </summary>
	[Fact]
	public void Validate_WhenBothRestoreAndBackupDisabled_Succeeds()
	{
		// Arrange
		var sut = new DatabaseOptions
		{
			AutoMigration =
			{
				RestoreOnFailure = false,
				CreateBackupBeforeMigration = false
			}
		};

		// Act
		List<ValidationResult> results = [..sut.Validate(new ValidationContext(sut))];

		// Assert
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseOptions.Validate"/> rejects the contradictory configuration where
	/// <see cref="DatabaseOptions.AutoMigrationOptions.RestoreOnFailure"/> is <see langword="true"/> but
	/// <see cref="DatabaseOptions.AutoMigrationOptions.CreateBackupBeforeMigration"/> is <see langword="false"/>.
	/// Automatic restore is impossible without a backup, so this combination is a configuration error.
	/// </summary>
	[Fact]
	public void Validate_WhenRestoreOnFailureWithoutBackup_Fails()
	{
		// Arrange
		var sut = new DatabaseOptions
		{
			AutoMigration =
			{
				RestoreOnFailure = true,
				CreateBackupBeforeMigration = false
			}
		};

		// Act
		List<ValidationResult> results = [..sut.Validate(new ValidationContext(sut))];

		// Assert
		ValidationResult error = Assert.Single(results);
		Assert.Equal(
			"Database:AutoMigration:RestoreOnFailure is enabled but " +
			"Database:AutoMigration:CreateBackupBeforeMigration is disabled. " +
			"Automatic restore requires a backup to restore from. Either enable " +
			"CreateBackupBeforeMigration or disable RestoreOnFailure.",
			error.ErrorMessage);
		Assert.Equal(
			["AutoMigration.RestoreOnFailure", "AutoMigration.CreateBackupBeforeMigration"],
			error.MemberNames);
	}
}
