// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;

using Xunit;

namespace LumaCore.Core.Tests.IO;

/// <summary>
/// Unit tests for <see cref="TemporaryFolderManagerOptions"/>.
/// </summary>
[Trait("Category", "IO")]
public sealed class TemporaryFolderManagerOptionsTests
{
	#region DefaultSectionName

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManagerOptions.DefaultSectionName"/> returns the expected
	/// configuration section name used for <c>appsettings.json</c> binding.
	/// </summary>
	[Fact]
	public void DefaultSectionName_ReturnsExpectedValue()
	{
		// Arrange + Act + Assert
		Assert.Equal("TemporaryFolders", TemporaryFolderManagerOptions.DefaultSectionName);
	}

	#endregion

	#region BasePath

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManagerOptions.BasePath"/> defaults to a <c>LumaCore</c> subdirectory
	/// under the system temporary directory.
	/// </summary>
	[Fact]
	public void BasePath_Default_CombinesTempPathWithLumaCore()
	{
		// Arrange + Act
		var options = new TemporaryFolderManagerOptions();

		// Assert
		Assert.Equal(Path.Combine(Path.GetTempPath(), "LumaCore"), options.BasePath);
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManagerOptions.BasePath"/> can be set to a custom path and returns
	/// the assigned value.
	/// </summary>
	[Fact]
	public void BasePath_SetCustomValue_ReturnsCustomValue()
	{
		// Arrange
		var options = new TemporaryFolderManagerOptions();
		string customPath = Path.Combine(Path.GetTempPath(), "custom-base");

		// Act
		options.BasePath = customPath;

		// Assert
		Assert.Equal(customPath, options.BasePath);
	}

	#endregion
}
