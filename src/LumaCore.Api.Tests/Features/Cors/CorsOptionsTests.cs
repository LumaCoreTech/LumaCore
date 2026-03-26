// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Cors;

using Xunit;

namespace LumaCore.Api.Tests.Features.Cors;

/// <summary>
/// Unit tests for <see cref="CorsOptions"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>Anchor (this file) — Constructor defaults, <see cref="CorsOptions.SectionName"/>.</item>
///         <item>Validation — Data-annotation and <see cref="CorsOptions.Validate"/> scenarios.</item>
///     </list>
/// </remarks>
[Trait("Category", "Cors")]
public sealed partial class CorsOptionsTests
{
	#region Constructor

	/// <summary>
	/// Verifies that a default-constructed <see cref="CorsOptions"/> instance has the expected property defaults.
	/// </summary>
	[Fact]
	public void Constructor_Initially_HasExpectedDefaults()
	{
		// Arrange + Act
		var sut = new CorsOptions();

		// Assert
		Assert.False(sut.Enabled);
		Assert.False(sut.AllowCredentials);
		Assert.Empty(sut.AllowedOrigins);
		Assert.Empty(sut.AllowedMethods);
		Assert.Empty(sut.AllowedHeaders);
		Assert.Empty(sut.ExposedHeaders);
		Assert.Null(sut.PreflightMaxAge);
	}

	#endregion

	#region SectionName

	/// <summary>
	/// Verifies that <see cref="CorsOptions.SectionName"/> is <c>"Cors"</c>.
	/// </summary>
	[Fact]
	public void SectionName_Always_ReturnsExpectedValue()
	{
		// Act + Assert
		Assert.Equal("Cors", CorsOptions.SectionName);
	}

	#endregion
}
