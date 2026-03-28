// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Core.Tests;

/// <summary>
/// Unit tests for <see cref="FilePathValidator"/>.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="FilePathValidator.Validate"/> coverage: from accepted paths through contract violations to
///     OS-specific rejections. These tests exercise the three validation layers — null/whitespace guard,
///     <see cref="Path.GetFullPath(string)"/> structural check, and per-segment character/length validation —
///     plus the <c>CallerArgumentExpressionAttribute</c> parameter-name
///     plumbing:
///     </para>
///     <list type="number">
///         <item>
///         <b>Happy path</b> — valid absolute/relative paths and the 255-char segment boundary pass without
///         exceptions (<see cref="ValidPathFormats"/>).
///         </item>
///         <item>
///         <b>Contract violations</b> — <see langword="null"/>, empty, and whitespace inputs are rejected
///         immediately by the <c>ThrowIfNullOrWhiteSpace</c> guard.
///         </item>
///         <item>
///         <b>OS-specific invalid format</b> — NUL characters and segment-length overflows (cross-platform),
///         plus reserved characters like <c>&lt; &gt; | " * ? :</c> and control characters on Windows
///         (<see cref="InvalidPathFormats"/>).
///         </item>
///         <item>
///         <b>CallerArgumentExpression</b> — parameter name inference from the call site and explicit override.
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Core")]
public sealed class FilePathValidatorTests
{
	// --- 1. Happy path: valid paths pass through without exceptions ---

