// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Data.Common;

using LumaCore.Core.Diagnostics;
using LumaCore.Data.Providers;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class SqliteProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.DropSchemaObjectsAsync"/> drops all user tables
	/// from the database.
	/// </summary>
	[Fact]
	public async Task DropSchemaObjectsAsync_WhenCalled_DropsAllUserTables()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		string dbPath = Path.Combine(Path.GetTempPath(), $"sqlite-dropschema-test-{Guid.NewGuid():N}.db");
		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite($"Data Source={dbPath}")
			.Options;
		LumaCoreDbContext dbContext = new(options);
		try
		{
			await dbContext.Database.EnsureCreatedAsync();
			DbConnection connection = dbContext.Database.GetDbConnection();

			// Verify tables exist before drop
			Assert.True(await sut.TableExistsAsync(connection, "Users", CancellationToken.None));

			// Act
			await sut.DropSchemaObjectsAsync(
				dbContext,
				new HashSet<string>(),
				CancellationToken.None,
				NullLogger.Instance);

			// Assert — all user tables should be gone
			Assert.False(await sut.TableExistsAsync(connection, "Users", CancellationToken.None));
			Assert.False(await sut.TableExistsAsync(connection, "Conversations", CancellationToken.None));
		}
		finally
		{
			await dbContext.DisposeAsync();
			SqliteConnection.ClearAllPools();
			try { File.Delete(dbPath); }
			catch
			{
				/* best-effort */
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.DropSchemaObjectsAsync"/> drops user-defined
	/// triggers and views in addition to tables. These object types are queried separately from
	/// <c>sqlite_master</c> and must be removed before the tables they reference.
	/// </summary>
	[Fact]
	public async Task DropSchemaObjectsAsync_WhenTriggersAndViewsExist_DropsAll()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		string dbPath = Path.Combine(Path.GetTempPath(), $"sqlite-dropschema-test-{Guid.NewGuid():N}.db");
		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite($"Data Source={dbPath}")
			.Options;
		LumaCoreDbContext dbContext = new(options);
		try
		{
			await dbContext.Database.EnsureCreatedAsync();
			DbConnection connection = dbContext.Database.GetDbConnection();

			// Create a view and a trigger so that the CollectObjectNamesAsync callbacks execute.
			await dbContext.Database.ExecuteSqlRawAsync("CREATE VIEW TestView AS SELECT 1 AS Value");
			await dbContext.Database.ExecuteSqlRawAsync(
				"""
				CREATE TRIGGER TestTrigger AFTER INSERT ON "Users"
				BEGIN
				  SELECT 1;
				END
				""");

			// Verify they exist before drop
			Assert.True(await ObjectExistsAsync(connection, "view", "TestView"));
			Assert.True(await ObjectExistsAsync(connection, "trigger", "TestTrigger"));

			// Act
			await sut.DropSchemaObjectsAsync(
				dbContext,
				new HashSet<string>(),
				CancellationToken.None,
				NullLogger.Instance);

			// Assert — trigger and view should be gone
			Assert.False(await ObjectExistsAsync(connection, "trigger", "TestTrigger"));
			Assert.False(await ObjectExistsAsync(connection, "view", "TestView"));
			// Assert — tables are also gone
			Assert.False(await sut.TableExistsAsync(connection, "Users", CancellationToken.None));
		}
		finally
		{
			await dbContext.DisposeAsync();
			SqliteConnection.ClearAllPools();
			try { File.Delete(dbPath); }
			catch
			{
				/* best-effort */
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.DropSchemaObjectsAsync"/> preserves tables listed
	/// in <c>tablesToPreserve</c> while dropping all other tables.
	/// </summary>
	[Fact]
	public async Task DropSchemaObjectsAsync_WhenPreserveSpecified_PreservesListedTables()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		string dbPath = Path.Combine(Path.GetTempPath(), $"sqlite-dropschema-test-{Guid.NewGuid():N}.db");
		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite($"Data Source={dbPath}")
			.Options;
		LumaCoreDbContext dbContext = new(options);
		try
		{
			await dbContext.Database.EnsureCreatedAsync();
			DbConnection connection = dbContext.Database.GetDbConnection();

			// Create an additional table to preserve
			await dbContext.Database.ExecuteSqlRawAsync("CREATE TABLE __RestoreCheckpoint (Id INTEGER PRIMARY KEY)");

			// Act — preserve the checkpoint table
			await sut.DropSchemaObjectsAsync(
				dbContext,
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "__RestoreCheckpoint" },
				CancellationToken.None);

			// Assert — preserved table still exists
			Assert.True(await sut.TableExistsAsync(connection, "__RestoreCheckpoint", CancellationToken.None));
			// Assert — other tables are gone
			Assert.False(await sut.TableExistsAsync(connection, "Users", CancellationToken.None));
		}
		finally
		{
			await dbContext.DisposeAsync();
			SqliteConnection.ClearAllPools();
			try { File.Delete(dbPath); }
			catch
			{
				/* best-effort */
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.DropSchemaObjectsAsync"/> completes successfully
	/// when <c>VACUUM</c> fails with a non-cancellation exception. The schema cleanup itself must have
	/// already succeeded — only the optional disk-space reclamation is affected.
	/// </summary>
	[Fact]
	public async Task DropSchemaObjectsAsync_WhenVacuumFails_CompletesSuccessfully()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		string dbPath = Path.Combine(Path.GetTempPath(), $"sqlite-dropschema-test-{Guid.NewGuid():N}.db");
		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite($"Data Source={dbPath}")
			.Options;
		LumaCoreDbContext dbContext = new(options);
		try
		{
			await dbContext.Database.EnsureCreatedAsync();
			DbConnection connection = dbContext.Database.GetDbConnection();

			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt(
					"SqliteProviderOperations.BeforeVacuum",
					new InvalidOperationException("Simulated VACUUM failure"));

			// Act — should complete despite VACUUM failure
			await sut.DropSchemaObjectsAsync(
				dbContext,
				new HashSet<string>(),
				CancellationToken.None,
				NullLogger.Instance);

			// Assert — tables were dropped (cleanup succeeded before VACUUM stage)
			Assert.False(await sut.TableExistsAsync(connection, "Users", CancellationToken.None));
		}
		finally
		{
			await dbContext.DisposeAsync();
			SqliteConnection.ClearAllPools();
			try { File.Delete(dbPath); }
			catch
			{
				/* best-effort */
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.DropSchemaObjectsAsync"/> propagates
	/// <see cref="OperationCanceledException"/> from the <c>VACUUM</c> stage instead of swallowing it.
	/// Cancellation exceptions are treated differently from generic failures — they must be rethrown.
	/// </summary>
	[Fact]
	public async Task DropSchemaObjectsAsync_WhenVacuumCancelled_PropagatesCancellation()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		string dbPath = Path.Combine(Path.GetTempPath(), $"sqlite-dropschema-test-{Guid.NewGuid():N}.db");
		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite($"Data Source={dbPath}")
			.Options;
		LumaCoreDbContext dbContext = new(options);
		try
		{
			await dbContext.Database.EnsureCreatedAsync();
			DbConnection connection = dbContext.Database.GetDbConnection();

			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt(
					"SqliteProviderOperations.BeforeVacuum",
					new OperationCanceledException());

			// Act + Assert — OperationCanceledException must propagate
			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				sut.DropSchemaObjectsAsync(dbContext, new HashSet<string>(), CancellationToken.None));

			// Assert — tables were still dropped (cleanup completed before VACUUM stage)
			Assert.False(await sut.TableExistsAsync(connection, "Users", CancellationToken.None));
		}
		finally
		{
			await dbContext.DisposeAsync();
			SqliteConnection.ClearAllPools();
			try { File.Delete(dbPath); }
			catch
			{
				/* best-effort */
			}
		}
	}

	/// <summary>
	/// Checks whether a named object of the given <paramref name="type"/> exists in <c>sqlite_master</c>.
	/// </summary>
	/// <param name="connection">An open database connection.</param>
	/// <param name="type">The SQLite object type (e.g., <c>view</c>, <c>trigger</c>).</param>
	/// <param name="name">The object name to look up.</param>
	/// <returns><see langword="true"/> if the object exists; otherwise, <see langword="false"/>.</returns>
	private static async Task<bool> ObjectExistsAsync(DbConnection connection, string type, string name)
	{
		if (connection.State != ConnectionState.Open)
			await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = @type AND name = @name";

			DbParameter typeParam = cmd.CreateParameter();
			typeParam.ParameterName = "@type";
			typeParam.Value = type;
			cmd.Parameters.Add(typeParam);

			DbParameter nameParam = cmd.CreateParameter();
			nameParam.ParameterName = "@name";
			nameParam.Value = name;
			cmd.Parameters.Add(nameParam);

			object? result = await cmd.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
			return result is not null;
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}
}
