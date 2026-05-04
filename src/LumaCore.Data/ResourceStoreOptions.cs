// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Data;

/// <summary>
/// Provides configuration settings for resource file storage in LumaCore.
/// </summary>
/// <remarks>
///     <para>
///     This configuration is typically loaded from <c>appsettings.json</c> under the section specified by
///     <see cref="SectionName"/>. It controls where uploaded files (images, documents, etc.) are persisted
///     on the filesystem.
///     </para>
///     <para>
///     Values are bound via the options pattern and validated during startup.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     "ResourceStorage": {
///         "StorageRootPath": "./resources"
///     }
///     </code>
/// </example>
public sealed class ResourceStoreOptions : IValidatableObject
{
	/// <summary>
	/// The configuration section name for resource storage options.
	/// </summary>
	public const string SectionName = "ResourceStorage";

	/// <summary>
	/// Provides the error message when the storage root path is not configured.
	/// </summary>
	private const string StorageRootPathRequiredError =
		"ResourceStorage:StorageRootPath must be configured. Set configuration key 'ResourceStorage:StorageRootPath' " +
		"or environment variable 'ResourceStorage__StorageRootPath'.";

	/// <summary>
	/// Gets or sets the root directory path where resource files are stored.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Can be an absolute path or a path relative to the application's working directory.
	///     The directory is created automatically on first use if it does not exist.
	///     </para>
	///     <para>
	///     All resource files are stored as flat GUID-based filenames (no extension) under this directory.
	///     </para>
	/// </remarks>
	[Required(AllowEmptyStrings = false, ErrorMessage = StorageRootPathRequiredError)]
	public string StorageRootPath { get; set; } = "./resources";

	/// <inheritdoc/>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (string.IsNullOrWhiteSpace(StorageRootPath))
		{
			yield break;
		}

		// Path.GetFullPath() throws on invalid characters and other syntactic issues.
		// Cannot yield from a catch block, so capture the error and yield afterwards.
		string? pathError = null;
		try
		{
			Path.GetFullPath(StorageRootPath);
		}
		catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
		{
			pathError = ex.Message;
		}

		if (pathError is not null)
		{
			yield return new ValidationResult(
				$"ResourceStorage:StorageRootPath contains an invalid path: '{StorageRootPath}'. {pathError}",
				[nameof(StorageRootPath)]);
		}
	}
}
