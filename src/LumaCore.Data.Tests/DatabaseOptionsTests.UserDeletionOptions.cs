// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Data.Tests;

public sealed partial class DatabaseOptionsTests
{
	/// <summary>
	/// Verifies that a default-constructed <see cref="DatabaseOptions.UserDeletionOptions"/> instance has the expected
	/// property defaults.
	/// </summary>
	[Fact]
	public void UserDeletionOptions_Constructor_Initially_HasExpectedDefaults()
	{
		// Arrange + Act
		var sut = new DatabaseOptions.UserDeletionOptions();

		// Assert
		AssertUserDeletionDefaults(sut);
	}
}
