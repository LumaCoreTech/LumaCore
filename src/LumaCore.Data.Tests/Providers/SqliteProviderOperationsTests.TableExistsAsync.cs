// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Microsoft.Data.Sqlite;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class SqliteProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.TableExistsAsync"/> returns <see langword="true"/>
	/// for a table that exists in the database.
	/// </summary>
	[Fact]
	public async Task TableExistsAsync_WhenTableExists_ReturnsTrue()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			await connection.OpenAsync();

			SqliteCommand cmd = connection.CreateCommand();
			try
			{
				cmd.CommandText = "CREATE TABLE TestTable (Id INTEGER PRIMARY KEY)";
				await cmd.ExecuteNonQueryAsync();
			}
			finally
			{
				await cmd.DisposeAsync();
			}

			// Act
			bool result = await sut.TableExistsAsync(connection, "TestTable", CancellationToken.None)
				;

			// Assert
			Assert.True(result);
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.TableExistsAsync"/> returns <see langword="false"/>
	/// for a table that does not exist in the database.
	/// </summary>
	[Fact]
	public async Task TableExistsAsync_WhenTableDoesNotExist_ReturnsFalse()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			await connection.OpenAsync();

			// Act
			bool result = await sut.TableExistsAsync(connection, "NonExistentTable", CancellationToken.None)
				;

			// Assert
			Assert.False(result);
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.TableExistsAsync"/> opens the connection
	/// automatically when it is not yet open.
	/// </summary>
	[Fact]
	public async Task TableExistsAsync_WhenConnectionIsClosed_OpensAutomatically()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			// Act — connection is closed, method should open it
			bool result = await sut.TableExistsAsync(connection, "AnyTable", CancellationToken.None)
				;

			// Assert — no exception, returns false (table doesn't exist in the fresh database)
			Assert.False(result);
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}
}
