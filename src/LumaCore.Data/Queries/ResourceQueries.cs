// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Queries;

/// <summary>
/// Provides pre-compiled queries for resource and resource-reference operations.
/// </summary>
/// <remarks>
///     <para>
///     Compiled queries eliminate the overhead of expression-tree parsing and SQL generation on each
///     execution. Use these for frequently-executed queries in hot paths.
///     </para>
///     <para>
///     <b>Important:</b> EF Core compiled queries do not accept a <see cref="CancellationToken"/>.
///     Cancellation is "best effort" only — the caller stops awaiting, but the underlying database
///     operation may still run to completion. Consider this trade-off when using these queries in
///     contexts where responsiveness to cancellation is critical.
///     </para>
///     <para>
///     All query delegates in this class are thread-safe and can be used concurrently. The
///     <see cref="LumaCoreDbContext"/> instances passed to them are not thread-safe and must remain scoped.
///     </para>
///     <para>
///     <b>Why no list-based queries here:</b> EF Core compiled queries do not support
///     <c>IEnumerable.Contains</c> over a parameter list (the SQL shape varies with the list length, so
///     the query cannot be cached as a single delegate). Operations like
///     <see cref="IResourceDataService.GetResourceReferenceMetadataByOwnersAsync"/> and
///     <see cref="IResourceDataService.CloneResourceReferencesAsync"/> therefore stay on the dynamic LINQ
///     path, matching the existing convention in the other <c>*Queries</c> classes.
///     </para>
/// </remarks>
public static class ResourceQueries
{
	/// <summary>
	/// Looks up an <see cref="ResourceDeletionState.Active"/> resource by its content hash to support
	/// upload deduplication.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Executed on every upload before any file I/O. A hit lets the upload pipeline reuse the existing
	///     resource row instead of writing a duplicate file.
	///     </para>
	///     <para>
	///     <b>Note:</b> unlike the other lookup queries in this class, this query does <b>not</b> apply
	///     <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>. The current callers in
	///     <c>ResourceService.UploadAsync</c> rely on the returned entity being tracked so they can issue
	///     explicit <c>Detach</c> calls around <c>ExecuteUpdate</c> and after a detected MARK race. A future
	///     refactor should migrate both this query and its dynamic-LINQ counterpart to no-tracking semantics
	///     together and remove the now-redundant detach calls.
	///     </para>
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, string, Task<ResourceEntity?>>
		GetActiveByContentHash = EF.CompileAsyncQuery((LumaCoreDbContext ctx, string contentHash) =>
			ctx.Resources
				.FirstOrDefault(r => r.ContentHash == contentHash &&
				                     r.DeletionState == ResourceDeletionState.Active));

	/// <summary>
	/// Reads the current <see cref="ResourceEntity.DeletionState"/> for a given resource id, bypassing
	/// the change tracker so the caller observes the database value rather than a cached snapshot.
	/// </summary>
	/// <remarks>
	/// Used by the upload pipeline to detect the MARK race window between the dedup lookup and the
	/// reference attach. Returns <see langword="null"/> only if the row was deleted concurrently;
	/// callers must treat that as "not active".
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ResourceId, Task<ResourceDeletionState?>>
		GetDeletionStateById = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ResourceId id) =>
			ctx.Resources
				.AsNoTracking()
				.Where(r => r.Id == id)
				.Select(r => (ResourceDeletionState?)r.DeletionState)
				.FirstOrDefault());

	/// <summary>
	/// Resolves the download metadata for a public resource-reference identifier in a single round-trip.
	/// </summary>
	/// <remarks>
	/// Used on every resource download (avatars, message attachments). Joins
	/// <see cref="ResourceReferenceEntity"/> with <see cref="ResourceEntity"/> so the caller obtains the
	/// storage path, content type, original file name, and size in one query.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, Guid, Task<ResourceDownloadInfo?>>
		GetDownloadInfoByPublicId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, Guid publicId) =>
			ctx.ResourceReferences
				.Where(rr => rr.PublicId == publicId)
				.Join(
					ctx.Resources,
					rr => rr.ResourceId,
					r => r.Id,
					(rr, r) => new ResourceDownloadInfo(
						r.StoragePath,
						rr.ContentType,
						rr.OriginalFileName,
						r.SizeBytes))
				.FirstOrDefault());
}
