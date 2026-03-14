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
	/// Verifies that <see cref="UserQueries.ExistsByEmail"/> returns <see langword="true"/> for a seeded user
	/// and <see langword="false"/> for a non-existent email.
	/// </summary>
	[Fact]
	public async Task UserQueries_ExistsByEmail_ReturnsExpectedResult()
	{
		// Act
		bool exists = await UserQueries.ExistsByEmail(mFixture.DbContext, "alice@example.test");
		bool notExists = await UserQueries.ExistsByEmail(mFixture.DbContext, "nobody@example.test");

		// Assert
		Assert.True(exists);
		Assert.False(notExists);
	}

	/// <summary>
	/// Verifies that <see cref="UserQueries.ExistsByUsernameNormalized"/> returns <see langword="true"/> for
	/// a seeded normalized username and <see langword="false"/> for a non-existent one.
	/// </summary>
	[Fact]
	public async Task UserQueries_ExistsByUsernameNormalized_ReturnsExpectedResult()
	{
		// Act
		bool exists = await UserQueries.ExistsByUsernameNormalized(mFixture.DbContext, "ALICE");
		bool notExists = await UserQueries.ExistsByUsernameNormalized(mFixture.DbContext, "NOBODY");

		// Assert
		Assert.True(exists);
		Assert.False(notExists);
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
