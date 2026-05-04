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
	/// Verifies that <see cref="RoleQueries.GetAll"/> returns the seeded role.
	/// </summary>
	[Fact]
	public async Task RoleQueries_GetAll_ReturnsSeededRoles()
	{
		// Act
		List<RoleEntity> roles = await ToListAsync(RoleQueries.GetAll(mFixture.DbContext));

		// Assert
		Assert.Single(roles);
		Assert.Equal(mRoleId, roles[0].Id);
		Assert.Equal("admin", roles[0].Name);
	}

	/// <summary>
	/// Verifies that <see cref="RoleQueries.GetByName"/> returns the role matching the given name.
	/// </summary>
	[Fact]
	public async Task RoleQueries_GetByName_ReturnsRole()
	{
		// Act
		RoleEntity? result = await RoleQueries.GetByName(mFixture.DbContext, "admin");

		// Assert
		Assert.NotNull(result);
		Assert.Equal(mRoleId, result.Id);
		Assert.Equal("admin", result.Name);
	}

	/// <summary>
	/// Verifies that <see cref="RoleQueries.GetRoleNamesByUserId"/> returns the role names assigned
	/// to the seeded user.
	/// </summary>
	[Fact]
	public async Task RoleQueries_GetRoleNamesByUserId_ReturnsAssignedRoles()
	{
		// Act
		List<string> roleNames = await ToListAsync(RoleQueries.GetRoleNamesByUserId(mFixture.DbContext, mAliceUserId));

		// Assert
		Assert.Single(roleNames);
		Assert.Equal("admin", roleNames[0]);
	}

	/// <summary>
	/// Verifies that <see cref="RoleQueries.UserHasRole"/> returns <see langword="true"/> only for roles
	/// actually assigned to the user.
	/// </summary>
	/// <param name="roleName">The role name to query.</param>
	/// <param name="expected">The expected outcome.</param>
	[Theory]
	[InlineData("admin", true)]      // Assigned in seed.
	[InlineData("moderator", false)] // Not assigned in seed.
	public async Task RoleQueries_UserHasRole_ReturnsTrueOnlyForAssignedRole(string roleName, bool expected)
	{
		// Act
		bool actual = await RoleQueries.UserHasRole(mFixture.DbContext, mAliceUserId, roleName);

		// Assert
		Assert.Equal(expected, actual);
	}
}
