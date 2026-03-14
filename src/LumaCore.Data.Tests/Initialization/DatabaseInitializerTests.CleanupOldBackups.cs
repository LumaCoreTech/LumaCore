// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Initialization;
using LumaCore.Core.IO;

using Microsoft.Data.Sqlite;

using Xunit;

// ReSharper disable AccessToDisposedClosure

namespace LumaCore.Data.Tests.Initialization;

// CleanupOldBackupsAsync(): retention-based deletion, metadata vs. filesystem timestamp handling,
// corrupt-file fallback, missing-metadata fallback, per-file error isolation, and outer-catch
// resilience (generic exceptions swallowed to avoid blocking startup).
public sealed partial class DatabaseInitializerTests
{
	#region CleanupOldBackupsAsync()

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> deletes backup files older than
	/// the configured retention period while preserving newer files. File age is determined by the
	/// <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata embedded in the Shuttle file.
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenOldFilesExist_DeletesOnlyOldFiles()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("cleanup-test");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			DateTimeOffset now = harness.TimeProvider.GetUtcNow();

			// Create valid shuttle files with embedded CreatedUtc metadata.
			string oldFile = Path.Combine(tempDir.Path, "old-backup.shuttle.sqlite");
			string newFile = Path.Combine(tempDir.Path, "new-backup.shuttle.sqlite");
			await TestHarness.CreateMinimalShuttleFileAsync(oldFile, now.AddDays(-8));
			await TestHarness.CreateMinimalShuttleFileAsync(newFile, now);

			// Act
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert
			Assert.False(File.Exists(oldFile));
			Assert.True(File.Exists(newFile));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> ignores files that do not match
	/// the LumaCore Shuttle file extension (<c>.shuttle.sqlite</c>).
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenNonShuttleFilesExist_IgnoresThem()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("cleanup-test");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			// Create an old non-shuttle file (beyond retention) that should not be deleted.
			string nonShuttleFile = Path.Combine(tempDir.Path, "data.db");
			await File.WriteAllTextAsync(nonShuttleFile, "important");
			File.SetLastWriteTimeUtc(nonShuttleFile, harness.TimeProvider.GetUtcNow().DateTime.AddDays(-8));

