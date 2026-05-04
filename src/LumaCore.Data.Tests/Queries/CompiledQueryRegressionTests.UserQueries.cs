// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Queries;

using Xunit;

namespace LumaCore.Data.Tests.Queries;

public sealed partial class CompiledQueryRegressionTests
{
	/// <summary>
	/// Verifies that <see cref="UserQueries.ExistsByEmail"/> returns <see langword="true"/> only for an email
	/// belonging to a seeded user.
	/// </summary>
	/// <param name="email">The email address to probe.</param>
	/// <param name="expected">The expected outcome.</param>
	[Theory]
	[InlineData("alice@example.test", true)]   // Seeded.
	[InlineData("nobody@example.test", false)] // Not seeded.
	public async Task UserQueries_ExistsByEmail_ReturnsTrueOnlyForExistingEmail(string email, bool expected)
	{
		// Act
		bool actual = await UserQueries.ExistsByEmail(mFixture.DbContext, email);

		// Assert
		Assert.Equal(expected, actual);
	}

	/// <summary>
	/// Verifies that <see cref="UserQueries.ExistsByUsernameNormalized"/> returns <see langword="true"/> only
	/// for a normalized username belonging to a seeded user.
	/// </summary>
	/// <param name="usernameNormalized">The normalized username to probe.</param>
	/// <param name="expected">The expected outcome.</param>
	[Theory]
	[InlineData("ALICE", true)]   // Seeded.
	[InlineData("NOBODY", false)] // Not seeded.
	public async Task UserQueries_ExistsByUsernameNormalized_ReturnsTrueOnlyForExistingUser(
		string usernameNormalized,
		bool   expected)
	{
		// Act
		bool actual = await UserQueries.ExistsByUsernameNormalized(mFixture.DbContext, usernameNormalized);

		// Assert
		Assert.Equal(expected, actual);
	}

	/// <summary>
	/// Verifies that <see cref="UserQueries.GetByEmail"/> returns the user with the
	/// <see cref="UserEntity.Participant"/> navigation loaded.
	/// </summary>
	[Fact]
	public async Task UserQueries_GetByEmail_ReturnsUserWithParticipant()
	{
		// Act
		UserEntity? result = await UserQueries.GetByEmail(mFixture.DbContext, "alice@example.test");

		// Assert
		Assert.NotNull(result);
		Assert.Equal("alice", result.Username);
		Assert.NotNull(result.Participant);
		Assert.Equal("Alice", result.Participant.DisplayName);
	}

	/// <summary>
	/// Verifies that <see cref="UserQueries.GetByParticipantId"/> returns the user linked to the seeded
	/// participant.
	/// </summary>
	[Fact]
	public async Task UserQueries_GetByParticipantId_ReturnsUser()
	{
		// Act
		UserEntity? result = await UserQueries.GetByParticipantId(mFixture.DbContext, mAliceParticipantId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("alice", result.Username);
		Assert.Equal("alice@example.test", result.Email);
	}

	/// <summary>
	/// Verifies that <see cref="UserQueries.GetByUsernameNormalized"/> returns the user with the
	/// <see cref="UserEntity.Participant"/> navigation loaded.
	/// </summary>
	[Fact]
	public async Task UserQueries_GetByUsernameNormalized_ReturnsUserWithParticipant()
	{
		// Act
		UserEntity? result = await UserQueries.GetByUsernameNormalized(mFixture.DbContext, "ALICE");

		// Assert
		Assert.NotNull(result);
		Assert.Equal("alice@example.test", result.Email);
		Assert.NotNull(result.Participant);
		Assert.Equal("Alice", result.Participant.DisplayName);
	}
}
