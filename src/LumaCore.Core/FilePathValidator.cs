// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;
using System.Runtime.CompilerServices;

namespace LumaCore.Core;

/// <summary>
/// Validates file paths for correctness on the current operating system before they are used to open files.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="Path.GetFullPath(string)"/> in modern .NET (Core 3.1+) is intentionally permissive — it only
///     rejects the NUL character (<c>\0</c>) and structural format errors. Characters that are invalid on Windows
///     (such as <c>&lt;</c>, <c>&gt;</c>, <c>|</c>) pass through unchecked because the runtime defers character
///     validation to the operating system.
///     </para>
///     <para>
///     This class supplements <see cref="Path.GetFullPath(string)"/> with explicit per-segment validation using
///     <see cref="Path.GetInvalidFileNameChars()"/>, which returns the OS-specific set of characters forbidden in
///     file and directory names. Directory separators are excluded from the check because they are structurally
///     valid in a full path. Additionally, each segment is checked against a 255-character length limit that
///     reflects the <c>NAME_MAX</c> constraint on most file systems.
///     </para>
/// </remarks>
public static class FilePathValidator
{
	/// <summary>
	/// Maximum number of characters allowed in a single path segment (file or directory name).
	/// </summary>
	/// <remarks>
	/// Most file systems enforce a limit around 255 units per name component: ext4, btrfs, and XFS on Linux
	/// impose a 255-byte <c>NAME_MAX</c> (effectively 255 ASCII characters), and NTFS on Windows limits each
	/// segment to 255 UTF-16 code units. This constant uses 255 characters as a conservative cross-platform
	/// heuristic that catches the most common violations.
	/// </remarks>
	private const int MaxSegmentLength = 255;

	/// <summary>
	/// Pre-computed set of characters that are invalid in file or directory names on the current operating
	/// system, excluding directory separators (which are structurally valid in a full path).
	/// </summary>
	/// <remarks>
	///     <list type="bullet">
	///         <item>
	///         <b>Windows:</b> <c>\0</c>, control characters <c>0x01–0x1F</c>, <c>"</c>, <c>&lt;</c>,
	///         <c>&gt;</c>, <c>|</c>, <c>:</c>, <c>*</c>, <c>?</c>
	///         </item>
	///         <item>
	///         <b>Linux:</b> <c>\0</c> only — all other characters are valid in file names on ext4,
	///         btrfs, XFS, and most POSIX file systems.
	///         </item>
	///     </list>
	///     <para>
	///     On Windows, the volume separator (<c>:</c>) is intentionally <b>not</b> removed from this set.
	///     A colon is only valid in the drive root (<c>C:\</c>), which is skipped via
	///     <see cref="Path.GetPathRoot(ReadOnlySpan{char})"/>. A colon in any other segment (e.g., NTFS
	///     Alternate Data Streams) is rejected.
	///     </para>
	/// </remarks>
	private static readonly SearchValues<char> sInvalidSegmentChars = CreateInvalidSegmentChars();

	/// <summary>
	/// Validates that <paramref name="filePath"/> is not <see langword="null"/>, empty, or white-space, and
	/// that it represents a well-formed file system path for the current operating system.
	/// </summary>
	/// <param name="filePath">The file path to validate.</param>
	/// <param name="paramName">
	/// The parameter name for exception messages. Automatically inferred via
	/// <see cref="CallerArgumentExpressionAttribute"/>.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="filePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="filePath"/> is empty, consists only of white-space characters, contains characters that
	/// are invalid on the current operating system, or contains a path segment that exceeds the maximum allowed
	/// length of 255 characters.
	/// </exception>
	public static void Validate(
		string filePath,
		[CallerArgumentExpression(nameof(filePath))]
		string? paramName = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath, paramName);

		// Path.GetFullPath() normalizes the path and catches NUL characters and structural format
		// errors (e.g., NotSupportedException for malformed volume syntax on Windows).
		try
		{
			Path.GetFullPath(filePath);
		}
		catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
		{
			throw new ArgumentException(
				"The file path has an invalid format for the current operating system.",
				paramName,
				ex);
		}

		// Path.GetFullPath() in modern .NET only rejects NUL characters. Supplement it with
		// per-segment validation against OS-specific invalid characters and length limits.
		ValidateSegments(filePath, paramName);
	}

	/// <summary>
	/// Checks each segment of <paramref name="filePath"/> for invalid characters and excessive length.
	/// </summary>
	/// <param name="filePath">The file path whose segments to validate.</param>
	/// <param name="paramName">The parameter name for exception messages.</param>
	/// <exception cref="ArgumentException">
	/// A segment contains invalid characters or exceeds <see cref="MaxSegmentLength"/> characters.
	/// </exception>
	private static void ValidateSegments(string filePath, string? paramName)
	{
		ReadOnlySpan<char> remaining = filePath.AsSpan();

		// Skip the root portion (e.g., "C:\" on Windows, "/" on Linux) — it contains separators
		// and the volume separator by design and must not be segment-checked.
		ReadOnlySpan<char> root = Path.GetPathRoot(remaining);
		remaining = remaining[root.Length..];

		while (remaining.Length > 0)
		{
			int separatorIndex = remaining.IndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			ReadOnlySpan<char> segment = separatorIndex >= 0
				                             ? remaining[..separatorIndex]
				                             : remaining;

			if (segment.Length > 0)
			{
				if (segment.Length > MaxSegmentLength)
				{
					throw new ArgumentException(
						$"The file path contains a segment that exceeds the maximum length of {MaxSegmentLength} characters.",
						paramName);
				}

				if (segment.ContainsAny(sInvalidSegmentChars))
				{
					throw new ArgumentException(
						"The file path contains characters that are invalid on the current operating system.",
						paramName);
				}
			}

			remaining = separatorIndex >= 0 ? remaining[(separatorIndex + 1)..] : [];
		}
	}

	/// <summary>
	/// Builds a <see cref="SearchValues{T}"/> containing all characters from
	/// <see cref="Path.GetInvalidFileNameChars()"/> except directory separators.
	/// </summary>
	/// <returns>An optimized search structure for invalid segment characters on the current OS.</returns>
	private static SearchValues<char> CreateInvalidSegmentChars()
	{
		// Path.GetInvalidFileNameChars() returns characters that are invalid in individual file and
		// directory names. This includes the directory separator characters (/ and \ on Windows, / on
		// Linux), which are structurally valid in a full path and must be excluded from segment checks.
		char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
		char[] invalidChars = Path.GetInvalidFileNameChars()
			.Where(c => !separators.Contains(c))
			.ToArray();

		return SearchValues.Create(invalidChars);
	}
}
