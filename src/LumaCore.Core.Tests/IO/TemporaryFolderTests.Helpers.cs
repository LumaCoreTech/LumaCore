// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;

using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Core.Tests.IO;

public sealed partial class TemporaryFolderTests
{
	/// <summary>
	/// Regex pattern matching a 32-character lowercase hexadecimal GUID string (format "N").
	/// </summary>
	private const string GuidPattern = "^[0-9a-f]{32}$";

	/// <summary>
	/// Creates an isolated temporary base directory for a single test. The caller is responsible for cleaning it up
	/// via <see cref="CleanupBasePath"/> in a <c>finally</c> block.
	/// </summary>
	/// <returns>The absolute path to the newly created base directory.</returns>
	private static string CreateIsolatedBasePath()
	{
		string path = Path.Combine(Path.GetTempPath(), $"LumaCore-Tests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	/// <summary>
	/// Deletes the specified directory and all its contents. Best-effort: I/O exceptions are swallowed.
	/// </summary>
	/// <param name="path">The absolute path to the directory to delete.</param>
	private static void CleanupBasePath(string path)
	{
		try
		{
			if (Directory.Exists(path))
				Directory.Delete(path, recursive: true);
		}
		catch
		{
			// Best-effort cleanup.
		}
	}

	/// <summary>
	/// Creates a <see cref="TemporaryFolderManager"/> configured to use the specified base path.
	/// Intended for tests that need managed-mode <see cref="TemporaryFolder"/> instances.
	/// </summary>
	/// <param name="basePath">The base directory for managed temporary folders.</param>
	/// <returns>A new <see cref="TemporaryFolderManager"/> instance.</returns>
	private static TemporaryFolderManager CreateManagerForTesting(string basePath)
	{
		IOptions<TemporaryFolderManagerOptions> options =
			Options.Create(new TemporaryFolderManagerOptions { BasePath = basePath });
		return new TemporaryFolderManager(options);
	}

	/// <summary>
	/// Asserts that the specified folder name matches the expected pattern based on whether a prefix was provided.
	/// </summary>
	/// <param name="folderName">The folder name (not the full path) to validate.</param>
	/// <param name="expectedPrefix">
	/// The prefix that should appear before the GUID, or <see langword="null"/> if the folder name should be a
	/// GUID only.
	/// </param>
	// ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
	private static void AssertFolderNamePattern(string folderName, string? expectedPrefix)
	{
		if (expectedPrefix is not null)
		{
			Assert.StartsWith(expectedPrefix + "-", folderName);
			string guidPart = folderName[(expectedPrefix.Length + 1)..];
			Assert.Matches(GuidPattern, guidPart);
		}
		else
		{
			Assert.Matches(GuidPattern, folderName);
		}
	}
}
