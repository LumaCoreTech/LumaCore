// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class ResourceServiceTests
{
	#region GetDownloadInfoAsync

	/// <summary>
	/// Verifies that <see cref="ResourceService.GetDownloadInfoAsync"/> returns the joined storage
	/// metadata for an existing reference.
	/// </summary>
	[Fact]
	public async Task GetDownloadInfoAsync_WhenReferenceExists_ReturnsMetadata()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("download-me");
		ResourceUploadResult uploaded = await sut.UploadAsync(
			                                content,
			                                ResourceOwnerKind.User,
			                                ownerId: new ResourceOwnerId(5),
			                                contentType: "application/octet-stream",
			                                createdByParticipantId: null,
			                                utcNow: DateTime.UtcNow,
			                                originalFileName: "x.bin");

		// Act
		ResourceDownloadInfo? info = await sut.GetDownloadInfoAsync(uploaded.ReferencePublicId);

		// Assert
		Assert.NotNull(info);
		Assert.Equal("application/octet-stream", info.ContentType);
		Assert.Equal("x.bin", info.OriginalFileName);
		Assert.Equal(11, info.SizeBytes);
		Assert.Equal(store.SavedPaths.Single(), info.StoragePath);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService.GetDownloadInfoAsync"/> returns <see langword="null"/>
	/// when no reference matches the supplied public identifier.
	/// </summary>
	[Fact]
	public async Task GetDownloadInfoAsync_WhenReferenceMissing_ReturnsNull()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);

		// Act
		ResourceDownloadInfo? info = await sut.GetDownloadInfoAsync(Guid.NewGuid());

		// Assert
		Assert.Null(info);
	}

	#endregion

	#region DeleteReferencesByOwnerAsync

	/// <summary>
	/// Verifies that <see cref="ResourceService.DeleteReferencesByOwnerAsync"/> removes all references
	/// for the specified owner and returns the deleted row count.
	/// </summary>
	[Fact]
	public async Task DeleteReferencesByOwnerAsync_WhenReferencesExist_DeletesAllAndReturnsCount()
	{
		// Arrange: create two references for owner 11 and one for owner 22.
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = DateTime.UtcNow;
		using (MemoryStream s1 = MakeStream("a"))
		{
			await sut.UploadAsync(
				s1,
				ResourceOwnerKind.User,
				new ResourceOwnerId(11),
				"text/plain",
				null,
				utcNow);
		}
		using (MemoryStream s2 = MakeStream("b"))
		{
			await sut.UploadAsync(
				s2,
				ResourceOwnerKind.User,
				new ResourceOwnerId(11),
				"text/plain",
				null,
				utcNow);
		}
		using (MemoryStream s3 = MakeStream("c"))
		{
			await sut.UploadAsync(
				s3,
				ResourceOwnerKind.User,
				new ResourceOwnerId(22),
				"text/plain",
				null,
				utcNow);
		}

		// Act
		int deleted = await sut.DeleteReferencesByOwnerAsync(
			              ResourceOwnerKind.User,
			              ownerId: new ResourceOwnerId(11));

		// Assert
		Assert.Equal(2, deleted);

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(
			0,
			await verify.ResourceReferences.AsNoTracking()
				.CountAsync(rr => rr.OwnerId == new ResourceOwnerId(11)));
		Assert.Equal(
			1,
			await verify.ResourceReferences.AsNoTracking()
				.CountAsync(rr => rr.OwnerId == new ResourceOwnerId(22)));
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService.DeleteReferencesByOwnerAsync"/> returns zero when no
	/// references match the supplied owner.
	/// </summary>
	[Fact]
	public async Task DeleteReferencesByOwnerAsync_WhenNoReferences_ReturnsZero()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);

		// Act
		int deleted = await sut.DeleteReferencesByOwnerAsync(
			              ResourceOwnerKind.User,
			              ownerId: new ResourceOwnerId(999));

		// Assert
		Assert.Equal(0, deleted);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService.DeleteReferencesByOwnerAsync"/> detaches stale
	/// <see cref="ResourceReferenceEntity"/> tracker entries for the targeted owner before running
	/// <c>ExecuteDeleteAsync</c>, so a subsequent upload for the same owner can insert a fresh
	/// reference without an Identity-Map collision.
	/// </summary>
	/// <remarks>
	/// The production comment documents the exact bug pattern this test reproduces:
	/// <c>ExecuteDeleteAsync</c> bypasses the <see cref="DbContext.ChangeTracker"/>, so if the owner's
	/// original reference was tracked (e.g. via an <c>Include(...)</c> read earlier in the same scope)
	/// and the underlying store later reuses the freed primary-key value (SQLite ROWID, sequence
	/// recycling), a follow-up <c>Add</c> would collide with the now-stale tracked instance. Without
	/// the detach step, the second upload throws
	/// <see cref="InvalidOperationException"/>. The sequence is therefore:
	/// <list type="number">
	///     <item>
	///         <description>Upload A → reference tracked via a subsequent read.</description>
	///     </item>
	///     <item>
	///         <description>DeleteReferencesByOwnerAsync(A) → row gone from DB, entry must be detached.</description>
	///     </item>
	///     <item>
	///         <description>Upload B for the same owner → must succeed, even if SQLite reuses the freed rowid.</description>
	///     </item>
	/// </list>
	/// </remarks>
	[Fact]
	public async Task DeleteReferencesByOwnerAsync_WhenEntryIsTracked_DetachesStaleEntryAndAllowsReinsert()
	{
		// Arrange — upload A and then read it back via the same DbContext so the reference is tracked.
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var ownerId = new ResourceOwnerId(77);

		using (MemoryStream firstContent = MakeStream("pre-delete"))
		{
			await sut.UploadAsync(
				firstContent,
				ResourceOwnerKind.User,
				ownerId,
				contentType: "text/plain",
				createdByParticipantId: null,
				utcNow);
		}

		// Touch the reference through the fixture's DbContext so the ChangeTracker holds it. This
		// mirrors realistic scope usage (e.g. a service that reads a list of references before
		// deleting them) — exactly the situation the production detach step guards against.
		ResourceReferenceEntity tracked = await mFixture.DbContext.ResourceReferences
			                                  .FirstAsync(rr =>
				                                  rr.OwnerKind == ResourceOwnerKind.User &&
				                                  rr.OwnerId == ownerId);
		Assert.Equal(
			EntityState.Unchanged,
			mFixture.DbContext.Entry(tracked).State); // sanity: the read really did track the entity

		// Act — delete via the SUT, then immediately upload a fresh reference for the same owner in
		// the same scope. Without the detach step this second upload would throw because the stale
		// tracker entry and the new Add would fight over the same (reused) primary key.
		int deleted = await sut.DeleteReferencesByOwnerAsync(ResourceOwnerKind.User, ownerId);

		// Post-delete invariant — the detach step has run; the previously tracked reference must no
		// longer appear in the ChangeTracker. Asserting this explicitly (in addition to the
		// re-upload further below) pins the contract even if a future refactor in the underlying
		// store changes primary-key reuse behaviour and masks the symptom.
		Assert.Equal(EntityState.Detached, mFixture.DbContext.Entry(tracked).State);
		Assert.DoesNotContain(
			mFixture.DbContext.ChangeTracker.Entries<ResourceReferenceEntity>(),
			e => e.Entity.OwnerKind == ResourceOwnerKind.User && e.Entity.OwnerId == ownerId);

		using MemoryStream secondContent = MakeStream("post-delete");
		ResourceUploadResult second = await sut.UploadAsync(
			                              secondContent,
			                              ResourceOwnerKind.User,
			                              ownerId,
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow);

		// Assert — the delete reported 1 row, the re-upload succeeded, and the DB now holds exactly
		// one reference for the owner: the fresh one. Checking count + PublicId together rules out
		// both "old reference survived" and "new reference was silently merged with the stale one".
		Assert.Equal(1, deleted);
		Assert.NotEqual(Guid.Empty, second.ReferencePublicId);

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceReferenceEntity only = await verify.ResourceReferences
			                               .AsNoTracking()
			                               .SingleAsync(rr =>
				                               rr.OwnerKind == ResourceOwnerKind.User && rr.OwnerId == ownerId);
		Assert.Equal(second.ReferencePublicId, only.PublicId);
	}

	#endregion

	#region PreferCompiledHotPathQueries

	/// <summary>
	/// Verifies that <see cref="ResourceService.GetDownloadInfoAsync"/> returns the same metadata when
	/// the pre-compiled hot-path query path is enabled, exercising the
	/// <see cref="LumaCore.Data.Queries.ResourceQueries.GetDownloadInfoByPublicId"/> branch.
	/// </summary>
	[Fact]
	public async Task GetDownloadInfoAsync_WhenPreferCompiledHotPathQueriesEnabled_ReturnsMetadata()
	{
		// Arrange — upload via the dynamic-LINQ SUT, then read via a SUT with compiled queries enabled
		// so we exercise both branches against the same database state.
		var store = new FakeResourceStore();
		ResourceService writer = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("compiled-download");
		ResourceUploadResult uploaded = await writer.UploadAsync(
			                                content,
			                                ResourceOwnerKind.User,
			                                ownerId: new ResourceOwnerId(7),
			                                contentType: "text/plain",
			                                createdByParticipantId: null,
			                                utcNow: DateTime.UtcNow,
			                                originalFileName: "c.txt");

		ResourceService reader = CreateSut(mFixture, store, preferCompiledHotPathQueries: true);

		// Act
		ResourceDownloadInfo? info = await reader.GetDownloadInfoAsync(uploaded.ReferencePublicId);

		// Assert
		Assert.NotNull(info);
		Assert.Equal("text/plain", info.ContentType);
		Assert.Equal("c.txt", info.OriginalFileName);
		Assert.Equal(17, info.SizeBytes);
		Assert.Equal(store.SavedPaths.Single(), info.StoragePath);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService.GetDownloadInfoAsync"/> returns <see langword="null"/>
	/// for an unknown reference when the pre-compiled hot-path query path is enabled.
	/// </summary>
	[Fact]
	public async Task GetDownloadInfoAsync_WhenPreferCompiledHotPathQueriesEnabledAndMissing_ReturnsNull()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store, preferCompiledHotPathQueries: true);

		// Act
		ResourceDownloadInfo? info = await sut.GetDownloadInfoAsync(Guid.NewGuid());

		// Assert
		Assert.Null(info);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService.UploadAsync"/> deduplicates against an existing Active
	/// resource when the pre-compiled hot-path query path is enabled, exercising both
	/// <see cref="LumaCore.Data.Queries.ResourceQueries.GetActiveByContentHash"/> and
	/// <see cref="LumaCore.Data.Queries.ResourceQueries.GetDeletionStateById"/>.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenPreferCompiledHotPathQueriesEnabled_DeduplicatesAgainstExisting()
	{
		// Arrange — first upload via dynamic-LINQ SUT to establish the row, then upload the same content
		// via the compiled-query SUT so the dedup lookup runs through ResourceQueries.
		var store = new FakeResourceStore();
		ResourceService writer = CreateSut(mFixture, store);
		DateTime utcNow = DateTime.UtcNow;

		using (MemoryStream first = MakeStream("dedup-payload"))
		{
			await writer.UploadAsync(
				first,
				ResourceOwnerKind.User,
				new ResourceOwnerId(1),
				"text/plain",
				null,
				utcNow);
		}

		ResourceService dedupSut = CreateSut(mFixture, store, preferCompiledHotPathQueries: true);

		// Act
		using MemoryStream second = MakeStream("dedup-payload");
		ResourceUploadResult result = await dedupSut.UploadAsync(
			                              second,
			                              ResourceOwnerKind.User,
			                              new ResourceOwnerId(2),
			                              "text/plain",
			                              null,
			                              utcNow);

		// Assert — second upload deduplicated; only the first call wrote a file.
		Assert.True(result.WasDeduplicated);
		Assert.Single(store.SavedPaths);
	}

	#endregion
}
