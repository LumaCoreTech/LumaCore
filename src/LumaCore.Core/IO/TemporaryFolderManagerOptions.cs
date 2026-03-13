// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.IO;

/// <summary>
/// Configuration options for <see cref="TemporaryFolderManager"/>.
/// </summary>
/// <remarks>
///     <para>
///     This class is used with the options pattern (<c>IOptions&lt;TemporaryFolderManagerOptions&gt;</c>) to configure
///     the <see cref="TemporaryFolderManager"/> when registered via dependency injection.
///     </para>
///     <para>
///         <b>Configuration example (appsettings.json):</b>
///         <code>
/// {
///   "TemporaryFolders": {
///     "BasePath": "/var/tmp/lumacore"
///   }
/// }
///     </code>
///     </para>
/// </remarks>
public sealed class TemporaryFolderManagerOptions
{
	/// <summary>
	/// The default configuration section name for <see cref="TemporaryFolderManagerOptions"/>.
	/// </summary>
	public const string DefaultSectionName = "TemporaryFolders";

	/// <summary>
	/// Gets or sets the base directory under which all managed temporary folders are created.
	/// </summary>
	/// <remarks>
	/// The directory is created automatically if it does not exist. The default value places temporary folders under
	/// a <c>LumaCore</c> subdirectory of the system's temporary directory (e.g., <c>/tmp/LumaCore</c> on Linux or
	/// <c>%TEMP%\LumaCore</c> on Windows).
	/// </remarks>
	/// <value>
	/// The absolute or relative path to the base directory. Default is <c>{TempPath}/LumaCore</c>.
	/// </value>
	public string BasePath { get; set; } = Path.Combine(Path.GetTempPath(), "LumaCore");
}
