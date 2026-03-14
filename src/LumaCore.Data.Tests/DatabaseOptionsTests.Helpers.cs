// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Data.Tests;

public sealed partial class DatabaseOptionsTests
{
	/// <summary>
	/// Asserts that all properties of an <see cref="DatabaseOptions.AutoMigrationOptions"/> instance have their expected
	/// default values.
	/// </summary>
	/// <param name="options">The instance to verify.</param>
	private static void AssertAutoMigrationDefaults(DatabaseOptions.AutoMigrationOptions options)
	{
		Assert.True(options.Enabled);
		Assert.True(options.CreateBackupBeforeMigration);
		Assert.True(options.RestoreOnFailure);
		Assert.Equal(7, options.BackupRetentionDays);
		Assert.Null(options.BackupDirectory);
	}

	/// <summary>
	/// Asserts that all properties of a <see cref="DatabaseOptions.RecoveryOptions"/> instance have their expected default
	/// values.
	/// </summary>
	/// <param name="options">The instance to verify.</param>
	private static void AssertRecoveryDefaults(DatabaseOptions.RecoveryOptions options)
	{
		Assert.True(options.Enabled);
		Assert.Equal(10, options.PollingIntervalSeconds);
		Assert.Equal(3, options.FailureThreshold);
		Assert.Equal(30, options.FailureWindowSeconds);
	}

	/// <summary>
	/// Asserts that all properties of a <see cref="DatabaseOptions.UserDeletionOptions"/> instance have their expected
	/// default values.
	/// </summary>
	/// <param name="options">The instance to verify.</param>
	private static void AssertUserDeletionDefaults(DatabaseOptions.UserDeletionOptions options)
	{
		Assert.True(options.DeletePrivateConversations);
		Assert.True(options.RedactMessages);
	}
}
