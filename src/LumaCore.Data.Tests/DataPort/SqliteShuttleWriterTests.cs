// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Unit tests for <see cref="SqliteShuttleWriter"/> covering construction, initialization, lifecycle,
/// table writing, metadata, migration history, and finalization.
/// </summary>
/// <remarks>
///     <para>
///     All tests use file-based SQLite databases in temporary directories. Written data is verified
///     using raw <c>SqliteConnection</c>/<c>SqliteCommand</c> calls — the companion
///     <see cref="SqliteShuttleReader"/> is intentionally not used to avoid cross-class coupling.
///     </para>
///     <list type="bullet">
///         <item><c>SqliteShuttleWriterTests.cs</c> — Lifecycle guards (this file)</item>
///         <item><c>SqliteShuttleWriterTests.Construction.cs</c> — Constructor parameter validation</item>
///         <item><c>SqliteShuttleWriterTests.FinalizeAsync.cs</c> — Finalization and completion markers</item>
///         <item><c>SqliteShuttleWriterTests.Helpers.cs</c> — Shared test infrastructure</item>
///         <item><c>SqliteShuttleWriterTests.WriteMetadataAsync.cs</c> — Metadata writing and reserved key guard</item>
///         <item><c>SqliteShuttleWriterTests.WriteMigrationHistoryAsync.cs</c> — Migration history writing</item>
///         <item><c>SqliteShuttleWriterTests.WriteTableAsync.cs</c> — Table creation and row insertion</item>
///     </list>
/// </remarks>
[Trait("Category", "DataPort")]
public sealed partial class SqliteShuttleWriterTests
{
	#region InitializeAsync()

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.InitializeAsync"/> succeeds and enables subsequent
	/// write operations.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenCalled_Succeeds()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
			try
			{
				Assert.False(sut.IsInitialized);

				// Act
				await sut.InitializeAsync();

				// Assert — writer is initialized and file was created
				Assert.True(sut.IsInitialized);
				Assert.True(File.Exists(filePath));
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.InitializeAsync"/> throws
	/// <see cref="InvalidOperationException"/> when called twice.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenCalledTwice_ThrowsInvalidOperationException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
			try
			{
				await sut.InitializeAsync();

				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAsync());
				Assert.Equal("Writer has already been initialized", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.InitializeAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the writer has been disposed.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.InitializeAsync());
			Assert.Equal(typeof(SqliteShuttleWriter).FullName!, ex.ObjectName);
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.InitializeAsync"/> throws
	/// <see cref="InvalidOperationException"/> when called after <see cref="SqliteShuttleWriter.FinalizeAsync"/>
	/// has already been called.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenAlreadyFinalized_ThrowsInvalidOperationException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
			try
			{
				await sut.InitializeAsync();
				await sut.FinalizeAsync();

				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAsync());
				Assert.Equal(
					"Cannot re-initialize a writer that has already been finalized. " +
					"Create a new instance for each export.",
					ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	#endregion

	#region DisposeAsync()

	/// <summary>
	/// Verifies that disposing the writer before initialization completes without throwing.
	/// This covers the <c>connection == null</c> branch in <see cref="SqliteShuttleWriter.DisposeAsync"/>.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_WhenNotInitialized_CompletesSuccessfully()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);

			// Act — should not throw
			await sut.DisposeAsync();

			// Assert — writer was never initialized
			Assert.False(sut.IsInitialized);
		}
	}

	#endregion
}
