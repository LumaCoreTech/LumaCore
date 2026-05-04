// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LumaCoreDataServiceTests
{
	/// <summary>
	/// Tests for <see cref="IResourceDataService"/> methods.
	/// </summary>
	/// <remarks>
	/// These tests cover resource reference metadata queries and set-based owner assignment for attachment
	/// wiring. The suite exercises both short-circuit paths (empty inputs) and full database interactions
	/// (joins, grouping, set-based updates).
	/// </remarks>
	[Trait("Category", "Services")]
	public sealed class Resources : TestBase
	{
		#region GetResourceReferenceMetadataByOwnersAsync

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.GetResourceReferenceMetadataByOwnersAsync"/> returns
		/// metadata grouped by owner id when multiple messages have attachments.
		/// </summary>
		[Fact]
		public async Task
			GetResourceReferenceMetadataByOwnersAsync_WhenOwnersHaveReferences_ReturnsGroupedMetadata()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ResourceEntity resource = await SeedResourceAsync(utcNow);

			var ref1PublicId = Guid.NewGuid();
			var ref2PublicId = Guid.NewGuid();

			// Two references pointing to the same resource but owned by different messages.
			Fixture.DbContext.ResourceReferences.AddRange(
				new ResourceReferenceEntity
				{
					PublicId = ref1PublicId,
					ResourceId = resource.Id,
					OwnerKind = ResourceOwnerKind.Message,
					OwnerId = new ResourceOwnerId(100),
					OriginalFileName = "doc.pdf",
					ContentType = "application/pdf",
					CreatedAtUtc = utcNow
				},
				new ResourceReferenceEntity
				{
					PublicId = ref2PublicId,
					ResourceId = resource.Id,
					OwnerKind = ResourceOwnerKind.Message,
					OwnerId = new ResourceOwnerId(200),
					OriginalFileName = null,
					ContentType = "image/png",
					CreatedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			IReadOnlyDictionary<ResourceOwnerId, IReadOnlyList<ResourceReferenceMetadata>> result =
				await service.GetResourceReferenceMetadataByOwnersAsync(
					ResourceOwnerKind.Message,
					[new ResourceOwnerId(100), new ResourceOwnerId(200)]);

			// Assert
			Assert.Equal(2, result.Count);

			ResourceReferenceMetadata meta1 = Assert.Single(result[new ResourceOwnerId(100)]);
			Assert.Equal(ref1PublicId, meta1.PublicId);
			Assert.Equal("doc.pdf", meta1.OriginalFileName);
			Assert.Equal("application/pdf", meta1.ContentType);
			Assert.Equal(1024, meta1.SizeBytes);

			ResourceReferenceMetadata meta2 = Assert.Single(result[new ResourceOwnerId(200)]);
			Assert.Equal(ref2PublicId, meta2.PublicId);
			Assert.Null(meta2.OriginalFileName);
			Assert.Equal("image/png", meta2.ContentType);
			Assert.Equal(1024, meta2.SizeBytes);
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.GetResourceReferenceMetadataByOwnersAsync"/> returns
		/// an empty dictionary when the input list is empty (short-circuit path).
		/// </summary>
		[Fact]
		public async Task GetResourceReferenceMetadataByOwnersAsync_WhenListIsEmpty_ReturnsEmptyDictionary()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			IReadOnlyDictionary<ResourceOwnerId, IReadOnlyList<ResourceReferenceMetadata>> result =
				await service.GetResourceReferenceMetadataByOwnersAsync(
					ResourceOwnerKind.Message,
					[]);

			// Assert
			Assert.Empty(result);
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.GetResourceReferenceMetadataByOwnersAsync"/> omits
		/// owner ids that have no matching resource references from the returned dictionary.
		/// </summary>
		[Fact]
		public async Task
			GetResourceReferenceMetadataByOwnersAsync_WhenNoMatchingReferences_ReturnsEmptyDictionary()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			IReadOnlyDictionary<ResourceOwnerId, IReadOnlyList<ResourceReferenceMetadata>> result =
				await service.GetResourceReferenceMetadataByOwnersAsync(
					ResourceOwnerKind.Message,
					[new ResourceOwnerId(99999)]);

			// Assert
			Assert.Empty(result);
		}

		#endregion

		#region AssignPendingReferencesToOwnerAsync

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.AssignPendingReferencesToOwnerAsync"/> updates
		/// pending references (<see cref="ResourceReferenceEntity.OwnerId"/> ==
		/// <see cref="ResourceOwnerId.Unassigned"/>) to the specified owner and returns
		/// the count of updated rows.
		/// </summary>
		[Fact]
		public async Task AssignPendingReferencesToOwnerAsync_WhenPendingReferencesExist_AssignsAndReturnsCount()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ResourceEntity resource = await SeedResourceAsync(utcNow);

			var pendingPublicId = Guid.NewGuid();
			Fixture.DbContext.ResourceReferences.Add(
				new ResourceReferenceEntity
				{
					PublicId = pendingPublicId,
					ResourceId = resource.Id,
					OwnerKind = ResourceOwnerKind.Message,
					OwnerId = ResourceOwnerId.Unassigned,
					ContentType = "text/plain",
					CreatedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			int updated = await service.AssignPendingReferencesToOwnerAsync(
				              [pendingPublicId],
				              ResourceOwnerKind.Message,
				              ownerId: new ResourceOwnerId(42));

			// Assert
			Assert.Equal(1, updated);

			// Verify the OwnerId was persisted.
			ResourceReferenceEntity? reloaded = await Fixture.DbContext.ResourceReferences
				                                    .AsNoTracking()
				                                    .FirstOrDefaultAsync(rr => rr.PublicId == pendingPublicId);
			Assert.NotNull(reloaded);
			Assert.Equal(new ResourceOwnerId(42), reloaded.OwnerId);
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.AssignPendingReferencesToOwnerAsync"/> returns 0
		/// when the input list is empty (short-circuit path).
		/// </summary>
		[Fact]
		public async Task AssignPendingReferencesToOwnerAsync_WhenListIsEmpty_ReturnsZero()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			int updated = await service.AssignPendingReferencesToOwnerAsync(
				              [],
				              ResourceOwnerKind.Message,
				              ownerId: new ResourceOwnerId(1));

			// Assert
			Assert.Equal(0, updated);
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.AssignPendingReferencesToOwnerAsync"/> does not
		/// update references that are already assigned to an owner (OwnerId != 0).
		/// </summary>
		[Fact]
		public async Task AssignPendingReferencesToOwnerAsync_WhenAlreadyAssigned_ReturnsZero()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ResourceEntity resource = await SeedResourceAsync(utcNow);

			// Reference already assigned to owner 10.
			var assignedPublicId = Guid.NewGuid();
			Fixture.DbContext.ResourceReferences.Add(
				new ResourceReferenceEntity
				{
					PublicId = assignedPublicId,
					ResourceId = resource.Id,
					OwnerKind = ResourceOwnerKind.Message,
					OwnerId = new ResourceOwnerId(10),
					ContentType = "text/plain",
					CreatedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			int updated = await service.AssignPendingReferencesToOwnerAsync(
				              [assignedPublicId],
				              ResourceOwnerKind.Message,
				              ownerId: new ResourceOwnerId(42));

			// Assert — already-assigned references must not be re-assigned.
			Assert.Equal(0, updated);

			ResourceReferenceEntity? reloaded = await Fixture.DbContext.ResourceReferences
				                                    .AsNoTracking()
				                                    .FirstOrDefaultAsync(rr => rr.PublicId == assignedPublicId);
			Assert.NotNull(reloaded);
			Assert.Equal(new ResourceOwnerId(10), reloaded.OwnerId);
		}

		#endregion

		#region CloneResourceReferencesAsync

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.CloneResourceReferencesAsync"/> creates new references
		/// owned by the target entity that point to the same underlying resources, with fresh public identifiers
		/// and the supplied creation timestamp.
		/// </summary>
		[Fact]
		public async Task CloneResourceReferencesAsync_WhenSourcesExist_ClonesWithFreshPublicIds()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcSource = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime utcClone = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

			ResourceEntity resource = await SeedResourceAsync(utcSource);

			var sourcePublicId = Guid.NewGuid();
			Fixture.DbContext.ResourceReferences.Add(
				new ResourceReferenceEntity
				{
					PublicId = sourcePublicId,
					ResourceId = resource.Id,
					OwnerKind = ResourceOwnerKind.Message,
					OwnerId = new ResourceOwnerId(100),
					OriginalFileName = "doc.pdf",
					ContentType = "application/pdf",
					CreatedAtUtc = utcSource
				});
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			IReadOnlyList<ResourceReferenceMetadata> result = await service.CloneResourceReferencesAsync(
				                                                  [sourcePublicId],
				                                                  ResourceOwnerKind.Message,
				                                                  ownerId: new ResourceOwnerId(200),
				                                                  utcClone);

			// Assert — returned metadata mirrors the source but with a fresh PublicId.
			ResourceReferenceMetadata clone = Assert.Single(result);
			Assert.NotEqual(Guid.Empty, clone.PublicId);
			Assert.NotEqual(sourcePublicId, clone.PublicId);
			Assert.Equal("doc.pdf", clone.OriginalFileName);
			Assert.Equal("application/pdf", clone.ContentType);
			Assert.Equal(1024, clone.SizeBytes);

			// Assert — clone row was persisted with the correct owner and timestamp.
			ResourceReferenceEntity? persisted = await Fixture.DbContext.ResourceReferences
				                                     .AsNoTracking()
				                                     .FirstOrDefaultAsync(rr => rr.PublicId == clone.PublicId);
			Assert.NotNull(persisted);
			Assert.Equal(resource.Id, persisted.ResourceId);
			Assert.Equal(ResourceOwnerKind.Message, persisted.OwnerKind);
			Assert.Equal(new ResourceOwnerId(200), persisted.OwnerId);
			Assert.Equal("doc.pdf", persisted.OriginalFileName);
			Assert.Equal("application/pdf", persisted.ContentType);
			Assert.Equal(utcClone, persisted.CreatedAtUtc);

			// Assert — the source reference was not modified or removed.
			ResourceReferenceEntity? source = await Fixture.DbContext.ResourceReferences
				                                  .AsNoTracking()
				                                  .FirstOrDefaultAsync(rr => rr.PublicId == sourcePublicId);
			Assert.NotNull(source);
			Assert.Equal(new ResourceOwnerId(100), source.OwnerId);
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.CloneResourceReferencesAsync"/> creates one clone per
		/// source reference with distinct public identifiers when multiple sources are supplied.
		/// </summary>
		[Fact]
		public async Task CloneResourceReferencesAsync_WhenMultipleSources_ClonesAllWithDistinctPublicIds()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ResourceEntity res1 = await SeedResourceAsync(utcNow);
			ResourceEntity res2 = await SeedResourceAsync(utcNow);

			var src1 = Guid.NewGuid();
			var src2 = Guid.NewGuid();
			Fixture.DbContext.ResourceReferences.AddRange(
				new ResourceReferenceEntity
				{
					PublicId = src1, ResourceId = res1.Id, OwnerKind = ResourceOwnerKind.Message,
					OwnerId = new ResourceOwnerId(100), OriginalFileName = "a.txt", ContentType = "text/plain",
					CreatedAtUtc = utcNow
				},
				new ResourceReferenceEntity
				{
					PublicId = src2, ResourceId = res2.Id, OwnerKind = ResourceOwnerKind.Message,
					OwnerId = new ResourceOwnerId(100), OriginalFileName = "b.png", ContentType = "image/png",
					CreatedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			IReadOnlyList<ResourceReferenceMetadata> result = await service.CloneResourceReferencesAsync(
				                                                  [src1, src2],
				                                                  ResourceOwnerKind.Message,
				                                                  ownerId: new ResourceOwnerId(200),
				                                                  utcNow);

			// Assert — two clones returned with distinct, fresh PublicIds.
			Assert.Equal(2, result.Count);
			Assert.NotEqual(result[0].PublicId, result[1].PublicId);
			Assert.DoesNotContain(src1, result.Select(r => r.PublicId));
			Assert.DoesNotContain(src2, result.Select(r => r.PublicId));

			// Assert — both clones persisted under the new owner.
			int clonedCount = await Fixture.DbContext.ResourceReferences
				                  .AsNoTracking()
				                  .CountAsync(rr => rr.OwnerId == new ResourceOwnerId(200));
			Assert.Equal(2, clonedCount);
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.CloneResourceReferencesAsync"/> short-circuits and
		/// returns an empty list when the input list is empty.
		/// </summary>
		[Fact]
		public async Task CloneResourceReferencesAsync_WhenListIsEmpty_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			IReadOnlyList<ResourceReferenceMetadata> result = await service.CloneResourceReferencesAsync(
				                                                  [],
				                                                  ResourceOwnerKind.Message,
				                                                  ownerId: new ResourceOwnerId(1),
				                                                  DateTime.UtcNow);

			// Assert
			Assert.Empty(result);
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.CloneResourceReferencesAsync"/> returns an empty list
		/// when none of the supplied source identifiers match an existing reference.
		/// </summary>
		[Fact]
		public async Task CloneResourceReferencesAsync_WhenNoSourcesFound_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			IReadOnlyList<ResourceReferenceMetadata> result = await service.CloneResourceReferencesAsync(
				                                                  [Guid.NewGuid()],
				                                                  ResourceOwnerKind.Message,
				                                                  ownerId: new ResourceOwnerId(1),
				                                                  DateTime.UtcNow);

			// Assert
			Assert.Empty(result);
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.CloneResourceReferencesAsync"/> transparently retries
		/// with a fresh <see cref="ResourceReferenceEntity.PublicId"/> when the first attempt collides with an
		/// existing row, and that the operation ultimately succeeds.
		/// </summary>
		/// <remarks>
		/// The collision is forced deterministically by hooking <see cref="DbContext.SavingChanges"/>: just before
		/// the first <c>SaveChanges</c> reaches the database, a side context inserts a row whose <c>PublicId</c>
		/// matches the pending clone, which triggers the UNIQUE constraint on save. The hook fires only once so
		/// the retry attempt sees a fresh GUID and succeeds.
		/// </remarks>
		[Fact]
		public async Task CloneResourceReferencesAsync_WhenPublicIdCollidesOnce_RetriesAndSucceeds()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ResourceEntity resource = await SeedResourceAsync(utcNow);

			var sourcePublicId = Guid.NewGuid();
			Fixture.DbContext.ResourceReferences.Add(
				new ResourceReferenceEntity
				{
					PublicId = sourcePublicId,
					ResourceId = resource.Id,
					OwnerKind = ResourceOwnerKind.Message,
					OwnerId = new ResourceOwnerId(100),
					ContentType = "text/plain",
					CreatedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			int hookFireCount = 0;

			void OnSavingChanges(object? sender, SavingChangesEventArgs _)
			{
				if (Interlocked.Increment(ref hookFireCount) != 1)
					return;

				// Read the pending clone's PublicId and inject a colliding row via a side context so that
				// the imminent SaveChanges on the main context fails the UNIQUE constraint on PublicId.
				ResourceReferenceEntity pending = Fixture.DbContext.ChangeTracker
					.Entries<ResourceReferenceEntity>()
					.Single(e => e.State == EntityState.Added)
					.Entity;

				using LumaCoreDbContext side = Fixture.CreateDbContext();
				side.ResourceReferences.Add(
					new ResourceReferenceEntity
					{
						PublicId = pending.PublicId,
						ResourceId = resource.Id,
						OwnerKind = ResourceOwnerKind.Message,
						OwnerId = new ResourceOwnerId(999),
						ContentType = "text/plain",
						CreatedAtUtc = utcNow
					});
				side.SaveChanges();
			}

			Fixture.DbContext.SavingChanges += OnSavingChanges;

			try
			{
				// Act
				IReadOnlyList<ResourceReferenceMetadata> result = await service.CloneResourceReferencesAsync(
					                                                  [sourcePublicId],
					                                                  ResourceOwnerKind.Message,
					                                                  ownerId: new ResourceOwnerId(200),
					                                                  utcNow);

				// Assert — the hook was invoked twice (collision attempt + retry attempt) but only the
				// first invocation injected a colliding row; the retry's SaveChanges then succeeded.
				Assert.Equal(2, hookFireCount);
				ResourceReferenceMetadata clone = Assert.Single(result);
				Assert.NotEqual(Guid.Empty, clone.PublicId);

				ResourceReferenceEntity? persisted = await Fixture.DbContext.ResourceReferences
					                                     .AsNoTracking()
					                                     .FirstOrDefaultAsync(rr => rr.PublicId == clone.PublicId
					                                                                && rr.OwnerId ==
					                                                                new ResourceOwnerId(200));
				Assert.NotNull(persisted);
			}
			finally
			{
				Fixture.DbContext.SavingChanges -= OnSavingChanges;
			}
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.CloneResourceReferencesAsync"/> rethrows a
		/// <see cref="DbUpdateException"/> when every retry attempt collides on
		/// <see cref="ResourceReferenceEntity.PublicId"/>, and leaves the change tracker clean.
		/// </summary>
		/// <remarks>
		/// The collision is forced deterministically on every save attempt by hooking
		/// <see cref="DbContext.SavingChanges"/> without a fire-once guard.
		/// </remarks>
		[Fact]
		public async Task CloneResourceReferencesAsync_WhenAllAttemptsCollide_ThrowsDbUpdateException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ResourceEntity resource = await SeedResourceAsync(utcNow);

			var sourcePublicId = Guid.NewGuid();
			Fixture.DbContext.ResourceReferences.Add(
				new ResourceReferenceEntity
				{
					PublicId = sourcePublicId,
					ResourceId = resource.Id,
					OwnerKind = ResourceOwnerKind.Message,
					OwnerId = new ResourceOwnerId(100),
					ContentType = "text/plain",
					CreatedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			void OnSavingChanges(object? sender, SavingChangesEventArgs _)
			{
				ResourceReferenceEntity pending = Fixture.DbContext.ChangeTracker
					.Entries<ResourceReferenceEntity>()
					.Single(e => e.State == EntityState.Added)
					.Entity;

				using LumaCoreDbContext side = Fixture.CreateDbContext();
				side.ResourceReferences.Add(
					new ResourceReferenceEntity
					{
						PublicId = pending.PublicId,
						ResourceId = resource.Id,
						OwnerKind = ResourceOwnerKind.Message,
						OwnerId = new ResourceOwnerId(999),
						ContentType = "text/plain",
						CreatedAtUtc = utcNow
					});
				side.SaveChanges();
			}

			Fixture.DbContext.SavingChanges += OnSavingChanges;

			try
			{
				// Act + Assert
				await Assert.ThrowsAsync<DbUpdateException>(() => service.CloneResourceReferencesAsync(
					[sourcePublicId],
					ResourceOwnerKind.Message,
					ownerId: new ResourceOwnerId(200),
					utcNow));

				// Assert — change tracker was cleaned up on the failure path.
				Assert.DoesNotContain(
					Fixture.DbContext.ChangeTracker.Entries<ResourceReferenceEntity>(),
					e => e.State == EntityState.Added);
			}
			finally
			{
				Fixture.DbContext.SavingChanges -= OnSavingChanges;
			}
		}

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.CloneResourceReferencesAsync"/> throws
		/// <see cref="ArgumentNullException"/> when <c>sourceReferencePublicIds</c> is <see langword="null"/>,
		/// matching the documented contract and the sister mutation
		/// <see cref="IResourceDataService.AssignPendingReferencesToOwnerAsync"/>.
		/// </summary>
		[Fact]
		public async Task CloneResourceReferencesAsync_WhenSourceReferencePublicIdsIsNull_ThrowsArgumentNullException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.CloneResourceReferencesAsync(
				         sourceReferencePublicIds: null!,
				         ResourceOwnerKind.Message,
				         ownerId: new ResourceOwnerId(1),
				         DateTime.UtcNow));
			Assert.Equal("sourceReferencePublicIds", ex.ParamName);
		}

		#endregion

		#region UTC clock fallback

		/// <summary>
		/// Verifies that <see cref="IResourceDataService.CloneResourceReferencesAsync"/> falls back to the
		/// injected <see cref="TimeProvider"/> for the cloned reference's <c>CreatedAtUtc</c> when the optional
		/// <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c>.
		/// </remarks>
		[Fact]
		public async Task CloneResourceReferencesAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime seedNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			ResourceEntity resource = await SeedResourceAsync(seedNow);
			var sourcePublicId = Guid.NewGuid();
			Fixture.DbContext.ResourceReferences.Add(
				new ResourceReferenceEntity
				{
					PublicId = sourcePublicId,
					ResourceId = resource.Id,
					OwnerKind = ResourceOwnerKind.Message,
					OwnerId = new ResourceOwnerId(100),
					OriginalFileName = "doc.pdf",
					ContentType = "application/pdf",
					CreatedAtUtc = seedNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			IReadOnlyList<ResourceReferenceMetadata> result = await service.CloneResourceReferencesAsync(
				                                                  [sourcePublicId],
				                                                  ResourceOwnerKind.Message,
				                                                  ownerId: new ResourceOwnerId(200));

			// Assert
			ResourceReferenceMetadata clone = Assert.Single(result);
			ResourceReferenceEntity? persisted = await Fixture.DbContext.ResourceReferences
				                                     .AsNoTracking()
				                                     .FirstOrDefaultAsync(r => r.PublicId == clone.PublicId);
			Assert.NotNull(persisted);
			Assert.Equal(fixedNow, persisted.CreatedAtUtc);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Seeds a minimal <see cref="ResourceEntity"/> for tests that need a resource to reference.
		/// </summary>
		/// <param name="utcNow">The UTC timestamp used for <see cref="ResourceEntity.CreatedAtUtc"/>.</param>
		/// <returns>The created <see cref="ResourceEntity"/>.</returns>
		private async Task<ResourceEntity> SeedResourceAsync(DateTime utcNow)
		{
			var resource = new ResourceEntity
			{
				ContentHash = Guid.NewGuid().ToString("N"),
				StoragePath = $"test/{Guid.NewGuid()}",
				SizeBytes = 1024,
				CreatedAtUtc = utcNow,
				DeletionState = ResourceDeletionState.Active
			};
			Fixture.DbContext.Resources.Add(resource);
			await Fixture.DbContext.SaveChangesAsync().ConfigureAwait(false);
			return resource;
		}

		#endregion
	}
}