	/// <summary>
	/// Valid file paths that should pass validation on the current operating system.
	/// </summary>
	public static TheoryData<string, string> ValidPathFormats
	{
		get
		{
			var data = new TheoryData<string, string>();

			if (OperatingSystem.IsWindows())
			{
				// Absolute Windows path with drive letter
				data.Add("absolute Windows path", @"C:\Users\test\file.txt");

				// Relative path with subdirectories
				data.Add("relative path", @"folder\subfolder\file.txt");

				// Segment at exactly 255 characters (boundary — valid)
				data.Add("255-char segment (boundary)", @"C:\folder\" + new string('a', 255));
			}
			else
			{
				// Absolute POSIX path
				data.Add("absolute POSIX path", "/tmp/test/file.txt");

				// Relative path
				data.Add("relative path", "folder/subfolder/file.txt");

				// Segment at exactly 255 characters (boundary — valid)
				data.Add("255-char segment (boundary)", "/tmp/" + new string('a', 255));

				// Characters that are valid on Linux but invalid on Windows
				data.Add("angle brackets (valid on Linux)", "test<file>.txt");
				data.Add("pipe character (valid on Linux)", "test|file.txt");
			}

			return data;
		}
	}

	/// <summary>
	/// Verifies that <see cref="FilePathValidator.Validate"/> accepts valid file paths without throwing.
	/// </summary>
	/// <param name="scenario">A human-readable description of the test case.</param>
	/// <param name="filePath">The valid file path to test.</param>
	[Theory]
	[MemberData(nameof(ValidPathFormats))]
	public void Validate_WhenFilePathIsValid_DoesNotThrow(string scenario, string filePath)
	{
		_ = scenario;

		// Act + Assert — no exception expected
		FilePathValidator.Validate(filePath);
	}

	// --- 2. Contract violations: null, empty, and whitespace ---

	/// <summary>
	/// Verifies that <see cref="FilePathValidator.Validate"/> throws <see cref="ArgumentNullException"/> when
	/// <c>filePath</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Validate_WhenFilePathIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		string? filePath = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => FilePathValidator.Validate(filePath!));
		Assert.Equal(nameof(filePath), ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="FilePathValidator.Validate"/> throws <see cref="ArgumentException"/> when
	/// <c>filePath</c> is empty or consists only of white-space characters.
	/// </summary>
	/// <param name="scenario">A human-readable description of the test case.</param>
	/// <param name="filePath">The invalid file path to test.</param>
	[Theory]
	[InlineData("empty string", "")]
	[InlineData("single space", " ")]
	[InlineData("tab character", "\t")]
	public void Validate_WhenFilePathIsEmptyOrWhiteSpace_ThrowsArgumentException(string scenario, string filePath)
	{
		_ = scenario;

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => FilePathValidator.Validate(filePath));
		Assert.Equal("filePath", ex.ParamName);
	}

	// --- 3. OS-specific invalid format: characters and segment length ---

	/// <summary>
	/// File paths with invalid characters or structural format violations for the current operating system.
	/// Each row contains the scenario name, the invalid path, and the expected <see cref="FilePathValidator"/>
	/// error message prefix.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Cross-platform cases</b> run on every OS. The NUL character is universally invalid, and path
	///     segments exceeding 255 characters violate the <c>NAME_MAX</c> / component-length limit on most file
	///     systems.
	///     </para>
	///     <para>
	///     <b>Windows-only cases</b> are conditionally included when the test runs on Windows. NTFS and ReFS
	///     prohibit a broader set of characters that are perfectly valid on POSIX file systems (ext4, btrfs, XFS,
	///     APFS). On Linux, <see cref="Path.GetInvalidFileNameChars()"/> returns only <c>\0</c> and <c>/</c> —
	///     making the character set effectively limited to the NUL byte after directory separators are excluded.
	///     </para>
	/// </remarks>
	public static TheoryData<string, string, string> InvalidPathFormats
	{
		get
		{
			var data = new TheoryData<string, string, string>
			{
				// --- Cross-platform: invalid on ALL operating systems ---

				// NUL (\0) is the only character universally forbidden in file paths across every
				// operating system and file system (POSIX, Windows, NTFS, ext4, APFS, etc.).
				// Path.GetFullPath() catches this before the per-segment check runs.
				{
					"NUL character (\\0)",
					"test\0path",
					"The file path has an invalid format for the current operating system."
				},

				// Most file systems impose a 255-byte (POSIX NAME_MAX) or 255 UTF-16 code unit (NTFS)
				// limit per path segment. A 256-character file name exceeds this on ext4, btrfs, XFS,
				// NTFS, and APFS alike. This is the primary structural constraint on Linux, where
				// almost all characters are valid but segment length is not.
				{
					"segment exceeds 255 chars",
					new string('a', 256),
					"The file path contains a segment that exceeds the maximum length of 255 characters."
				}
			};

			if (OperatingSystem.IsWindows())
			{
				// --- Windows-only: NTFS/ReFS reject these; valid on Linux/macOS ---
				//
				// On Linux (ext4, btrfs, XFS), all of the following characters are ordinary
				// characters that can appear in file and directory names without restriction.
				// Only Windows file systems (NTFS, ReFS, FAT32) reserve them.

				const string invalidCharsMessage =
					"The file path contains characters that are invalid on the current operating system.";

				// NTFS reserves < > for I/O redirection and | for piping at the shell level.
				// The file system itself also rejects them in file and directory names.
				data.Add("angle bracket (<)", "test<path", invalidCharsMessage);
				data.Add("angle bracket (>)", "test>path", invalidCharsMessage);
				data.Add("pipe character (|)", "test|path", invalidCharsMessage);

				// Double quotes and wildcard characters (* ?) are reserved by the Windows shell
				// and rejected by NTFS at the file system level.
				data.Add("double quote (\")", "test\"path", invalidCharsMessage);
				data.Add("asterisk (*)", "test*path", invalidCharsMessage);
				data.Add("question mark (?)", "test?path", invalidCharsMessage);

				// A colon outside the drive root (C:\) designates an NTFS Alternate Data Stream
				// (ADS). While technically valid NTFS syntax, file paths must never reference ADS —
				// the colon is treated as invalid in non-root segments.
				data.Add("colon in segment (:)", @"C:\test:stream", invalidCharsMessage);

				// Control characters 0x01–0x1F are forbidden on NTFS but permitted (though rarely
				// used in practice) on most POSIX file systems.
				// ReSharper disable once VariableLengthStringHexEscapeSequence
				data.Add("control character (0x01)", "test\x01path", invalidCharsMessage);
			}

			return data;
		}
	}

	/// <summary>
	/// Verifies that <see cref="FilePathValidator.Validate"/> throws <see cref="ArgumentException"/> when
	/// <c>filePath</c> contains characters that are invalid for the current operating system or has an invalid
	/// structural format.
	/// </summary>
	/// <param name="scenario">A human-readable description of the test case.</param>
	/// <param name="filePath">The invalid file path to test.</param>
	/// <param name="expectedMessage">
	/// Expected message prefix produced by <see cref="FilePathValidator"/>.
	/// </param>
	[Theory]
	[MemberData(nameof(InvalidPathFormats))]
	public void Validate_WhenFilePathHasInvalidFormat_ThrowsArgumentException(
		string scenario,
		string filePath,
		string expectedMessage)
	{
		_ = scenario;

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => FilePathValidator.Validate(filePath));
		Assert.Equal("filePath", ex.ParamName);
		Assert.StartsWith(expectedMessage, ex.Message);
	}

	// --- 4. CallerArgumentExpression: parameter name inference and explicit override ---

	/// <summary>
	/// Verifies that <see cref="FilePathValidator.Validate"/> infers the parameter name via
	/// <c>CallerArgumentExpressionAttribute</c> when no explicit parameter name is provided.
	/// </summary>
	[Fact]
	public void Validate_WhenCalledWithoutExplicitParamName_InfersParameterName()
	{
		// Arrange
		string myCustomParam = "";

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => FilePathValidator.Validate(myCustomParam));
		Assert.Equal(nameof(myCustomParam), ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="FilePathValidator.Validate"/> uses the explicit parameter name when provided.
	/// </summary>
	[Fact]
	public void Validate_WhenCalledWithExplicitParamName_UsesExplicitName()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => FilePathValidator.Validate("", "customName"));
		Assert.Equal("customName", ex.ParamName);
	}
}
