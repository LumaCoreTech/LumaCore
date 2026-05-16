// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Core.Cryptography;

using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Core.Tests.Cryptography;

public partial class Pbkdf2PasswordHasherTests
{
	#region Pbkdf2PasswordHasher(PasswordHashingOptions) constructor

	/// <summary>
	/// Verifies that the hasher exposes the configured iteration count after construction.
	/// </summary>
	[Fact]
	public void Constructor_WithOptions_StoresIterations()
	{
		// Arrange
		var options = new PasswordHashingOptions { Iterations = TestIterations };

		// Act
		var sut = new Pbkdf2PasswordHasher(options);

		// Assert
		Assert.Equal(TestIterations, sut.Iterations);
	}

	/// <summary>
	/// Verifies that the hasher rejects a <see langword="null"/> options instance.
	/// </summary>
	[Fact]
	public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new Pbkdf2PasswordHasher((PasswordHashingOptions)null!));
		Assert.Equal("options", ex.ParamName);
	}

	/// <summary>
	/// Verifies that invalid options (iteration count below the configured minimum) cause construction to
	/// fail rather than producing a weakened hasher.
	/// </summary>
	[Fact]
	public void Constructor_WhenIterationsBelowMinimum_ThrowsValidationException()
	{
		// Arrange
		var options = new PasswordHashingOptions { Iterations = 1 };

		// Act + Assert
		Assert.Throws<ValidationException>(() => new Pbkdf2PasswordHasher(options));
	}

	#endregion

	#region Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions>) constructor

	/// <summary>
	/// Verifies that the IOptions overload unwraps and applies the configured iteration count.
	/// </summary>
	[Fact]
	public void Constructor_WithIOptions_StoresIterations()
	{
		// Arrange
		var options = new PasswordHashingOptions { Iterations = TestIterations };

		// Act
		var sut = new Pbkdf2PasswordHasher(Wrap(options));

		// Assert
		Assert.Equal(TestIterations, sut.Iterations);
	}

	/// <summary>
	/// Verifies that a <see langword="null"/> IOptions wrapper is rejected.
	/// </summary>
	[Fact]
	public void Constructor_WhenIOptionsIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
			new Pbkdf2PasswordHasher((IOptions<PasswordHashingOptions>)null!));
		Assert.Equal("options", ex.ParamName);
	}

	/// <summary>
	/// Verifies that an IOptions wrapper whose <c>Value</c> is <see langword="null"/> is rejected — the
	/// hasher cannot operate without a valid options instance.
	/// </summary>
	[Fact]
	public void Constructor_WhenIOptionsValueIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new Pbkdf2PasswordHasher(Wrap(null)));
		Assert.Equal("options", ex.ParamName);
	}

	#endregion
}