			// Act
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — non-shuttle file is preserved
			Assert.True(File.Exists(nonShuttleFile));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> does not throw when the
	/// configured backup directory does not exist (graceful no-op).
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenDirectoryDoesNotExist_DoesNotThrow()
	{
		// Arrange — point to a non-existent directory
		string nonExistentDir = Path.Combine(Path.GetTempPath(), $"cleanup-missing-{Guid.NewGuid():N}");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = nonExistentDir;
		});
		try
		{
			// Act — should be a no-op, not throw
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — no-op completed and the initialization status is unchanged.
			Assert.False(Directory.Exists(nonExistentDir));
			AssertNotStartedStatus(harness.Status);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> preserves all files when none
	/// exceed the retention period. File age is determined by the embedded
	/// <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata.
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenAllFilesAreRecent_PreservesAll()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("cleanup-test");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			DateTimeOffset now = harness.TimeProvider.GetUtcNow();

			// Create valid shuttle files with recent CreatedUtc metadata.
			string file1 = Path.Combine(tempDir.Path, "recent1.shuttle.sqlite");
			string file2 = Path.Combine(tempDir.Path, "recent2.shuttle.sqlite");
			await TestHarness.CreateMinimalShuttleFileAsync(file1, now.AddDays(-3));
			await TestHarness.CreateMinimalShuttleFileAsync(file2, now.AddDays(-1));

			// Act
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — both files preserved
			Assert.True(File.Exists(file1));
			Assert.True(File.Exists(file2));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> does not propagate exceptions when
	/// file deletion fails (e.g., due to file locks or I/O errors) and continues processing subsequent files.
	/// Uses <see cref="ExecutionStageMonitor"/> to inject an <see cref="IOException"/> on the first
	/// <c>CleanupOldBackups.BeforeDelete</c> stage only — deterministic and cross-platform (unlike
	/// <c>FileAttributes.ReadOnly</c> which only prevents deletion on Windows). The second file is
	/// actually deleted, proving the loop continues past the failure.
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenDeletionFails_DoesNotThrow()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("cleanup-test");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			DateTimeOffset now = harness.TimeProvider.GetUtcNow();

			string file1 = Path.Combine(tempDir.Path, "old1.shuttle.sqlite");
			string file2 = Path.Combine(tempDir.Path, "old2.shuttle.sqlite");
			await TestHarness.CreateMinimalShuttleFileAsync(file1, now.AddDays(-8));
			await TestHarness.CreateMinimalShuttleFileAsync(file2, now.AddDays(-8));

			// Inject an IOException on the first delete attempt only. The second attempt
			// proceeds normally, so one file is actually deleted — proving error isolation.
			// Directory.EnumerateFiles() order is unspecified, so we don't know which file
			// hits the fault; the assertion checks that exactly one survived.
			int deleteAttempts = 0;
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.OnStage(
					"CleanupOldBackups.BeforeDelete",
					() =>
					{
						if (Interlocked.Increment(ref deleteAttempts) == 1)
							throw new IOException("Simulated delete failure");
					});

			// Act — should not throw despite the deletion failure on the first file.
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — both files were attempted (loop continued past the first failure).
			Assert.Equal(2, deleteAttempts);

			// Exactly one file survived (failed delete) and one was cleaned up (successful delete).
			// We don't assert which is which because enumeration order is unspecified.
			bool file1Exists = File.Exists(file1);
			bool file2Exists = File.Exists(file2);
			Assert.NotEqual(file1Exists, file2Exists);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> uses the embedded
	/// <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata to determine file age, even when the
	/// file system last-write timestamp indicates a different age. This ensures retention decisions are
	/// immune to file copy/move operations that reset <c>FileInfo.LastWriteTimeUtc</c>.
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenMetadataDisagreesWithFilesystem_UsesMetadata()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("cleanup-test");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			DateTimeOffset now = harness.TimeProvider.GetUtcNow();

			// Create a shuttle file whose metadata says "8 days old" (beyond retention)
			// but whose filesystem timestamp says "1 day old" (within retention).
			// The method must trust the metadata and delete the file.
			string file = Path.Combine(tempDir.Path, "metadata-old.shuttle.sqlite");
			await TestHarness.CreateMinimalShuttleFileAsync(file, now.AddDays(-8));
			File.SetLastWriteTimeUtc(file, now.AddDays(-1).DateTime);

			// Act
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — deleted based on metadata age, not filesystem age.
			Assert.False(File.Exists(file));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> falls back to
	/// <c>FileInfo.LastWriteTimeUtc</c> when the shuttle file is corrupt (not valid SQLite) and the
	/// <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata cannot be read.
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenShuttleFileCorrupt_FallsBackToFileLastWriteTime()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("cleanup-test");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			DateTime now = harness.TimeProvider.GetUtcNow().DateTime;

			// Create a corrupt shuttle file (not valid SQLite) with a filesystem timestamp
			// that is beyond the retention period.
			string corruptFile = Path.Combine(tempDir.Path, "corrupt.shuttle.sqlite");
			await File.WriteAllTextAsync(corruptFile, "not-a-sqlite-database");
			File.SetLastWriteTimeUtc(corruptFile, now.AddDays(-8));

			// Act — metadata read fails, falls back to filesystem timestamp, deletes the file.
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — deleted based on filesystem fallback.
			Assert.False(File.Exists(corruptFile));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> correctly handles an empty backup
	/// directory where no shuttle files exist (exercises the <c>deletedCount == 0</c> path without
	/// logging a deletion summary).
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenDirectoryExistsButEmpty_DoesNotThrow()
	{
		// Arrange — empty directory (no shuttle files at all)
		using var tempDir = new TemporaryFolder("cleanup-test");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			// Act — no files to delete
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — no exception, directory still exists and is empty.
			Assert.True(Directory.Exists(tempDir.Path));
			Assert.Empty(Directory.GetFiles(tempDir.Path));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> preserves a file whose
	/// <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata is <b>exactly</b> at the retention boundary
	/// (age equals <c>BackupRetentionDays</c>). The comparison uses strict less-than (<c>createdUtc &lt; cutoffDate</c>),
	/// so a file at the boundary is preserved.
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenFileIsExactlyAtRetentionBoundary_PreservesFile()
	{
		// Arrange — file created exactly 7 days ago with 7-day retention.
		using var tempDir = new TemporaryFolder("cleanup-boundary");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			DateTimeOffset now = harness.TimeProvider.GetUtcNow();

			string boundaryFile = Path.Combine(tempDir.Path, "boundary.shuttle.sqlite");
			await TestHarness.CreateMinimalShuttleFileAsync(boundaryFile, now.AddDays(-7));

			// Act
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — exactly at boundary → preserved (strict less-than comparison).
			Assert.True(File.Exists(boundaryFile));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when a corrupt shuttle file (unreadable metadata) has a filesystem
	/// <c>LastWriteTimeUtc</c> <b>within</b> the retention period, the fallback preserves the file.
	/// This is the keep-direction counterpart of
	/// <see cref="CleanupOldBackupsAsync_WhenShuttleFileCorrupt_FallsBackToFileLastWriteTime"/> which tests
	/// the delete-direction.
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenCorruptFileWithinRetention_PreservesFile()
	{
		// Arrange — corrupt shuttle file with a recent filesystem timestamp.
		using var tempDir = new TemporaryFolder("cleanup-corrupt-recent");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			DateTime now = harness.TimeProvider.GetUtcNow().DateTime;

			string corruptFile = Path.Combine(tempDir.Path, "corrupt-recent.shuttle.sqlite");
			await File.WriteAllTextAsync(corruptFile, "not-a-sqlite-database");
			File.SetLastWriteTimeUtc(corruptFile, now.AddDays(-3));

			// Act — metadata read fails, falls back to filesystem timestamp (3 days old < 7 days retention).
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — preserved based on filesystem fallback.
			Assert.True(File.Exists(corruptFile));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> falls back to
	/// <c>FileInfo.LastWriteTimeUtc</c> when the shuttle file is a valid SQLite database but its
	/// <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata entry has been removed. Unlike the
	/// <see cref="CleanupOldBackupsAsync_WhenShuttleFileCorrupt_FallsBackToFileLastWriteTime"/> test
	/// (which uses a completely corrupt file), this test uses a structurally valid shuttle file,
	/// exercising the <c>GetCreatedUtcAsync() returns null</c> path rather than the
	/// <c>InitializeAsync() throws</c> path.
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenShuttleHasNoCreatedUtcMetadata_FallsBackToFileLastWriteTime()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("cleanup-no-created");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = tempDir.Path;
		});
		try
		{
			DateTimeOffset now = harness.TimeProvider.GetUtcNow();

			// Create a valid shuttle file with CreatedUtc metadata, then strip the CreatedUtc entry.
			string file = Path.Combine(tempDir.Path, "no-created.shuttle.sqlite");
			await TestHarness.CreateMinimalShuttleFileAsync(file, now.AddDays(-3));

			// Remove the CreatedUtc metadata row via raw SQLite.
			// Pooling must be disabled so the file handle is released immediately — otherwise the
			// pooled connection holds the lock and CleanupOldBackupsAsync cannot delete the file.
			await using (var conn = new SqliteConnection($"Data Source={file};Pooling=False"))
			{
				await conn.OpenAsync();
				await using SqliteCommand cmd = conn.CreateCommand();
				cmd.CommandText = """DELETE FROM "__Shuttle_BackupInfo" WHERE "key" = 'CreatedUtc'""";
				await cmd.ExecuteNonQueryAsync();
			}

			// Set the filesystem timestamp to beyond the retention period.
			File.SetLastWriteTimeUtc(file, now.AddDays(-8).DateTime);

			// Act — metadata read returns null for CreatedUtc, falls back to filesystem time (8 days old > 7 days).
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — deleted based on filesystem fallback.
			Assert.False(File.Exists(file));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> swallows generic (non-OCE)
	/// exceptions from its outer try/catch without propagating them — ensuring that backup cleanup issues
	/// never block application startup. Uses <see cref="ExecutionStageMonitor"/> to inject an
	/// <see cref="IOException"/> at the <c>CleanupOldBackups.BeforeScan</c> stage (before the directory
	/// scan), which is caught by the method-level catch block rather than the per-file catch block.
	/// </summary>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenScanThrowsGenericException_DoesNotThrow()
	{
		// Arrange
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = Path.GetTempPath();
		});
		try
		{
			// Inject an IOException at BeforeScan — this fires after the cancellation check but
			// before the directory scan, hitting the outer catch (Exception ex) block.
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt("CleanupOldBackups.BeforeScan", new IOException("Simulated scan failure"));

			// Act — should not throw; the outer catch swallows the exception.
			await harness.Sut.CleanupOldBackupsAsync(harness.Options, CancellationToken.None);

			// Assert — the exception was swallowed and the initialization status is unchanged.
			// StartAsync() was never called, so the status must still be NotStarted with no
			// failure information — the swallowed IOException must not leak into the status.
			AssertNotStartedStatus(harness.Status);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> propagates
	/// <see cref="OperationCanceledException"/> instead of swallowing it in the outer catch block.
	/// </summary>
	/// <remarks>
	/// Uses <see cref="ExecutionStageMonitor"/> to deterministically cancel the token at the
	/// <c>CleanupOldBackups.BeforeScan</c> stage.
	/// </remarks>
	[Fact]
	public async Task CleanupOldBackupsAsync_WhenOperationCancelled_PropagatesException()
	{
		// Arrange
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = Path.GetTempPath();
		});
		try
		{
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.CancelAt("CleanupOldBackups.BeforeScan", out CancellationToken ct);

			// Act + Assert — OperationCanceledException is not swallowed.
			var ex = await Assert.ThrowsAsync<OperationCanceledException>(() =>
				         harness.Sut.CleanupOldBackupsAsync(harness.Options, ct));
			Assert.Equal(ct, ex.CancellationToken);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion
}
