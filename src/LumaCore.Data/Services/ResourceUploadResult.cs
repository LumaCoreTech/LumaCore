// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Services;

/// <summary>
/// Contains the outcome of a successful resource upload via <see cref="IResourceService.UploadAsync"/>.
/// </summary>
/// <param name="ReferencePublicId">
/// The public GUID of the newly created <see cref="Entities.ResourceReferenceEntity"/>,
/// suitable for constructing the download URL.
/// </param>
/// <param name="ContentHash">The SHA-256 hash of the uploaded content (lowercase hex, 64 characters).</param>
/// <param name="SizeBytes">The size of the stored file in bytes.</param>
/// <param name="WasDeduplicated">
/// <see langword="true"/> if an existing resource with the same content hash was reused
/// (no new file was written to storage); <see langword="false"/> if a new file was persisted.
/// </param>
public sealed record ResourceUploadResult(
	Guid   ReferencePublicId,
	string ContentHash,
	long   SizeBytes,
	bool   WasDeduplicated);
