// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Core.IO;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Unit tests for <see cref="SqliteShuttleReader"/> covering construction, initialization, lifecycle,
/// table reading, metadata access, migration history, integrity validation, and timestamp parsing.
/// </summary>
/// <remarks>
///     <para>
///     All tests use file-based SQLite databases in temporary directories. Valid shuttle files are
///     created via <see cref="SqliteShuttleWriter"/> as test infrastructure; invalid files (missing
///     markers, wrong format version) are created directly with raw <c>SqliteConnection</c> commands.
///     </para>
///     <list type="bullet">
///         <item><c>SqliteShuttleReaderTests.Construction.cs</c> — Constructor parameter validation</item>
///         <item><c>SqliteShuttleReaderTests.InitializeAsync.cs</c> — Initialization and marker validation</item>
///         <item><c>SqliteShuttleReaderTests.ValidateIntegrityAsync.cs</c> — PRAGMA integrity_check</item>
///         <item><c>SqliteShuttleReaderTests.GetTableNamesAsync.cs</c> — Table name filtering</item>
///         <item><c>SqliteShuttleReaderTests.ReadTableAsync.cs</c> — Schema and row reading</item>
///         <item><c>SqliteShuttleReaderTests.GetMigrationHistoryAsync.cs</c> — Migration history</item>
///         <item><c>SqliteShuttleReaderTests.GetMetadataAsync.cs</c> — Metadata access</item>
///         <item><c>SqliteShuttleReaderTests.GetCreatedUtcAsync.cs</c> — Timestamp parsing</item>
///         <item><c>SqliteShuttleReaderTests.Helpers.cs</c> — Shared test infrastructure</item>
///     </list>
/// </remarks>
[Trait("Category", "DataPort")]
public sealed partial class SqliteShuttleReaderTests
{
	#region DisposeAsync()

	/// <summary>
	/// Verifies that disposing the reader before initialization completes without throwing.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_WhenNotInitialized_CompletesSuccessfully()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			var sut = new SqliteShuttleReader(filePath, NullLogger.Instance);

			// Act — should not throw
			await sut.DisposeAsync();
		}
	}

	#endregion
}
