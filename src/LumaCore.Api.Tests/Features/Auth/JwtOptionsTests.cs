// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

/// <summary>
/// Unit tests for <see cref="JwtOptions"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>Anchor (this file) — Constructor defaults, <see cref="JwtOptions.SectionName"/>.</item>
///         <item>Validation — Data-annotation validation (success, boundary, failure scenarios).</item>
///     </list>
/// </remarks>
[Trait("Category", "Auth")]
public sealed partial class JwtOptionsTests
{
	#region Constructor

	/// <summary>
	/// Verifies that a default-constructed <see cref="JwtOptions"/> instance has the expected property defaults.
	/// </summary>
	[Fact]
	public void Constructor_Initially_HasExpectedDefaults()
	{
		// Arrange + Act
		var sut = new JwtOptions();

		// Assert
		Assert.Equal(60, sut.AccessTokenLifetimeMinutes);
		Assert.Equal(string.Empty, sut.Audience);
		Assert.Equal(string.Empty, sut.Issuer);
		Assert.Equal(string.Empty, sut.SigningKey);
	}

	#endregion

	#region SectionName

	/// <summary>
	/// Verifies that <see cref="JwtOptions.SectionName"/> is <c>"Jwt"</c>.
	/// </summary>
	[Fact]
	public void SectionName_Always_ReturnsExpectedValue()
	{
		// Act + Assert
		Assert.Equal("Jwt", JwtOptions.SectionName);
	}

	#endregion
}
