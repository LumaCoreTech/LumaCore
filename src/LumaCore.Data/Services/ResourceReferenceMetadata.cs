// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// Lightweight projection of a <see cref="ResourceReferenceEntity"/> joined with its
/// <see cref="ResourceEntity"/> for display purposes (e.g., attachment lists).
/// </summary>
/// <param name="PublicId">
/// The public GUID of the <see cref="ResourceReferenceEntity"/>, used to construct the download URL.
/// </param>
/// <param name="OriginalFileName">
/// The original file name provided by the uploader, or <see langword="null"/> if none was provided.
/// </param>
/// <param name="ContentType">The MIME content type of the resource.</param>
/// <param name="SizeBytes">The file size in bytes.</param>
/// <remarks>
/// This record carries only the metadata needed to render a download link or preview in the UI.
/// It intentionally omits storage paths and internal identifiers — those are handled by
/// <see cref="IResourceService.GetDownloadInfoAsync"/>.
/// </remarks>
public sealed record ResourceReferenceMetadata(
	Guid    PublicId,
	string? OriginalFileName,
	string  ContentType,
	long    SizeBytes);
