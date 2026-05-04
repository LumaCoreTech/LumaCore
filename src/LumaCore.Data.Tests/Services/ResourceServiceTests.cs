// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Data.Tests.Services;

// Resource lifecycle orchestration.
//
// These tests cover ResourceService against a real (SQLite in-memory) DbContext and a fake
// IResourceStore that records file operations in memory. The story is layered:
//
//   1. UploadAsync happy path: writes file, inserts row, attaches reference.
//   2. UploadAsync dedup hit: same hash → reuses existing row, no new file written.
//   3. UploadAsync dedup race (the audit-fix path): MARK promotes the dedup target between
//      our SELECT and our reference INSERT — service must detach the orphan reference and
//      fall through to a fresh upload.
//   4. UploadAsync non-DbUpdate cleanup: when SaveChanges throws something *other* than a
//      DbUpdateException after the file landed, the service must roll back the
//      transaction/savepoint, unregister the compensation, delete the orphan file inline, and
//      rethrow the original exception unchanged — both for own-transaction and ambient paths.
//   5. UploadAsync ambient transaction: when the caller wraps the upload in a compensating
//      transaction, file cleanup must fire on outer rollback.
//   6. GetDownloadInfoAsync: round-trips reference → info; null when missing.
//   7. DeleteReferencesByOwnerAsync: ExecuteDelete returns row count.
//
// Helpers (CreateSut, MakeStream) live in ResourceServiceTests.Helpers.cs; the in-memory
// FakeResourceStore lives in ResourceServiceTests.TestModels.cs.

/// <summary>
/// Tests for <see cref="ResourceService"/>: orchestrates <see cref="LumaCoreDbContext"/> and
/// <see cref="IResourceStore"/> for resource upload (with content-hash deduplication and
/// MARK-race revalidation), download, and pre-CASCADE reference deletion. Each <c>partial</c>
/// file in this class targets one operation; see the file-level narrative comment for the
/// reading order.
/// </summary>
[Trait("Category", "Resources")]
public sealed partial class ResourceServiceTests : IAsyncLifetime
{
	private readonly DbFixture mFixture = DbFixture.CreateSqliteInMemory();

	/// <summary>
	/// Initializes the database schema for the test instance.
	/// </summary>
	/// <returns>A task that represents the asynchronous initialization operation.</returns>
	public ValueTask InitializeAsync() => mFixture.InitializeAsync();

	/// <summary>
	/// Disposes the underlying database resources.
	/// </summary>
	/// <returns>A task that represents the asynchronous dispose operation.</returns>
	public ValueTask DisposeAsync() => mFixture.DisposeAsync();

	#region Constructor

	/// <summary>
	/// Verifies that <see cref="ResourceService"/> rejects a <see langword="null"/> <see cref="LumaCoreDbContext"/>
	/// with an <see cref="ArgumentNullException"/> identifying the offending parameter.
	/// </summary>
	[Fact]
	public void Constructor_WhenDbContextIsNull_ThrowsArgumentNullException()
	{
		// Arrange + Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceService(
			dbContext: null!,
			store: new FakeResourceStore(),
			streamBufferPool: new StreamBufferPool(new StreamBufferPoolOptions()),
			databaseOptions: Options.Create(new DatabaseOptions()),
			timeProvider: TimeProvider.System,
			logger: NullLogger<ResourceService>.Instance));
		Assert.Equal("dbContext", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService"/> rejects a <see langword="null"/> <see cref="IResourceStore"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenStoreIsNull_ThrowsArgumentNullException()
	{
		// Arrange — class fixture is initialized by xUnit via IAsyncLifetime before this test runs.
		// The constructor under test does not touch the context before the null check, so the
		// already-prepared mFixture.DbContext is sufficient.

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceService(
			dbContext: mFixture.DbContext,
			store: null!,
			streamBufferPool: new StreamBufferPool(new StreamBufferPoolOptions()),
			databaseOptions: Options.Create(new DatabaseOptions()),
			timeProvider: TimeProvider.System,
			logger: NullLogger<ResourceService>.Instance));
		Assert.Equal("store", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService"/> rejects a <see langword="null"/>
	/// <see cref="IOptions{TOptions}"/> wrapper.
	/// </summary>
	[Fact]
	public void Constructor_WhenDatabaseOptionsIsNull_ThrowsArgumentNullException()
	{
		// Arrange — class fixture initialized by xUnit (see Constructor_WhenStoreIsNull_... for rationale).

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceService(
			dbContext: mFixture.DbContext,
			store: new FakeResourceStore(),
			streamBufferPool: new StreamBufferPool(new StreamBufferPoolOptions()),
			databaseOptions: null!,
			timeProvider: TimeProvider.System,
			logger: NullLogger<ResourceService>.Instance));
		Assert.Equal("databaseOptions", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService"/> rejects a <see langword="null"/>
	/// <see cref="TimeProvider"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
	{
		// Arrange — class fixture initialized by xUnit (see Constructor_WhenStoreIsNull_... for rationale).

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceService(
			dbContext: mFixture.DbContext,
			store: new FakeResourceStore(),
			streamBufferPool: new StreamBufferPool(new StreamBufferPoolOptions()),
			databaseOptions: Options.Create(new DatabaseOptions()),
			timeProvider: null!,
			logger: NullLogger<ResourceService>.Instance));
		Assert.Equal("timeProvider", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService"/> rejects a <see langword="null"/>
	/// <see cref="ILogger"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
	{
		// Arrange — class fixture initialized by xUnit (see Constructor_WhenStoreIsNull_... for rationale).

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceService(
			dbContext: mFixture.DbContext,
			store: new FakeResourceStore(),
			streamBufferPool: new StreamBufferPool(new StreamBufferPoolOptions()),
			databaseOptions: Options.Create(new DatabaseOptions()),
			timeProvider: TimeProvider.System,
			logger: null!));
		Assert.Equal("logger", ex.ParamName);
	}

	#endregion
}
