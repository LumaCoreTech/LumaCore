// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Cryptography;
using System.Text;

using LumaCore.Core;
using LumaCore.Data.Entities;
using LumaCore.Data.Services;
using LumaCore.Definitions;
using LumaCore.TestUtilities.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class ResourceServiceTests
{
	#region Argument validation

	/// <summary>
	/// Verifies that <see cref="ResourceService.UploadAsync"/> rejects a <see langword="null"/> content stream.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenContentIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UploadAsync(
			         content: null!,
			         ResourceOwnerKind.User,
			         ownerId: new ResourceOwnerId(1),
			         contentType: "image/png",
			         createdByParticipantId: null,
			         utcNow: DateTime.UtcNow));
		Assert.Equal("content", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService.UploadAsync"/> rejects a <see langword="null"/> content type.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenContentTypeIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("payload");

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UploadAsync(
			         content,
			         ResourceOwnerKind.User,
			         ownerId: new ResourceOwnerId(1),
			         contentType: null!,
			         createdByParticipantId: null,
			         utcNow: DateTime.UtcNow));
		Assert.Equal("contentType", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceService.UploadAsync"/> rejects a whitespace-only content type.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenContentTypeIsWhitespace_ThrowsArgumentException()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("payload");

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.UploadAsync(
			         content,
			         ResourceOwnerKind.User,
			         ownerId: new ResourceOwnerId(1),
			         contentType: "   ",
			         createdByParticipantId: null,
			         utcNow: DateTime.UtcNow));
		Assert.Equal("contentType", ex.ParamName);
	}

	#endregion

	#region Happy path

	/// <summary>
	/// Verifies that uploading new content writes the file to the store, inserts a
	/// <see cref="ResourceEntity"/> row, attaches a <see cref="ResourceReferenceEntity"/> and
	/// returns a <see cref="ResourceUploadResult"/> with <c>WasDeduplicated = false</c>.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenContentIsNew_PersistsFileAndCreatesResourceAndReference()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("hello-resource");
		DateTime utcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

		// Act
		ResourceUploadResult result = await sut.UploadAsync(
			                              content,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(42),
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow,
			                              originalFileName: "hello.txt");

		// Assert
		Assert.False(result.WasDeduplicated);
		Assert.Equal(14, result.SizeBytes);
		Assert.Equal(64, result.ContentHash.Length);
		Assert.NotEqual(Guid.Empty, result.ReferencePublicId);

		// File written exactly once.
		Assert.Equal(1, store.SaveCount);
		Assert.Single(store.Files);

		// Verify DB state via a fresh context to bypass change tracking.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceEntity resource = await verify.Resources.AsNoTracking().SingleAsync();
		Assert.Equal(result.ContentHash, resource.ContentHash);
		Assert.Equal(ResourceDeletionState.Active, resource.DeletionState);
		Assert.Equal(14, resource.SizeBytes);
		Assert.Single(store.SavedPaths, resource.StoragePath);

		ResourceReferenceEntity reference = await verify.ResourceReferences.AsNoTracking().SingleAsync();
		Assert.Equal(result.ReferencePublicId, reference.PublicId);
		Assert.Equal(ResourceOwnerKind.User, reference.OwnerKind);
		Assert.Equal(new ResourceOwnerId(42), reference.OwnerId);
		Assert.Equal("text/plain", reference.ContentType);
		Assert.Equal("hello.txt", reference.OriginalFileName);
	}

	/// <summary>
	/// Verifies that uploading content whose hash already matches an <see cref="ResourceDeletionState.Active"/>
	/// resource <em>reuses</em> that resource: no new file is written, no new resource row is created,
	/// and only a fresh <see cref="ResourceReferenceEntity"/> is attached.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenSameHashExists_DeduplicatesWithoutWritingNewFile()
	{
		// Arrange: first upload primes the deduplication table.
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		using (MemoryStream first = MakeStream("dup-payload"))
		{
			await sut.UploadAsync(
				first,
				ResourceOwnerKind.User,
				ownerId: new ResourceOwnerId(1),
				contentType: "text/plain",
				createdByParticipantId: null,
				utcNow);
		}
		int firstSaveCount = store.SaveCount;

		// Act: second upload with the SAME bytes but a different owner.
		using MemoryStream second = MakeStream("dup-payload");
		ResourceUploadResult result = await sut.UploadAsync(
			                              second,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(2),
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow);

		// Assert
		Assert.True(result.WasDeduplicated);
		Assert.Equal(firstSaveCount, store.SaveCount); // No additional file written.

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(1, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(2, await verify.ResourceReferences.AsNoTracking().CountAsync());
	}

	/// <summary>
	/// Verifies that a non-<see langword="null"/> <c>createdByParticipantId</c> is mapped onto
	/// <see cref="ResourceEntity.CreatedByParticipantId"/> when the upload persists a fresh resource.
	/// All other happy-path tests pass <see langword="null"/>; this test pins the positive case so a
	/// future refactor cannot silently drop the audit field on inserted rows.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenCreatedByParticipantIdProvided_PersistsItOnNewResourceRow()
	{
		// Arrange — seed a real ParticipantEntity so the test also passes against providers that
		// enforce the FK (Postgres, SQL Server). Field values are minimal: the upload service only
		// reads the id; the rest is just enough to satisfy NOT NULL columns.
		DateTime utcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var participant = new ParticipantEntity
		{
			PublicId = Guid.NewGuid(),
			CreatedAtUtc = utcNow.AddMinutes(-1),
			DisplayName = "uploader"
		};
		mFixture.DbContext.Participants.Add(participant);
		await mFixture.DbContext.SaveChangesAsync();
		ParticipantId expectedParticipantId = participant.Id;

		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("audited-payload");

		// Act
		ResourceUploadResult result = await sut.UploadAsync(
			                              content,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(participant.Id.Value),
			                              contentType: "text/plain",
			                              createdByParticipantId: expectedParticipantId,
			                              utcNow);

		// Assert: load the inserted Resource via a fresh context to bypass change tracking and
		// confirm the value was actually written to the row (not just held in memory).
		Assert.False(result.WasDeduplicated);
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceEntity persisted = await verify.Resources.AsNoTracking().SingleAsync();
		Assert.Equal(expectedParticipantId, persisted.CreatedByParticipantId);
	}

	#endregion

	#region Dedup race (audit-fix regression)

	/// <summary>
	/// Verifies the MARK-race protection added in the audit fix: when the dedup target is promoted
	/// from <see cref="ResourceDeletionState.Active"/> to <see cref="ResourceDeletionState.PendingDeletion"/>
	/// between the dedup SELECT and the reference INSERT becoming visible, the service detaches the
	/// orphan reference and falls through to a fresh upload instead of returning a doomed PublicId.
	/// </summary>
	/// <remarks>
	/// Race simulation: a <see cref="DbContext.SavingChanges"/> hook on the SUT context flips the
	/// dedup target's <see cref="ResourceEntity.DeletionState"/> to
	/// <see cref="ResourceDeletionState.PendingDeletion"/> via a side context at the exact moment EF
	/// is about to INSERT the dedup reference. This closes the window between the dedup SELECT (which
	/// observed Active) and the AsNoTracking() revalidation that runs after the INSERT commits — so
	/// the SUT must observe the flipped state, detach its just-attached orphan reference, and proceed
	/// to a fresh upload (attempt 2).
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenDedupTargetPromotedMidUpload_DetachesAndUploadsFresh()
	{
		// Arrange — pre-insert an Active row that the upload's dedup SELECT will match. The
		// SavingChanges hook below races us by promoting the row to PendingDeletion just before
		// the orphan reference is inserted, so the AsNoTracking() revalidation observes the flip.
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		const string payload = "race-payload";

		// Compute the same hash the service will compute, so we can pre-insert a matching row.
		byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
		string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

		var existing = new ResourceEntity
		{
			ContentHash = hash,
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = payloadBytes.Length,
			CreatedAtUtc = utcNow.AddMinutes(-5),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.Active
		};
		mFixture.DbContext.Resources.Add(existing);
		await mFixture.DbContext.SaveChangesAsync();

		// Hook fires once, when EF is about to INSERT the orphan ResourceReference on the SUT's
		// context. A side context flips the resource row to PendingDeletion — invisible to the
		// SUT's change tracker but observable by the post-INSERT AsNoTracking() revalidation.
		bool racePerformed = false;
		mFixture.DbContext.SavingChanges += (_, _) =>
		{
			if (racePerformed)
				return;

			bool insertingReference = mFixture.DbContext.ChangeTracker
				.Entries<ResourceReferenceEntity>()
				.Any(e => e.State == EntityState.Added);
			if (!insertingReference)
				return;

			racePerformed = true;
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			int pendingState = (int)ResourceDeletionState.PendingDeletion;
			long existingId = existing.Id.Value;
			side.Database.ExecuteSql(
				$"UPDATE \"Resources\" SET \"DeletionState\" = {pendingState} WHERE \"Id\" = {existingId}");
		};

		// Act
		using MemoryStream content = MakeStream(payload);
		ResourceUploadResult result = await sut.UploadAsync(
			                              content,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(99),
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow);

		// Assert: race was triggered, then a fresh upload succeeded → WasDeduplicated must be false
		// and a brand-new file must have been written to the store.
		Assert.True(racePerformed);
		Assert.False(result.WasDeduplicated);
		Assert.Equal(1, store.SaveCount);

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		// Two resource rows now exist: the original (PendingDeletion) and the freshly uploaded (Active).
		Assert.Equal(2, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(
			1,
			await verify.Resources.AsNoTracking()
				.CountAsync(r => r.DeletionState == ResourceDeletionState.Active));
		// The orphan reference attached during the racy attempt must have been removed; the only
		// surviving reference is the one created by the successful fresh upload.
		Assert.Equal(1, await verify.ResourceReferences.AsNoTracking().CountAsync());
	}

	/// <summary>
	/// Verifies the audit-fix follow-up: when the dedup target is <em>hard-deleted</em> (not just
	/// promoted) between the dedup SELECT and the post-attach revalidation — the realistic outcome
	/// when MARK and SWEEP run in the same GC cycle and the cascade takes the just-attached
	/// reference with it — the service must treat the missing row identically to the
	/// <see cref="ResourceDeletionState.PendingDeletion"/> case (compiled-query parity) and fall
	/// through to a fresh upload, instead of surfacing
	/// <see cref="InvalidOperationException"/> from <c>FirstAsync</c>.
	/// </summary>
	/// <remarks>
	/// Race simulation: a <see cref="DbContext.SavingChanges"/> hook flags the pending reference
	/// INSERT, and a paired <see cref="DbContext.SavedChanges"/> hook then deletes the resource
	/// row via a side context — i.e., strictly <em>after</em> the INSERT committed, so the FK is
	/// satisfied at INSERT time but the subsequent <c>AsNoTracking()</c> revalidation observes
	/// the missing row. (Doing the DELETE in <c>SavingChanges</c> would remove the FK target
	/// before the INSERT and surface a foreign-key violation instead of the race we want to
	/// reproduce — the SQLite in-memory fixture shares a single connection, so the side
	/// context's DELETE is immediately visible to the SUT.)
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenDedupTargetHardDeletedMidUpload_DetachesAndUploadsFresh()
	{
		// Arrange — pre-insert an Active row that the upload's dedup SELECT will match.
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		const string payload = "hard-delete-race-payload";

		byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
		string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

		var existing = new ResourceEntity
		{
			ContentHash = hash,
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = payloadBytes.Length,
			CreatedAtUtc = utcNow.AddMinutes(-5),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.Active
		};
		mFixture.DbContext.Resources.Add(existing);
		await mFixture.DbContext.SaveChangesAsync();

		// SavingChanges flags the pending reference INSERT; SavedChanges then performs the
		// hard-delete via a side context — strictly *after* the INSERT committed. Doing the DELETE
		// in SavingChanges would remove the FK target before the INSERT (the SQLite in-memory
		// fixture shares a single connection, so the side-context DELETE is immediately visible)
		// and surface a FK violation instead of the race we want to reproduce.
		bool insertPending = false;
		bool racePerformed = false;
		mFixture.DbContext.SavingChanges += (_, _) =>
		{
			if (racePerformed)
				return;

			insertPending = mFixture.DbContext.ChangeTracker
				.Entries<ResourceReferenceEntity>()
				.Any(e => e.State == EntityState.Added);
		};
		mFixture.DbContext.SavedChanges += (_, _) =>
		{
			if (racePerformed || !insertPending)
				return;

			racePerformed = true;
			insertPending = false;
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			long existingId = existing.Id.Value;
			side.Database.ExecuteSql($"DELETE FROM \"Resources\" WHERE \"Id\" = {existingId}");
		};

		// Act
		using MemoryStream content = MakeStream(payload);
		ResourceUploadResult result = await sut.UploadAsync(
			                              content,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(99),
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow);

		// Assert: race fired and the fresh-upload fallback ran. Without the FirstOrDefaultAsync
		// fix, FirstAsync would have surfaced an InvalidOperationException to the caller here.
		Assert.True(racePerformed);
		Assert.False(result.WasDeduplicated);
		Assert.Equal(1, store.SaveCount);

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		// Original row gone, single fresh Active row remains.
		Assert.Equal(1, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(
			1,
			await verify.Resources.AsNoTracking()
				.CountAsync(r => r.DeletionState == ResourceDeletionState.Active));
		// Exactly one reference — created by the successful fresh upload (the orphan reference
		// from the racy attempt was cascade-deleted with the original row).
		Assert.Equal(1, await verify.ResourceReferences.AsNoTracking().CountAsync());
	}

	/// <summary>
	/// Same hard-delete race scenario as
	/// <see cref="UploadAsync_WhenDedupTargetHardDeletedMidUpload_DetachesAndUploadsFresh"/>, but
	/// exercising the compiled-query path (<see cref="DatabaseOptions.PreferCompiledHotPathQueries"/>
	/// = <see langword="true"/>). Both branches must agree on treating a missing row as
	/// <see cref="ResourceDeletionState.PendingDeletion"/>; this test prevents future divergence.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenDedupTargetHardDeletedMidUpload_CompiledPath_DetachesAndUploadsFresh()
	{
		// Arrange — same shape as the non-compiled twin above, but with the hot-path opt-in flipped on.
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store, preferCompiledHotPathQueries: true);
		DateTime utcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		const string payload = "hard-delete-race-payload-compiled";

		byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
		string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

		var existing = new ResourceEntity
		{
			ContentHash = hash,
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = payloadBytes.Length,
			CreatedAtUtc = utcNow.AddMinutes(-5),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.Active
		};
		mFixture.DbContext.Resources.Add(existing);
		await mFixture.DbContext.SaveChangesAsync();

		// Same SavingChanges/SavedChanges split as the non-compiled twin — see that test for the
		// detailed rationale (must DELETE *after* the reference INSERT to avoid an FK violation).
		bool insertPending = false;
		bool racePerformed = false;
		mFixture.DbContext.SavingChanges += (_, _) =>
		{
			if (racePerformed)
				return;

			insertPending = mFixture.DbContext.ChangeTracker
				.Entries<ResourceReferenceEntity>()
				.Any(e => e.State == EntityState.Added);
		};
		mFixture.DbContext.SavedChanges += (_, _) =>
		{
			if (racePerformed || !insertPending)
				return;

			racePerformed = true;
			insertPending = false;
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			long existingId = existing.Id.Value;
			side.Database.ExecuteSql($"DELETE FROM \"Resources\" WHERE \"Id\" = {existingId}");
		};

		// Act
		using MemoryStream content = MakeStream(payload);
		ResourceUploadResult result = await sut.UploadAsync(
			                              content,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(99),
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow);

		// Assert: same outcome as the non-compiled path — fresh upload, no surfaced exception.
		Assert.True(racePerformed);
		Assert.False(result.WasDeduplicated);
		Assert.Equal(1, store.SaveCount);

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(1, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(1, await verify.ResourceReferences.AsNoTracking().CountAsync());
	}

	#endregion

	#region Orphan-file cleanup symmetry (audit-fix regression)

	/// <summary>
	/// Verifies that a failure of <see cref="IResourceStore.DeleteAsync"/> during the
	/// <see cref="DbUpdateException"/> cleanup branch does <em>not</em> mask the original conflict.
	/// The service must still classify the conflict (here: a concurrent Active row with the same
	/// hash) and complete via the dedup-race-winner branch — a leaked orphan file is recoverable
	/// by the next SWEEP cycle, but a swallowed conflict would leave the caller without a result.
	/// </summary>
	/// <remarks>
	/// Race simulation: <see cref="FakeResourceStore.OnSave"/> fires while the SUT is writing the
	/// file to the store \u2014 i.e., <em>before</em> the upload's database transaction opens \u2014 and
	/// pre-inserts a competing Active row with the same content hash via a side context. (Doing
	/// this from <c>SavingChanges</c> would be wrong: the SQLite in-memory fixture shares a
	/// single connection, so the side-context's INSERT would join the SUT's open transaction and
	/// vanish on rollback, leaving the post-conflict re-query without a winner.) The composite
	/// unique index <c>(ContentHash, DeletionState)</c> then turns the SUT's INSERT into a
	/// <see cref="DbUpdateException"/>, driving the conflict-cleanup branch in <c>UploadAsync</c>.
	/// <see cref="FakeResourceStore.OnDelete"/> throws on the cleanup call to exercise the
	/// audit-fix <c>try/catch</c> around <see cref="IResourceStore.DeleteAsync"/>.
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenOrphanCleanupFailsDuringConflict_StillCompletesViaHashRaceWinner()
	{
		// Arrange \u2014 no pre-existing row, so the dedup SELECT returns null and the SUT proceeds
		// down the fresh-upload path.
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		const string payload = "orphan-cleanup-failure-payload";

		byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
		string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

		// OnSave hook fires once during the file write, BEFORE the upload's DB transaction opens.
		// A side-context INSERT of an Active row with the same hash then violates the composite
		// (ContentHash, DeletionState) unique index when the SUT later inserts its own row \u2014 the
		// entry point for the conflict-cleanup branch we want to exercise. Because the side
		// context's SaveChanges runs while the connection is still transaction-free, its row
		// survives the SUT's later rollback and is visible to the post-conflict re-query.
		bool racePerformed = false;
		long raceWinnerId = 0;
		store.OnSave = _ =>
		{
			if (racePerformed)
				return Task.CompletedTask;

			racePerformed = true;
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			var winner = new ResourceEntity
			{
				ContentHash = hash,
				StoragePath = Guid.NewGuid().ToString(),
				SizeBytes = payloadBytes.Length,
				CreatedAtUtc = utcNow.AddSeconds(-1),
				CreatedByParticipantId = null,
				DeletionState = ResourceDeletionState.Active
			};
			side.Resources.Add(winner);
			side.SaveChanges();
			raceWinnerId = winner.Id.Value;
			return Task.CompletedTask;
		};

		// Inject a store-side failure on the orphan-file cleanup that runs inside the DbUpdateException
		// catch. Without the audit fix this exception would propagate and mask the conflict, leaving
		// the caller with a confusing IOException instead of the dedup result.
		int deleteFailureCount = 0;
		store.OnDelete = _ =>
		{
			deleteFailureCount++;
			throw new IOException("simulated orphan-file cleanup failure");
		};

		// Act
		using MemoryStream content = MakeStream(payload);
		ResourceUploadResult result = await sut.UploadAsync(
			                              content,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(99),
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow);

		// Assert: race fired, cleanup failure was swallowed, hash-race-winner branch produced a
		// dedup result against the side-context's row.
		Assert.True(racePerformed);
		Assert.Equal(1, deleteFailureCount);
		Assert.True(result.WasDeduplicated);
		Assert.Equal(hash, result.ContentHash);

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		// Exactly one Active resource (the race winner); the SUT's INSERT was rolled back.
		ResourceEntity surviving = await verify.Resources.AsNoTracking().SingleAsync();
		Assert.Equal(raceWinnerId, surviving.Id.Value);
		Assert.Equal(ResourceDeletionState.Active, surviving.DeletionState);

		// Exactly one reference, attached to the race winner.
		ResourceReferenceEntity reference = await verify.ResourceReferences.AsNoTracking().SingleAsync();
		Assert.Equal(raceWinnerId, reference.ResourceId.Value);
		Assert.Equal(result.ReferencePublicId, reference.PublicId);
	}

	/// <summary>
	/// Verifies the <see cref="DbUpdateException"/>-cleanup branch under an ambient
	/// <see cref="ICompensatingTransaction"/>: when a hash-race winner is inserted via a side context
	/// while the SUT is uploading inside the caller's transaction, the service must roll back the
	/// per-upload savepoint (not the outer transaction), classify the conflict, attach a dedup reference
	/// to the winner, and complete successfully — leaving the outer transaction free to commit.
	/// </summary>
	/// <remarks>
	/// Counterpart to <see cref="UploadAsync_WhenOrphanCleanupFailsDuringConflict_StillCompletesViaHashRaceWinner"/>,
	/// which exercises the same conflict but on the standalone (own-transaction) path. This test covers
	/// the previously uncovered <c>RollbackToSavepointAsync</c> +
	/// <c>ReleaseSavepointAsync</c> pair in the ambient branch of the
	/// <c>catch (DbUpdateException)</c> handler.
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenDbUpdateConflictUnderAmbientTransaction_RollsBackToSavepointAndDeduplicates()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 17, 10, 0, 0, DateTimeKind.Utc);
		const string payload = "ambient-dbupdate-conflict-payload";

		byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
		string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

		// OnSave fires while the SUT is writing the file, AFTER the ambient transaction is already open
		// but BEFORE the per-upload savepoint is created. The side-context INSERT joins the shared
		// SQLite connection / ambient transaction and is visible to the SUT's INSERT — but it lives
		// OUTSIDE the savepoint, so the SUT's savepoint rollback does NOT undo it. This is the same
		// trick used by the standalone-path counterpart test, just under an ambient txn.
		bool racePerformed = false;
		long raceWinnerId = 0;
		store.OnSave = _ =>
		{
			if (racePerformed)
				return Task.CompletedTask;

			racePerformed = true;
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			var winner = new ResourceEntity
			{
				ContentHash = hash,
				StoragePath = Guid.NewGuid().ToString(),
				SizeBytes = payloadBytes.Length,
				CreatedAtUtc = utcNow.AddSeconds(-1),
				CreatedByParticipantId = null,
				DeletionState = ResourceDeletionState.Active
			};
			side.Resources.Add(winner);
			side.SaveChanges();
			raceWinnerId = winner.Id.Value;
			return Task.CompletedTask;
		};

		// Act: open an ambient compensating transaction so the SUT takes the savepoint branch.
		await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();

		using MemoryStream content = MakeStream(payload);
		ResourceUploadResult result = await sut.UploadAsync(
			                              content,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(13),
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow);

		// Commit the outer transaction so the dedup reference becomes visible to the verification
		// context. If RollbackToSavepoint/ReleaseSavepoint had broken the outer transaction, this
		// commit would throw.
		await tx.CommitAsync();

		// Assert: race fired, dedup result returned, savepoint cleanup did not break the outer txn.
		Assert.True(racePerformed);
		Assert.True(result.WasDeduplicated);
		Assert.Equal(hash, result.ContentHash);

		// Assert: orphan file was inline-deleted by the conflict-cleanup branch (path was ours, the
		// foreign row uses a different StoragePath GUID).
		Assert.Equal(1, store.SaveCount);
		Assert.Equal(1, store.DeleteCount);
		Assert.Equal(store.SavedPaths.Single(), store.DeletedPaths.Single());

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		// Exactly one Active resource (the race winner); the SUT's INSERT was rolled back via savepoint.
		ResourceEntity surviving = await verify.Resources.AsNoTracking().SingleAsync();
		Assert.Equal(raceWinnerId, surviving.Id.Value);

		ResourceReferenceEntity reference = await verify.ResourceReferences.AsNoTracking().SingleAsync();
		Assert.Equal(raceWinnerId, reference.ResourceId.Value);
		Assert.Equal(result.ReferencePublicId, reference.PublicId);
	}

	/// <summary>
	/// Verifies that when the <see cref="DbUpdateException"/> recovery branch runs but the hash-race
	/// candidate has already been hard-deleted (not just promoted) between the SUT's initial
	/// <c>SaveChangesAsync</c> failure and the recovery <c>FirstOrDefaultAsync</c>, the service
	/// surfaces the original <see cref="DbUpdateException"/> instead of quietly swallowing it. This
	/// pins the final <c>throw</c> at the bottom of the <c>catch (DbUpdateException)</c> block, which
	/// is the only remaining safety net when neither the storage-path-collision branch nor the
	/// hash-race-winner branch applies.
	/// </summary>
	/// <remarks>
	/// The scenario is reconstructed with two cooperating race hooks:
	/// <list type="number">
	///     <item>
	///         <description>
	///         <see cref="FakeResourceStore.OnSave"/> fires during the SUT's file write (so AFTER the
	///         SUT's dedup read returned no match) and inserts a colliding Active row via a side
	///         context. This forces the unique <c>(ContentHash, DeletionState)</c> violation when the
	///         SUT's own INSERT runs next.
	///         </description>
	///     </item>
	///     <item>
	///         <description>
	///         <see cref="FakeResourceStore.OnDelete"/> fires during the orphan-file cleanup inside the
	///         <c>catch (DbUpdateException)</c> branch — that is AFTER the SaveChanges threw but BEFORE
	///         the recovery <c>FirstOrDefaultAsync</c> — and deletes the just-inserted colliding row.
	///         The recovery read therefore sees no hash-race winner, and the service must fall through
	///         to the final <c>throw</c>.
	///         </description>
	///     </item>
	/// </list>
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenDbUpdateConflictButNoHashRaceWinner_RethrowsOriginalDbUpdateException()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 19, 10, 0, 0, DateTimeKind.Utc);
		const string payload = "no-winner-payload";

		byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
		string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

		long collidingRowId = 0;
		bool saveHookFired = false;
		bool deleteHookFired = false;

		// OnSave: fires during the SUT's IResourceStore.SaveAsync — the SUT's dedup SELECT has
		// already run (returned empty) and the INSERT has NOT yet run. Race-insert the colliding
		// row from a side context so the SUT's INSERT will hit the unique index.
		store.OnSave = _ =>
		{
			if (saveHookFired)
				return Task.CompletedTask;

			saveHookFired = true;
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			var colliding = new ResourceEntity
			{
				ContentHash = hash,
				StoragePath = Guid.NewGuid().ToString(),
				SizeBytes = payloadBytes.Length,
				CreatedAtUtc = utcNow.AddSeconds(-1),
				CreatedByParticipantId = null,
				DeletionState = ResourceDeletionState.Active
			};
			side.Resources.Add(colliding);
			side.SaveChanges();
			collidingRowId = colliding.Id.Value;
			return Task.CompletedTask;
		};

		// OnDelete: fires during the orphan-file cleanup in the catch-DbUpdateException branch —
		// AFTER the SaveChanges threw, BEFORE the recovery FirstOrDefaultAsync. Race-remove the
		// colliding row so the recovery read finds no hash-race winner and the service falls
		// through to the final rethrow.
		store.OnDelete = _ =>
		{
			if (deleteHookFired)
				return Task.CompletedTask;

			deleteHookFired = true;
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			int removed = side.Resources
				.Where(r => r.Id == new ResourceId(collidingRowId))
				.ExecuteDelete();
			Assert.Equal(1, removed); // sanity — the OnSave hook really inserted it
			return Task.CompletedTask;
		};

		// Act + Assert — the service must surface the original DbUpdateException.
		using MemoryStream content = MakeStream(payload);
		var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => sut.UploadAsync(
			         content,
			         ResourceOwnerKind.User,
			         ownerId: new ResourceOwnerId(21),
			         contentType: "text/plain",
			         createdByParticipantId: null,
			         utcNow));

		Assert.NotNull(ex);
		Assert.True(saveHookFired);
		Assert.True(deleteHookFired);

		// Sanity: no surviving rows (the colliding row was race-removed, the SUT's INSERT was
		// rolled back) and no references attached.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(0, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(0, await verify.ResourceReferences.AsNoTracking().CountAsync());

		// The SUT's own storage file was deleted by the orphan-file cleanup before the rethrow.
		Assert.Equal(1, store.SaveCount);
		Assert.Equal(1, store.DeleteCount);
		Assert.Equal(store.SavedPaths.Single(), store.DeletedPaths.Single());
	}

	/// <summary>
	/// Verifies that the standalone-path hash-race recovery detects and recovers from a MARK-promotion
	/// that lands between the <c>hashRaceWinner</c> SELECT and the <c>AttachReferenceAsync</c> commit:
	/// the service must observe the post-commit <see cref="ResourceDeletionState.PendingDeletion"/>
	/// state, remove the orphaned reference it just created, and surface the original
	/// <see cref="DbUpdateException"/>. Without this hardening the caller would receive a
	/// <see cref="ResourceUploadResult.ReferencePublicId"/> pointing at a resource that the next SWEEP
	/// cycle will cascade-delete — a silent "upload worked, file vanished later" failure that is
	/// extremely hard to diagnose in production.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Race choreography — three cooperating hooks:
	///     </para>
	///     <list type="number">
	///         <item>
	///             <description>
	///             <see cref="FakeResourceStore.OnSave"/> fires during the SUT's file write: a
	///             side-context inserts the hash-race winner row (Active). This triggers the unique
	///             <c>(ContentHash, DeletionState)</c> violation on the SUT's own INSERT.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             The SUT's own-transaction SaveChanges throws, the catch-branch runs orphan-file
	///             cleanup, and the recovery FirstOrDefaultAsync reads the winner as Active.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             A <c>SavingChanges</c> hook on the SUT's own DbContext fires
	///             <b>
	///             on the recovery
	///             AttachReferenceAsync SaveChanges
	///             </b>
	///             (gated to skip the first, failing SaveChanges)
	///             and promotes the winner to PendingDeletion via a side context BEFORE the recovery
	///             SaveChanges commits. By the time the post-attach AsNoTracking re-read runs, the
	///             winner is PendingDeletion — the exact race the new revalidation must catch.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     Counterpart to the ambient-path test
	///     <see cref="UploadAsync_WhenDbUpdateConflictUnderAmbientTransaction_RollsBackToSavepointAndDeduplicates"/>,
	///     which exercises the pessimistic lock for the same race shape under a compensating
	///     transaction.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenStandaloneRecoveryWinnerPromotedBeforeCommit_RemovesReferenceAndRethrows()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 20, 10, 0, 0, DateTimeKind.Utc);
		const string payload = "standalone-recovery-race-payload";

		byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
		string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

		long winnerId = 0;
		bool saveHookFired = false;
		bool promotionTriggered = false;
		int saveChangesInvocations = 0;

		// Hook 1 — OnSave: race-insert the winner during the SUT's file write. This fires BEFORE
		// the SUT's own-transaction SaveChanges, so the SUT's INSERT hits the unique index.
		store.OnSave = _ =>
		{
			if (saveHookFired)
				return Task.CompletedTask;

			saveHookFired = true;
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			var winner = new ResourceEntity
			{
				ContentHash = hash,
				StoragePath = Guid.NewGuid().ToString(),
				SizeBytes = payloadBytes.Length,
				CreatedAtUtc = utcNow.AddSeconds(-1),
				CreatedByParticipantId = null,
				DeletionState = ResourceDeletionState.Active
			};
			side.Resources.Add(winner);
			side.SaveChanges();
			winnerId = winner.Id.Value;
			return Task.CompletedTask;
		};

		// Hook 2 — SavingChanges on the SUT's own DbContext: the first invocation is the failing
		// own-tx SaveChanges (we let it pass through untouched); the second invocation is the
		// recovery AttachReferenceAsync SaveChanges, and THAT is where we inject the promotion.
		// The hook must promote BEFORE the SaveChanges commits, so the post-attach AsNoTracking
		// re-read will observe PendingDeletion.
		EventHandler<SavingChangesEventArgs> savingHook = (_, _) =>
		{
			saveChangesInvocations++;

			// Skip the first SaveChanges (the own-transaction one that throws).
			if (saveChangesInvocations < 2)
				return;

			if (promotionTriggered)
				return;

			promotionTriggered = true;

			// Promote the winner to PendingDeletion on a side context. This commits immediately;
			// the SUT's outer SaveChanges is still in flight but under an implicit auto-commit
			// (standalone path), so READ COMMITTED makes the promotion visible to the post-attach
			// re-read that follows.
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			int promoted = side.Resources
				.Where(r => r.Id == new ResourceId(winnerId))
				.ExecuteUpdate(s => s.SetProperty(
					r => r.DeletionState,
					ResourceDeletionState.PendingDeletion));
			Assert.Equal(1, promoted); // sanity
		};
		mFixture.DbContext.SavingChanges += savingHook;

		try
		{
			// Act + Assert — the service must surface the original DbUpdateException.
			using MemoryStream content = MakeStream(payload);
			var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => sut.UploadAsync(
				         content,
				         ResourceOwnerKind.User,
				         ownerId: new ResourceOwnerId(31),
				         contentType: "text/plain",
				         createdByParticipantId: null,
				         utcNow));

			Assert.NotNull(ex);
			Assert.True(saveHookFired);
			Assert.True(promotionTriggered);
			// One own-tx SaveChanges (failed), one recovery-attach SaveChanges (succeeded, then
			// rolled back by our post-attach revalidation).
			Assert.Equal(2, saveChangesInvocations);
		}
		finally
		{
			mFixture.DbContext.SavingChanges -= savingHook;
		}

		// Assert — the orphaned reference must be gone from the DB.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(0, await verify.ResourceReferences.AsNoTracking().CountAsync());

		// The winner row itself survives (in PendingDeletion state) — it is a separate entity's
		// row and SWEEP will reclaim it later. The SUT's recovery did NOT touch it.
		ResourceEntity surviving = await verify.Resources.AsNoTracking().SingleAsync();
		Assert.Equal(winnerId, surviving.Id.Value);
		Assert.Equal(ResourceDeletionState.PendingDeletion, surviving.DeletionState);

		// The SUT's own storage file was deleted by the orphan-file cleanup before the rethrow.
		Assert.Equal(1, store.SaveCount);
		Assert.Equal(1, store.DeleteCount);
		Assert.Equal(store.SavedPaths.Single(), store.DeletedPaths.Single());
	}

	/// <summary>
	/// Verifies that a <see cref="ResourceEntity.StoragePath"/> GUID collision (statistically impossible
	/// without a broken RNG) does <em>not</em> delete the foreign file on disk. This is the critical
	/// data-loss guard: the colliding path belongs to the foreign row, so blindly removing it during
	/// orphan-file cleanup would silently destroy another resource's payload.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Race simulation: <see cref="FakeResourceStore.OnSave"/> fires during the SUT's file write and
	///     pre-inserts a side-context row that already owns the storage path the SUT just generated. The
	///     SUT's subsequent INSERT then violates the unique <see cref="ResourceEntity.StoragePath"/>
	///     index, driving the <see cref="DbUpdateException"/> branch.
	///     </para>
	///     <para>
	///     The test asserts three guarantees:
	///     </para>
	///     <list type="number">
	///         <item>
	///             <description><see cref="IResourceStore.DeleteAsync"/> is <b>never</b> called.</description>
	///         </item>
	///         <item>
	///             <description>The SUT throws <see cref="InvalidOperationException"/> with a clear RNG-compromised hint.</description>
	///         </item>
	///         <item>
	///             <description>The foreign row stays intact in the database.</description>
	///         </item>
	///     </list>
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenStoragePathGuidCollidesWithForeignRow_PreservesForeignFileAndThrows()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 18, 10, 0, 0, DateTimeKind.Utc);
		const string payload = "storage-path-collision-payload";

		// Capture the SUT's just-generated storage path during the file write so the OnSave hook can
		// pre-insert a foreign row that owns it. Doing the side-insert from OnSave (which fires before
		// the SUT's DB transaction opens) ensures the foreign row survives the SUT's later rollback.
		string? collidingPath = null;
		long foreignRowId = 0;
		store.OnSave = path =>
		{
			collidingPath = path;
			using LumaCoreDbContext side = mFixture.CreateDbContext();
			var foreign = new ResourceEntity
			{
				// Different content hash, otherwise the conflict would route through the dedup-race
				// branch instead of the StoragePath-collision branch we want to exercise.
				ContentHash = new string('a', 64),
				StoragePath = path,
				SizeBytes = 1,
				CreatedAtUtc = utcNow.AddSeconds(-1),
				CreatedByParticipantId = null,
				DeletionState = ResourceDeletionState.Active
			};
			side.Resources.Add(foreign);
			side.SaveChanges();
			foreignRowId = foreign.Id.Value;
			return Task.CompletedTask;
		};

		using MemoryStream content = MakeStream(payload);

		// Act + Assert: SUT must surface the RNG-compromised hint without deleting the foreign file.
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UploadAsync(
			         content,
			         ResourceOwnerKind.User,
			         ownerId: new ResourceOwnerId(17),
			         contentType: "text/plain",
			         createdByParticipantId: null,
			         utcNow));

		Assert.NotNull(collidingPath);
		Assert.Equal(
			$"GUID collision on Resource.StoragePath '{collidingPath}'. The random number " +
			"generator may be compromised. The file on disk was preserved because it belongs " +
			"to a foreign row. Investigate the host's entropy source.",
			ex.Message);
		Assert.IsType<DbUpdateException>(ex.InnerException);

		// THE critical assertion: DeleteAsync must NEVER have been called — the path belongs to the
		// foreign row, deleting it would destroy that resource's data on disk.
		Assert.Equal(0, store.DeleteCount);
		Assert.Empty(store.DeletedPaths);

		// Foreign row is intact; the SUT's INSERT was rolled back, so it remains the sole row.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceEntity surviving = await verify.Resources.AsNoTracking().SingleAsync();
		Assert.Equal(foreignRowId, surviving.Id.Value);
		Assert.Equal(collidingPath, surviving.StoragePath);
		Assert.Equal(0, await verify.ResourceReferences.AsNoTracking().CountAsync());
	}

	#endregion

	#region Non-DbUpdate exception cleanup (audit-fix regression)

	/// <summary>
	/// Verifies the standalone (own-transaction) branch of the
	/// <c>catch (Exception ex) when (ex is not DbUpdateException)</c> handler in
	/// <see cref="ResourceService.UploadAsync"/>: when an arbitrary non-<see cref="DbUpdateException"/>
	/// failure surfaces from <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> after the
	/// file has been written, the service must roll back the own transaction, detach the failed
	/// <see cref="ResourceEntity"/>, unregister the orphan-file compensation, delete the orphan
	/// file inline, and rethrow the original exception unchanged.
	/// </summary>
	/// <remarks>
	/// Fault injection: a <see cref="DbContext.SavingChanges"/> handler throws
	/// <see cref="InvalidOperationException"/> the first time a <see cref="ResourceEntity"/> is in
	/// the <see cref="EntityState.Added"/> state. EF propagates handler exceptions directly (no
	/// <see cref="DbUpdateException"/> wrapping), reaching the non-DbUpdate catch. The follow-up
	/// <see cref="LumaCoreDbContext.BeginCompensatingTransactionAsync"/> + <c>RollbackAsync</c>
	/// pair at the end is the strongest available proof that the compensation handle was actually
	/// removed: a stale handle would fire on the rollback and bump <c>DeleteCount</c> to 2.
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenNonDbUpdateExceptionUnderOwnTransaction_RollsBackAndDeletesOrphanFileAndRethrows()
	{
		// Arrange — no ambient transaction, so the SUT takes the own-transaction branch.
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("non-dbupdate-own-tx-payload");
		DateTime utcNow = new(2026, 1, 19, 10, 0, 0, DateTimeKind.Utc);

		// Hook fires when the ResourceEntity Add is about to be flushed (after the file has been
		// written and the own transaction is open). Throwing a plain InvalidOperationException
		// from SavingChanges propagates without DbUpdateException wrapping — exactly the path the
		// non-DbUpdate catch handler is designed for.
		bool injected = false;
		mFixture.DbContext.SavingChanges += (_, _) =>
		{
			if (injected)
				return;

			bool insertingResource = mFixture.DbContext.ChangeTracker
				.Entries<ResourceEntity>()
				.Any(e => e.State == EntityState.Added);
			if (!insertingResource)
				return;

			injected = true;
			throw new InvalidOperationException("test injected");
		};

		// Act + Assert: original exception rethrown unchanged (type + exact message).
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UploadAsync(
			         content,
			         ResourceOwnerKind.User,
			         ownerId: new ResourceOwnerId(5),
			         contentType: "text/plain",
			         createdByParticipantId: null,
			         utcNow));
		Assert.Equal("test injected", ex.Message);

		// Assert: file was written (1) and then deleted (1) — same path, store is empty.
		Assert.True(injected);
		Assert.Equal(1, store.SaveCount);
		Assert.Equal(1, store.DeleteCount);
		Assert.Equal(store.SavedPaths.Single(), store.DeletedPaths.Single());
		Assert.Empty(store.Files);

		// Assert: own transaction was rolled back and the resource was detached — no rows persist.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(0, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(0, await verify.ResourceReferences.AsNoTracking().CountAsync());

		// Assert: the orphan-file compensation was unregistered. A stale handle would fire on the
		// rollback below and push DeleteCount to 2 — the strongest check available without
		// reaching into the compensation list internals.
		await using ICompensatingTransaction probe = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		await probe.RollbackAsync();
		Assert.Equal(1, store.DeleteCount);
	}

	/// <summary>
	/// Counterpart to
	/// <see cref="UploadAsync_WhenNonDbUpdateExceptionUnderOwnTransaction_RollsBackAndDeletesOrphanFileAndRethrows"/>:
	/// covers the ambient-transaction branch of the non-<see cref="DbUpdateException"/> catch
	/// handler. The service must roll back to the per-upload savepoint, release it, detach the
	/// failed <see cref="ResourceEntity"/>, unregister the compensation, delete the orphan file
	/// inline, and rethrow — leaving the outer <see cref="ICompensatingTransaction"/> healthy
	/// enough to commit.
	/// </summary>
	/// <remarks>
	/// Same fault-injection technique as the standalone-path twin (see that test for the
	/// rationale). The post-throw <c>CommitAsync</c> on the outer transaction is the load-bearing
	/// assertion: a broken savepoint cleanup would surface here as a commit failure.
	/// </remarks>
	[Fact]
	public async Task
		UploadAsync_WhenNonDbUpdateExceptionUnderAmbientTransaction_RollsBackToSavepointAndDeletesOrphanFile()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("non-dbupdate-ambient-payload");
		DateTime utcNow = new(2026, 1, 19, 11, 0, 0, DateTimeKind.Utc);

		// Same SavingChanges hook as the standalone-path twin — throws InvalidOperationException
		// when the ResourceEntity Add is about to flush, after the savepoint is created.
		bool injected = false;
		mFixture.DbContext.SavingChanges += (_, _) =>
		{
			if (injected)
				return;

			bool insertingResource = mFixture.DbContext.ChangeTracker
				.Entries<ResourceEntity>()
				.Any(e => e.State == EntityState.Added);
			if (!insertingResource)
				return;

			injected = true;
			throw new InvalidOperationException("test injected");
		};

		// Act — open an ambient compensating transaction so the SUT takes the savepoint branch.
		// Scoped via an inner await using block so the wrapper is fully disposed BEFORE the probe
		// transaction below begins; otherwise the post-commit / awaiting-dispose misuse guard in
		// BeginCompensatingTransactionAsync would FailFast and kill the test host.
		InvalidOperationException ex;
		await using (ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync())
		{
			// Act + Assert: original exception rethrown unchanged.
			ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UploadAsync(
				     content,
				     ResourceOwnerKind.User,
				     ownerId: new ResourceOwnerId(11),
				     contentType: "text/plain",
				     createdByParticipantId: null,
				     utcNow));
			Assert.Equal("test injected", ex.Message);

			// Assert: savepoint was rolled back AND released cleanly — committing the outer
			// transaction must succeed. A leaked savepoint or a transaction-state corruption would
			// surface here as an exception.
			await tx.CommitAsync();
		}

		// Assert: file was written and inline-deleted (production keeps both paths symmetric — the
		// ambient branch unregisters and deletes here even though the outer compensation could
		// also do it on rollback).
		Assert.True(injected);
		Assert.Equal(1, store.SaveCount);
		Assert.Equal(1, store.DeleteCount);
		Assert.Equal(store.SavedPaths.Single(), store.DeletedPaths.Single());
		Assert.Empty(store.Files);

		// Assert: nothing committed.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(0, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(0, await verify.ResourceReferences.AsNoTracking().CountAsync());

		// Assert: compensation handle was unregistered — a follow-up rollback must not re-delete.
		await using ICompensatingTransaction probe = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		await probe.RollbackAsync();
		Assert.Equal(1, store.DeleteCount);
	}

	#endregion

	#region Ambient transaction

	/// <summary>
	/// Verifies that when the caller wraps the upload in an <see cref="ICompensatingTransaction"/>
	/// and rolls it back, the orphan-file cleanup compensation registered by the upload fires —
	/// the file written before the savepoint is removed.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenAmbientCompensatingTransactionRollsBack_DeletesUploadedFile()
	{
		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("ambient-rollback");

		// Act
		await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		await sut.UploadAsync(
			content,
			ResourceOwnerKind.User,
			ownerId: new ResourceOwnerId(7),
			contentType: "text/plain",
			createdByParticipantId: null,
			utcNow: DateTime.UtcNow);
		// File was written and visible in the store.
		Assert.Single(store.Files);

		await tx.RollbackAsync();

		// Assert: rollback fired the compensation, which deleted the orphan file.
		Assert.Empty(store.Files);
		Assert.Equal(1, store.DeleteCount);

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(0, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(0, await verify.ResourceReferences.AsNoTracking().CountAsync());
	}

	/// <summary>
	/// Verifies the audit-fix pessimistic write-lock branch: when the upload runs under an ambient
	/// <see cref="ICompensatingTransaction"/> and the dedup SELECT hits an existing
	/// <see cref="ResourceDeletionState.Active"/> row, the service must take a row-level write lock
	/// on the dedup target via a no-op <c>ExecuteUpdate</c> before attaching the reference. The
	/// happy path (no concurrent MARK) must complete cleanly: the lock is acquired, the reference
	/// is attached against the existing row, the outer commit succeeds, no new file is written,
	/// and the verification context observes exactly one Active resource with both references
	/// pointing to it.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This test guards against two regressions in the lock branch:
	///     </para>
	///     <list type="number">
	///         <item>
	///             <description>
	///             The detach-before-<c>ExecuteUpdate</c> dance must not leave the change tracker
	///             in a state that breaks <see cref="ResourceService.UploadAsync"/>'s subsequent
	///             <c>AttachReferenceAsync</c> call (which only needs <c>existing.Id</c>, a value
	///             snapshot taken before detach).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             The <c>SetProperty(r =&gt; r.DeletionState, r =&gt; r.DeletionState)</c> no-op
	///             must execute as a real UPDATE statement (lock acquisition relies on the
	///             provider treating it as a write), but must not actually mutate the row — the
	///             surviving resource must remain
	///             <see cref="ResourceDeletionState.Active"/>.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     A separate test for the <c>locked == 0</c> retry branch is intentionally omitted: the
	///     SQLite in-memory fixture's single-writer model and EF's <c>SavingChanges</c> hooks
	///     cannot intercept the raw <c>ExecuteUpdate</c> SQL emitted by the lock to flip the row
	///     between the dedup SELECT and the lock UPDATE. The retry path is semantically identical
	///     to the post-attach revalidation branch already covered by
	///     <see cref="UploadAsync_WhenDedupTargetPromotedMidUpload_DetachesAndUploadsFresh"/>
	///     (detach the dedup target, fall through the loop, retry or fresh-upload).
	///     </para>
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenDedupHitUnderAmbientTransaction_AcquiresLockAndCommitsReference()
	{
		// Arrange — pre-insert an Active row outside the ambient transaction so the dedup SELECT
		// inside the transaction will match it.
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		DateTime utcNow = new(2026, 1, 18, 10, 0, 0, DateTimeKind.Utc);
		const string payload = "ambient-dedup-lock-payload";

		byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
		string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

		var existing = new ResourceEntity
		{
			ContentHash = hash,
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = payloadBytes.Length,
			CreatedAtUtc = utcNow.AddMinutes(-5),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.Active
		};
		mFixture.DbContext.Resources.Add(existing);
		await mFixture.DbContext.SaveChangesAsync();
		long existingId = existing.Id.Value;

		// Detach so the SUT's dedup SELECT materializes a fresh tracked instance — mirrors the
		// production path where the SUT does not see the caller's pre-existing tracked entities.
		mFixture.DbContext.Entry(existing).State = EntityState.Detached;

		// Act — open an ambient compensating transaction so the SUT takes the pessimistic-lock
		// branch in the dedup loop.
		await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();

		using MemoryStream content = MakeStream(payload);
		ResourceUploadResult result = await sut.UploadAsync(
			                              content,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(42),
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow);

		// Commit so the reference becomes visible to the verification context. If the lock branch
		// had left the change tracker or the transaction in a broken state, this commit would throw.
		await tx.CommitAsync();

		// Assert: dedup result returned, no new file written.
		Assert.True(result.WasDeduplicated);
		Assert.Equal(hash, result.ContentHash);
		Assert.Equal(0, store.SaveCount);
		Assert.Equal(0, store.DeleteCount);

		// Assert: the no-op ExecuteUpdate did not mutate state — the existing row is still Active
		// with the same StoragePath, and exactly one reference now points to it.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceEntity surviving = await verify.Resources.AsNoTracking().SingleAsync();
		Assert.Equal(existingId, surviving.Id.Value);
		Assert.Equal(ResourceDeletionState.Active, surviving.DeletionState);
		Assert.Equal(existing.StoragePath, surviving.StoragePath);

		ResourceReferenceEntity reference = await verify.ResourceReferences.AsNoTracking().SingleAsync();
		Assert.Equal(existingId, reference.ResourceId.Value);
		Assert.Equal(result.ReferencePublicId, reference.PublicId);
	}

	#endregion

	#region Pre-DB cleanup (audit-fix regression)

	/// <summary>
	/// Verifies the SaveAsync-failure cleanup path: when <see cref="IResourceStore.SaveAsync"/> throws,
	/// the orphan-file compensation registered <em>before</em> the save must be unregistered, the
	/// best-effort <see cref="IResourceStore.DeleteAsync"/> call must fire, and the original
	/// exception must propagate unchanged. Together these guarantees ensure no stale compensation
	/// is left attached to the scoped <see cref="LumaCoreDbContext"/> that a later unrelated
	/// <see cref="ICompensatingTransaction"/> rollback could mistakenly invoke against a foreign file.
	/// </summary>
	/// <remarks>
	/// The post-throw <c>BeginCompensatingTransactionAsync</c> + <c>RollbackAsync</c> probe is the
	/// strongest available proof that the compensation was actually unregistered: a stale handle
	/// would fire on the rollback and bump <c>DeleteCount</c> beyond the single best-effort call
	/// from the catch block.
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenSaveAsyncThrows_UnregistersCompensationAndRethrows()
	{
		// Arrange — inject a SaveAsync failure via the FakeResourceStore.OnSave hook.
		var store = new FakeResourceStore
		{
			OnSave = _ => throw new IOException("disk full")
		};
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("save-fails-payload");
		DateTime utcNow = new(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc);

		// Act + Assert — original exception type and message preserved.
		var ex = await Assert.ThrowsAsync<IOException>(() => sut.UploadAsync(
			         content,
			         ResourceOwnerKind.User,
			         ownerId: new ResourceOwnerId(1),
			         contentType: "text/plain",
			         createdByParticipantId: null,
			         utcNow));
		Assert.Equal("disk full", ex.Message);

		// Assert: SaveAsync was attempted once, the catch issued the best-effort DeleteAsync once,
		// and no row was persisted.
		Assert.Equal(1, store.SaveCount);
		Assert.Equal(1, store.DeleteCount);
		Assert.Equal(store.SavedPaths.Single(), store.DeletedPaths.Single());

		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(0, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(0, await verify.ResourceReferences.AsNoTracking().CountAsync());

		// Assert: the orphan-file compensation was unregistered. A stale handle would fire on the
		// probe rollback below and push DeleteCount to 2.
		await using ICompensatingTransaction probe = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		await probe.RollbackAsync();
		Assert.Equal(1, store.DeleteCount);
	}

	/// <summary>
	/// Verifies that when both <see cref="IResourceStore.SaveAsync"/> <em>and</em> the best-effort
	/// <see cref="IResourceStore.DeleteAsync"/> cleanup that follows it throw, the original SaveAsync
	/// exception still propagates unchanged — the cleanup failure is swallowed and surfaced only as
	/// a <see cref="LogLevel.Warning"/> entry carrying the cleanup exception. This locks in the
	/// "exception from a catch block" trap: no exception-replacement, no exception-aggregation.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenSaveAsyncThrowsAndCleanupAlsoThrows_LogsWarningAndRethrowsOriginal()
	{
		// Arrange — inject failures on both SaveAsync and the follow-up best-effort DeleteAsync.
		var saveFailure = new IOException("disk full");
		var cleanupFailure = new IOException("delete also failed");
		var store = new FakeResourceStore
		{
			OnSave = _ => throw saveFailure,
			OnDelete = _ => throw cleanupFailure
		};
		var logger = new ListLogger<ResourceService>();
		ResourceService sut = CreateSut(mFixture, store, logger: logger);
		using MemoryStream content = MakeStream("save-and-cleanup-fail-payload");
		DateTime utcNow = new(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc);

		// Act + Assert — original SaveAsync exception propagates unchanged.
		var ex = await Assert.ThrowsAsync<IOException>(() => sut.UploadAsync(
			         content,
			         ResourceOwnerKind.User,
			         ownerId: new ResourceOwnerId(1),
			         contentType: "text/plain",
			         createdByParticipantId: null,
			         utcNow));
		Assert.Same(saveFailure, ex);

		// Assert: SaveAsync was attempted once; DeleteAsync was attempted once (the best-effort cleanup).
		Assert.Equal(1, store.SaveCount);
		Assert.Equal(1, store.DeleteCount);

		// Assert: the cleanup failure surfaced as a single Warning log entry carrying the cleanup
		// exception (NOT the SaveAsync exception — that one propagates via throw).
		LogEntry warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
		Assert.Same(cleanupFailure, warning.Exception);
		Assert.Equal(
			$"Best-effort cleanup after SaveAsync failure could not delete {store.SavedPaths.Single()}",
			warning.Message);

		// Assert: no DB rows were persisted, and the orphan-file compensation was unregistered
		// (probe rollback must not bump DeleteCount).
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(0, await verify.Resources.AsNoTracking().CountAsync());
		Assert.Equal(0, await verify.ResourceReferences.AsNoTracking().CountAsync());

		await using ICompensatingTransaction probe = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		await probe.RollbackAsync();
		Assert.Equal(1, store.DeleteCount);
	}

	/// <summary>
	/// Verifies that the <see cref="LumaCoreDbContext.RegisterRollbackCompensation"/> misuse guard fires
	/// <em>before</em> any file is written: when the caller has opened a foreign (non-compensating) DB
	/// transaction, the <see cref="FailFast.TerminateApplication(string)"/> call inside the upload
	/// triggers <see cref="FailFastCanceledException"/> from the compensation registration step, and
	/// crucially <see cref="IResourceStore.SaveAsync"/> must <b>not</b> have been invoked. This locks in
	/// the audit-fix order (register before save), which prevents an orphan file from being left on
	/// disk when FailFast actually terminates the process in production.
	/// </summary>
	/// <remarks>
	/// The misuse path was structurally orphan-prone before the audit fix: the file landed on disk
	/// first, then the FailFast triggered, leaving an untrackable file with no DB row for the GC to
	/// reconcile. This regression test asserts <c>SaveCount == 0</c> as the load-bearing invariant.
	/// </remarks>
	[Fact]
	public async Task UploadAsync_WhenForeignTransactionActive_FailsFastBeforeWritingFile()
	{
		// Arrange: subscribe to BeforeTermination so FailFast throws FailFastCanceledException instead of
		// terminating the test host. The handler is removed in a finally to keep the static event
		// state isolated from sibling tests.
		EventHandler<FailFastEventArgs> cancelTermination = (_, args) => args.Cancel = true;
		FailFast.BeforeTermination += cancelTermination;

		try
		{
			var store = new FakeResourceStore();
			ResourceService sut = CreateSut(mFixture, store);
			using MemoryStream content = MakeStream("foreign-tx-payload");
			DateTime utcNow = new(2026, 1, 20, 10, 0, 0, DateTimeKind.Utc);

			// Open a "foreign" transaction the conventional way — exactly the misuse the guard targets.
			await using IDbContextTransaction foreignTx =
				await mFixture.DbContext.Database.BeginTransactionAsync();

			// Act + Assert — FailFast surfaces as FailFastCanceledException because the test host
			// cancels termination above.
			await Assert.ThrowsAsync<FailFastCanceledException>(() => sut.UploadAsync(
				content,
				ResourceOwnerKind.User,
				ownerId: new ResourceOwnerId(1),
				contentType: "text/plain",
				createdByParticipantId: null,
				utcNow));

			// Load-bearing assertion for the audit fix: the FailFast must trigger BEFORE SaveAsync,
			// otherwise a real production termination would leak the file with no DB row.
			Assert.Equal(0, store.SaveCount);
			Assert.Equal(0, store.DeleteCount);
		}
		finally
		{
			FailFast.BeforeTermination -= cancelTermination;
		}
	}

	#endregion

	#region Time provider fallback

	/// <summary>
	/// Verifies that <see cref="ResourceService.UploadAsync"/> falls back to the injected
	/// <see cref="TimeProvider"/> when the caller passes <see langword="null"/> for <c>utcNow</c>: both the
	/// persisted <see cref="ResourceEntity.CreatedAtUtc"/> and the
	/// <see cref="ResourceReferenceEntity.CreatedAtUtc"/> must equal the clock's current UTC time. This is the
	/// per-method binding test for the nullable-<c>utcNow</c> contract — the helper itself is trivial, but each
	/// caller must actually route through it.
	/// </summary>
	[Fact]
	public async Task UploadAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
	{
		// Arrange
		DateTimeOffset fakeNow = new(2026, 6, 1, 12, 30, 45, TimeSpan.Zero);
		var timeProvider = new FakeTimeProvider(fakeNow);
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store, timeProvider: timeProvider);
		using MemoryStream content = MakeStream("clock-fallback");

		// Act
		ResourceUploadResult result = await sut.UploadAsync(
			                              content,
			                              ResourceOwnerKind.User,
			                              ownerId: new ResourceOwnerId(1),
			                              contentType: "text/plain",
			                              createdByParticipantId: null,
			                              utcNow: null);

		// Assert
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceEntity resource = await verify.Resources.AsNoTracking().SingleAsync();
		ResourceReferenceEntity reference = await verify.ResourceReferences.AsNoTracking().SingleAsync();

		Assert.Equal(fakeNow.UtcDateTime, resource.CreatedAtUtc);
		Assert.Equal(fakeNow.UtcDateTime, reference.CreatedAtUtc);
		Assert.Equal(result.ReferencePublicId, reference.PublicId);
	}

	#endregion

	#region Original file name validation

	/// <summary>
	/// Test data for <see cref="UploadAsync_WhenOriginalFileNameInvalid_ThrowsArgumentException"/>. Each row
	/// provides an invalid <c>originalFileName</c> value rejected by the documented contract.
	/// </summary>
	public static TheoryData<string, string> UploadAsync_InvalidOriginalFileName_Data => new()
	{
		// Empty string: caller-side defaults that should have collapsed to null.
		{ "Empty", string.Empty },

		// Whitespace only: stripped UI input that slipped through.
		{ "Whitespace", "   " },

		// Length exceeds the EntityLimits.ResourceOriginalFileNameMaxLength (255) cap.
		{ "Too long", new string('x', EntityLimits.ResourceOriginalFileNameMaxLength + 1) },

		// Forward slash: descriptive metadata must not carry directory components.
		{ "Forward slash", "folder/file.txt" },

		// Backslash: same rationale, Windows-style path separator.
		{ "Backslash", "folder\\file.txt" },

		// Embedded NUL: classic injection guard for native interop and log sinks.
		{ "Embedded NUL", "file\0name.txt" }
	};

	/// <summary>
	/// Verifies that <see cref="ResourceService.UploadAsync"/> rejects malformed
	/// <c>originalFileName</c> values with an <see cref="ArgumentException"/> identifying the offending parameter.
	/// </summary>
	/// <param name="scenario">Human-readable scenario label.</param>
	/// <param name="originalFileName">The invalid file name candidate.</param>
	[Theory]
	[MemberData(nameof(UploadAsync_InvalidOriginalFileName_Data))]
	public async Task UploadAsync_WhenOriginalFileNameInvalid_ThrowsArgumentException(
		string scenario,
		string originalFileName)
	{
		_ = scenario;

		// Arrange
		var store = new FakeResourceStore();
		ResourceService sut = CreateSut(mFixture, store);
		using MemoryStream content = MakeStream("payload");

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.UploadAsync(
			         content,
			         ResourceOwnerKind.User,
			         ownerId: new ResourceOwnerId(1),
			         contentType: "text/plain",
			         createdByParticipantId: null,
			         utcNow: DateTime.UtcNow,
			         originalFileName: originalFileName));
		Assert.Equal(nameof(originalFileName), ex.ParamName);

		// Validation must happen before any IO — store must remain untouched.
		Assert.Equal(0, store.SaveCount);
	}

	#endregion
}
