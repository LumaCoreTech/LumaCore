// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class DatabaseMaintenanceServiceTests
{
	#region GetBackupDirectory()

	/// <summary>
	/// Verifies that <see cref="DatabaseMaintenanceService.GetBackupDirectory"/> returns the configured
	/// absolute path unchanged when <see cref="DatabaseOptions.AutoMigrationOptions.BackupDirectory"/>
	/// is an absolute (rooted) path.
	/// </summary>
	[Fact]
	public void GetBackupDirectory_WhenAbsolutePathConfigured_ReturnsPathUnchanged()
	{
		// Arrange
		string absolutePath = Path.Combine(Path.GetTempPath(), "LumaCore-Test", "my-backups");
		var options = new DatabaseOptions { AutoMigration = { BackupDirectory = absolutePath } };

		// Act
		string result = DatabaseMaintenanceService.GetBackupDirectory(options);

		// Assert
		Assert.Equal(absolutePath, result);
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseMaintenanceService.GetBackupDirectory"/> resolves a relative
	/// <see cref="DatabaseOptions.AutoMigrationOptions.BackupDirectory"/> against
	/// <see cref="AppContext.BaseDirectory"/>.
	/// </summary>
	[Fact]
	public void GetBackupDirectory_WhenRelativePathConfigured_ResolvesAgainstBaseDirectory()
	{
		// Arrange
		const string relativePath = "data/backups";
		var options = new DatabaseOptions { AutoMigration = { BackupDirectory = relativePath } };
		string expected = Path.Combine(AppContext.BaseDirectory, relativePath);

		// Act
		string result = DatabaseMaintenanceService.GetBackupDirectory(options);

		// Assert
		Assert.Equal(expected, result);
	}

	/// <summary>
	/// Test data for <see cref="GetBackupDirectory_WhenNotConfigured_ReturnsDefaultTempPath"/>:
	/// each row covers a different "not configured" variant of
	/// <see cref="DatabaseOptions.AutoMigrationOptions.BackupDirectory"/>.
	/// </summary>
	public static TheoryData<string, string?> GetBackupDirectory_NotConfigured_Data => new()
	{
		// null (default)
		{ "null", null },

		// empty string
		{ "empty", "" },

		// whitespace only
		{ "whitespace", "   " }
	};

	/// <summary>
	/// Verifies that <see cref="DatabaseMaintenanceService.GetBackupDirectory"/> falls back to the default
	/// temp-based path when <see cref="DatabaseOptions.AutoMigrationOptions.BackupDirectory"/> is
	/// <see langword="null"/>, empty, or whitespace-only.
	/// </summary>
	/// <param name="scenario">A human-readable description of the test case.</param>
	/// <param name="backupDirectory">The backup directory value to configure.</param>
	[Theory]
	[MemberData(nameof(GetBackupDirectory_NotConfigured_Data))]
	public void GetBackupDirectory_WhenNotConfigured_ReturnsDefaultTempPath(
		string  scenario,
		string? backupDirectory)
	{
		_ = scenario;

		// Arrange
		var options = new DatabaseOptions { AutoMigration = { BackupDirectory = backupDirectory } };
		string expected = Path.Combine(Path.GetTempPath(), "LumaCore", "backups");

		// Act
		string result = DatabaseMaintenanceService.GetBackupDirectory(options);

		// Assert
		Assert.Equal(expected, result);
	}

	#endregion
}
