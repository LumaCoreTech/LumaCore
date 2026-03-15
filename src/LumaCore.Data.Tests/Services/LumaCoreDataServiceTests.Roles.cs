// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LumaCoreDataServiceTests
{
	/// <summary>
	/// Tests for <see cref="IRoleDataService"/> methods.
	/// </summary>
	/// <remarks>
	/// These tests validate role assignment and removal. They deliberately cover the idempotent behavior
	/// (assigning the same role twice returns <c>false</c>) and the database-constraint-backed duplicate handling.
	/// </remarks>
	[Trait("Category", "Data")]
	public sealed class Roles : TestBase
	{
		#region AssignRoleToUserAsync

		/// <summary>
		/// Verifies that <see cref="IRoleDataService.AssignRoleToUserAsync"/> returns <c>true</c> and creates a join row
		/// when a role is assigned to a user for the first time.
		/// </summary>
		[Fact]
		public async Task AssignRoleToUserAsync_WhenFirstTime_ReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			(ParticipantEntity _, UserEntity user) =
				await CreateUserParticipantWithUserAsync("alice", "alice@example.test", utcNow);

			var role = new RoleEntity
			{
				PublicId = Guid.NewGuid(),
				Name = "admin",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Roles.Add(role);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			bool assigned = await service.AssignRoleToUserAsync(
				                userId: user.Id,
				                roleId: role.Id,
				                utcNow: utcNow);

			// Assert
			Assert.True(assigned);

			UserRoleEntity? reloaded = await Fixture.DbContext.UserRoles
				                           .AsNoTracking()
				                           .FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);

			Assert.NotNull(reloaded);
			Assert.Equal(utcNow, reloaded.AssignedAtUtc);
		}

		/// <summary>
		/// Verifies the duplicate-handling branch of <see cref="IRoleDataService.AssignRoleToUserAsync"/> by performing a
		/// second assignment using a fresh <see cref="DbContext"/>.
		/// </summary>
		/// <remarks>
		/// Using a new context avoids EF tracking identity conflicts and ensures the operation hits the database UNIQUE
		/// constraint (which the production code catches and translates into a <c>false</c> result).
		/// </remarks>
		[Fact]
		public async Task AssignRoleToUserAsync_WhenAlreadyAssigned_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var role = new RoleEntity
			{
				PublicId = Guid.NewGuid(),
				Name = "custom",
				Description = null,
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Roles.Add(role);

			(ParticipantEntity _, UserEntity user) =
				await CreateUserParticipantWithUserAsync("alice", "alice@example.test", utcNow);

			// Act
			bool first = await service.AssignRoleToUserAsync(user.Id, role.Id, utcNow);

			// Assert
			Assert.True(first);

			// Special: Using a new DbContext forces the duplicate insert to hit the database constraint.
			LumaCoreDataService service2 = LumaCoreDataServiceFactory.Create(Fixture.CreateDbContext());
			bool second = await service2.AssignRoleToUserAsync(user.Id, role.Id, utcNow);
			Assert.False(second);

			int count = await Fixture.DbContext.UserRoles.CountAsync();
			Assert.Equal(1, count);
		}

		/// <summary>
		/// Verifies that <see cref="IRoleDataService.AssignRoleToUserAsync"/> rethrows <see cref="DbUpdateException"/> when
		/// the failure is not caused by a duplicate (UserId, RoleId) assignment.
		/// </summary>
		/// <remarks>
		/// We provoke a FK violation by referencing non-existing user/role ids. The code catches
		/// <see cref="DbUpdateException"/> and only returns <c>false</c> if the join already exists; otherwise it must
		/// rethrow.
		/// </remarks>
		[Fact]
		public async Task AssignRoleToUserAsync_WhenForeignKeyViolation_RethrowsDbUpdateException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act + Assert
			await Assert.ThrowsAsync<DbUpdateException>(() =>
				service.AssignRoleToUserAsync(userId: new UserId(999), roleId: new RoleId(999), utcNow: utcNow));
		}

		/// <summary>
		/// Test data for <see cref="AssignRoleToUserAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// Each row provides an invalid id combination that triggers an <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, UserId, RoleId, string>
			AssignRoleToUserAsync_InvalidId_Data => new()
		{
			// User id is zero
			{ "Zero userId", new UserId(0), new RoleId(1), "userId.Value" },

			// Role id is zero
			{ "Zero roleId", new UserId(1), new RoleId(0), "roleId.Value" }
		};

		/// <summary>
		/// Verifies that <see cref="IRoleDataService.AssignRoleToUserAsync"/> validates id parameters and throws
		/// <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="userId">The user id to pass to the method.</param>
		/// <param name="roleId">The role id to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentOutOfRangeException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(AssignRoleToUserAsync_InvalidId_Data))]
		public async Task AssignRoleToUserAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException(
			string scenario,
			UserId userId,
			RoleId roleId,
			string expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.AssignRoleToUserAsync(userId: userId, roleId: roleId, utcNow: utcNow));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion

		#region RemoveRoleFromUserAsync

		/// <summary>
		/// Verifies that <see cref="IRoleDataService.RemoveRoleFromUserAsync"/> removes an existing assignment and returns
		/// <c>true</c>.
		/// </summary>
		/// <remarks>
		/// The test clears the change tracker before removal to ensure the implementation reads the current join row
		/// state from the database.
		/// </remarks>
		[Fact]
		public async Task RemoveRoleFromUserAsync_WhenAssigned_RemovesAndReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			(ParticipantEntity _, UserEntity user) =
				await CreateUserParticipantWithUserAsync("alice", "alice@example.test", utcNow);

			var role = new RoleEntity
			{
				PublicId = Guid.NewGuid(),
				Name = "admin",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Roles.Add(role);
			await Fixture.DbContext.SaveChangesAsync();

			await service.AssignRoleToUserAsync(user.Id, role.Id, utcNow);

			// Special: ensure removal reads from the database.
			Fixture.DbContext.ChangeTracker.Clear();

			// Act
			bool removed = await service.RemoveRoleFromUserAsync(userId: user.Id, roleId: role.Id);

			// Assert
			Assert.True(removed);

			bool stillExists = await Fixture.DbContext.UserRoles
				                   .AsNoTracking()
				                   .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);

			Assert.False(stillExists);
		}

		/// <summary>
		/// Verifies that <see cref="IRoleDataService.RemoveRoleFromUserAsync"/> returns <c>false</c> when the assignment
		/// does not exist.
		/// </summary>
		[Fact]
		public async Task RemoveRoleFromUserAsync_WhenNotAssigned_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool removed = await service.RemoveRoleFromUserAsync(userId: new UserId(1), roleId: new RoleId(1));

			// Assert
			Assert.False(removed);
		}

		/// <summary>
		/// Test data for <see cref="RemoveRoleFromUserAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// Each row provides an invalid id combination that triggers an <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, UserId, RoleId, string>
			RemoveRoleFromUserAsync_InvalidId_Data => new()
		{
			// User id is zero
			{ "Zero userId", new UserId(0), new RoleId(1), "userId.Value" },

			// Role id is zero
			{ "Zero roleId", new UserId(1), new RoleId(0), "roleId.Value" }
		};

		/// <summary>
		/// Verifies that <see cref="IRoleDataService.RemoveRoleFromUserAsync"/> validates id parameters and throws
		/// <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="userId">The user id to pass to the method.</param>
		/// <param name="roleId">The role id to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentOutOfRangeException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(RemoveRoleFromUserAsync_InvalidId_Data))]
		public async Task RemoveRoleFromUserAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException(
			string scenario,
			UserId userId,
			RoleId roleId,
			string expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.RemoveRoleFromUserAsync(userId: userId, roleId: roleId));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion
	}
}
