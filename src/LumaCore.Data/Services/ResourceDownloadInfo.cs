// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Services;

/// <summary>
/// Contains the metadata needed to serve a resource download via
/// <see cref="IResourceService.GetDownloadInfoAsync"/>.
/// </summary>
/// <param name="StoragePath">
/// The relative path within the storage root, passed to <see cref="IResourceStore.OpenReadAsync"/>
/// to obtain a readable stream.
/// </param>
/// <param name="ContentType">The MIME content type to use in the HTTP response.</param>
/// <param name="OriginalFileName">
/// The original file name provided by the uploader, or <see langword="null"/> if none was provided.
/// Used for the <c>Content-Disposition</c> header.
/// </param>
/// <param name="SizeBytes">The file size in bytes, used for the <c>Content-Length</c> header.</param>
public sealed record ResourceDownloadInfo(
	string  StoragePath,
	string  ContentType,
	string? OriginalFileName,
	long    SizeBytes);
