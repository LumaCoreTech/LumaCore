// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Core.Tests;

/// <summary>
/// Tests for <see cref="Guard"/>.
/// </summary>
public sealed class GuardTests
{
	#region ThrowIfNullOrEmptyOrTooLong

	/// <summary>
	/// Verifies that <see cref="Guard.ThrowIfNullOrEmptyOrTooLong"/> throws
	/// <see cref="ArgumentOutOfRangeException"/> when <c>maxLength</c> is zero or negative.
	/// </summary>
	/// <param name="maxLength">The invalid maximum length.</param>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void ThrowIfNullOrEmptyOrTooLong_WhenMaxLengthInvalid_ThrowsArgumentOutOfRangeException(int maxLength)
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
			Guard.ThrowIfNullOrEmptyOrTooLong("valid", maxLength, out string _));
		Assert.Equal(nameof(maxLength), ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="Guard.ThrowIfNullOrEmptyOrTooLong"/> throws <see cref="ArgumentNullException"/> for
	/// <see langword="null"/> input.
	/// </summary>
	[Fact]
	public void ThrowIfNullOrEmptyOrTooLong_WhenNull_ThrowsArgumentNullException()
	{
		// Arrange
		string? value = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
			Guard.ThrowIfNullOrEmptyOrTooLong(value, 100, out string _));
		Assert.Equal(nameof(value), ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="Guard.ThrowIfNullOrEmptyOrTooLong"/> throws <see cref="ArgumentException"/> for
	/// empty, whitespace-only, or over-length input.
	/// </summary>
	/// <param name="input">The invalid input string.</param>
	/// <param name="maxLength">The maximum allowed length after trimming.</param>
	[Theory]
	[InlineData("", 100)]
	[InlineData("   ", 100)]
	[InlineData("xxxxxxxxxxx", 10)]
	public void ThrowIfNullOrEmptyOrTooLong_WhenInvalid_ThrowsArgumentException(string input, int maxLength)
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() =>
			Guard.ThrowIfNullOrEmptyOrTooLong(input, maxLength, out string _));
		Assert.Equal(nameof(input), ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="Guard.ThrowIfNullOrEmptyOrTooLong"/> outputs the trimmed value for valid input,
	/// including values at exactly the maximum length and values with surrounding whitespace.
	/// </summary>
	/// <param name="input">The input string to validate.</param>
	/// <param name="maxLength">The maximum allowed length after trimming.</param>
	/// <param name="expected">The expected trimmed output.</param>
	[Theory]
	[InlineData("  hello  ", 100, "hello")]
	[InlineData("  xxxxxxxxxx  ", 10, "xxxxxxxxxx")]
	[InlineData("     xxxxxxxxxx     ", 10, "xxxxxxxxxx")]
	public void ThrowIfNullOrEmptyOrTooLong_WhenValid_OutputsTrimmed(string input, int maxLength, string expected)
	{
		// Act
		Guard.ThrowIfNullOrEmptyOrTooLong(input, maxLength, out string trimmed);

		// Assert
		Assert.Equal(expected, trimmed);
	}

	#endregion

	#region ThrowIfTooLong

	/// <summary>
	/// Verifies that <see cref="Guard.ThrowIfTooLong"/> throws <see cref="ArgumentOutOfRangeException"/> when
	/// <c>maxLength</c> is zero or negative.
	/// </summary>
	/// <param name="maxLength">The invalid maximum length.</param>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void ThrowIfTooLong_WhenMaxLengthInvalid_ThrowsArgumentOutOfRangeException(int maxLength)
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfTooLong(
			"valid",
			maxLength,
			out string? _));
		Assert.Equal(nameof(maxLength), ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="Guard.ThrowIfTooLong"/> outputs <see langword="null"/> for
	/// <see langword="null"/>, empty, or whitespace-only input.
	/// </summary>
	/// <param name="input">The input string to normalize.</param>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowIfTooLong_WhenNullOrBlank_OutputsNull(string? input)
	{
		// Act
		Guard.ThrowIfTooLong(input, 100, out string? trimmed);

		// Assert
		Assert.Null(trimmed);
	}

	/// <summary>
	/// Verifies that <see cref="Guard.ThrowIfTooLong"/> throws <see cref="ArgumentException"/> when the trimmed
	/// value exceeds the maximum length.
	/// </summary>
	[Fact]
	public void ThrowIfTooLong_WhenTrimmedExceedsMaxLength_ThrowsArgumentException()
	{
		// Arrange
		string input = "xxxxxxxxxxx";

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => Guard.ThrowIfTooLong(input, 10, out string? _));
		Assert.Equal(nameof(input), ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="Guard.ThrowIfTooLong"/> outputs the trimmed value for valid input,
	/// including values at exactly the maximum length.
	/// </summary>
	/// <param name="input">The input string to validate.</param>
	/// <param name="maxLength">The maximum allowed length after trimming.</param>
	/// <param name="expected">The expected trimmed output.</param>
	[Theory]
	[InlineData("  hello  ", 100, "hello")]
	[InlineData("  xxxxxxxxxx  ", 10, "xxxxxxxxxx")]
	public void ThrowIfTooLong_WhenValid_OutputsTrimmed(string input, int maxLength, string expected)
	{
		// Act
		Guard.ThrowIfTooLong(input, maxLength, out string? trimmed);

		// Assert
		Assert.Equal(expected, trimmed);
	}

	#endregion
}
