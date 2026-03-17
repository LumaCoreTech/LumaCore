// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleReaderTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.InitializeAsync"/> succeeds when the shuttle file
	/// is valid and contains the expected completion markers.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenShuttleFileIsValid_Succeeds()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			var sut = new SqliteShuttleReader(filePath, NullLogger.Instance);
			try
			{
				// Arrange — verify precondition
				Assert.False(sut.IsInitialized);

				// Act
				await sut.InitializeAsync();

				// Assert
				Assert.True(sut.IsInitialized);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.InitializeAsync"/> throws
	/// <see cref="InvalidOperationException"/> when called twice on the same instance.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenCalledTwice_ThrowsInvalidOperationException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			var sut = new SqliteShuttleReader(filePath, NullLogger.Instance);
			try
			{
				await sut.InitializeAsync();

				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAsync());
				Assert.Equal("Reader has already been initialized.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.InitializeAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the reader has been disposed.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			var sut = new SqliteShuttleReader(filePath, NullLogger.Instance);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.InitializeAsync());
			Assert.Contains(nameof(SqliteShuttleReader), ex.ObjectName);
		}
	}

	/// <summary>
	/// Provides test data for
	/// <see cref="InitializeAsync_WhenShuttleFileIsInvalid_ThrowsInvalidOperationException"/>.
	/// Each row pairs a human-readable scenario name (shown by the test runner) with the setup delegate
	/// that produces the corresponding invalid shuttle file.
	/// </summary>
	public static TheoryData<string, Func<string, Task>> InvalidShuttleFileData => new()
	{
		// No BackupInfo table — validation fails immediately
		{ "no backup info table", CreateShuttleFileWithNoBackupInfoTableAsync },

		// BackupInfo exists but the export-status marker row is missing
		{ "missing status marker", CreateShuttleFileWithMissingStatusMarkerAsync },

		// BackupInfo exists with status but the format version is unsupported
		{ "wrong format version", CreateShuttleFileWithWrongFormatVersionAsync }
	};

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.InitializeAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the shuttle file fails structural validation.
	/// </summary>
	/// <param name="scenario">A human-readable label for the test runner output.</param>
	/// <param name="createInvalidFile">
	/// Setup delegate that creates the invalid shuttle file at the given path.
	/// </param>
	[Theory]
	[MemberData(nameof(InvalidShuttleFileData))]
	public async Task InitializeAsync_WhenShuttleFileIsInvalid_ThrowsInvalidOperationException(
		string             scenario,
		Func<string, Task> createInvalidFile)
	{
		_ = scenario; // Used by the test runner for display purposes only.

		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await createInvalidFile(filePath);

			var sut = new SqliteShuttleReader(filePath, NullLogger.Instance);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAsync());
				Assert.StartsWith("Shuttle file validation failed.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}
}
