// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Xunit;

namespace LumaCore.Data.Tests;

// Model metadata: provider-specific configuration that survives the round-trip from
// OnModelCreating into the runtime IModel.
//
// These tests pin down the rare bits of the model that differ between providers (or that
// would silently lose a default value if the configuration regressed):
//
//   1. Users.Email unique index filter — must be ANSI ("Email" IS NOT NULL) on SQLite/PostgreSQL
//      and bracket-quoted ([Email] IS NOT NULL) on SQL Server. A wrong filter would either reject
//      multiple NULL emails (data loss for users without an address) or be ignored entirely
//      (silent uniqueness regression).
//
//   2. MessageEntity.Type default value — must remain MessageType.User so that the database
//      default and the model default agree; otherwise inserts that omit Type would write a
//      different role than the application expects.

public sealed partial class LumaCoreDbContextTests
{
	#region Users.Email index filter

	/// <summary>
	/// Verifies that the <c>Users.Email</c> unique index uses the ANSI filter syntax
	/// (<c>"Email" IS NOT NULL</c>) when the runtime model is built for SQLite. The same syntax also
	/// applies to PostgreSQL.
	/// </summary>
	[Fact]
	public void Model_WhenProviderIsSqlite_UsesAnsiEmailIndexFilter()
	{
		// Arrange
		IReadOnlyIndex emailIndex = mFixture.DbContext.Model
			.FindEntityType(typeof(UserEntity))!
			.GetIndexes()
			.Single(index => index.GetDatabaseName() == "IX_Users_Email");

		// Act
		string? filter = emailIndex.GetFilter();

		// Assert
		Assert.Equal("\"Email\" IS NOT NULL", filter);
	}

	/// <summary>
	/// Verifies that the <c>Users.Email</c> unique index uses SQL Server filter syntax
	/// (<c>[Email] IS NOT NULL</c>) when the runtime model is built for SQL Server.
	/// </summary>
	[Fact]
	public void Model_WhenProviderIsSqlServer_UsesSqlServerEmailIndexFilter()
	{
		// Arrange
		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlServer(
				"Server=(localdb)\\mssqllocaldb;Database=LumaCoreModelMetadataTests;Trusted_Connection=True;TrustServerCertificate=True")
			.Options;

		using var sut = new LumaCoreDbContext(options);
		IReadOnlyIndex emailIndex = sut.Model
			.FindEntityType(typeof(UserEntity))!
			.GetIndexes()
			.Single(index => index.GetDatabaseName() == "IX_Users_Email");

		// Act
		string? filter = emailIndex.GetFilter();

		// Assert
		Assert.Equal("[Email] IS NOT NULL", filter);
	}

	#endregion

	#region MessageEntity.Type default value

	/// <summary>
	/// Verifies that the <see cref="MessageEntity.Type"/> column retains the database default value
	/// (<see cref="MessageType.User"/>) declared by the model.
	/// </summary>
	[Fact]
	public void Model_WhenInspectingMessageType_HasUserDefaultValue()
	{
		// Arrange
		IProperty typeProperty = mFixture.DbContext.Model
			.FindEntityType(typeof(MessageEntity))!
			.FindProperty(nameof(MessageEntity.Type))!;

		// Act
		object? defaultValue = typeProperty.GetDefaultValue();

		// Assert
		Assert.Equal(MessageType.User, Assert.IsType<MessageType>(defaultValue));
	}

	#endregion
}
