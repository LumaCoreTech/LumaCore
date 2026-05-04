// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LumaCoreDataServiceTests
{
	/// <summary>
	/// Tests for <see cref="IUserDataService"/> methods.
	/// </summary>
	/// <remarks>
	/// These tests focus on validation/normalization (trimming, limits, null handling), query helpers (lookup and
	/// existence checks), and privacy-sensitive deletion behavior (scrubbing participant profile and redaction).
	/// </remarks>
	[Trait("Category", "Services")]
	public sealed class Users : TestBase
	{
		#region CreateParticipantAsync

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateParticipantAsync"/> creates a participant with all fields
		/// correctly persisted, including a generated <see cref="ParticipantEntity.PublicId"/>.
		/// </summary>
		[Fact]
		public async Task CreateParticipantAsync_WhenValid_CreatesParticipantWithAllFields()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act
			ParticipantEntity participant = await service.CreateParticipantAsync(
				                                displayName: "  Alice  ",
				                                utcNow: utcNow);

			ParticipantEntity? reloaded = await Fixture.DbContext.Participants
				                              .AsNoTracking()
				                              .FirstOrDefaultAsync(p => p.Id == participant.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.True(reloaded.Id.Value > 0);
			Assert.NotEqual(Guid.Empty, reloaded.PublicId);
			Assert.Equal("Alice", reloaded.DisplayName);
			Assert.Equal(utcNow, reloaded.CreatedAtUtc);
		}

		/// <summary>
		/// Test data for <see cref="CreateParticipantAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid display name that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string, string> CreateParticipantAsync_InvalidInput_Data => new()
		{
			// Whitespace-only display name
			{ "Whitespace display name", "   ", "displayName" },

			// Display name exceeds the 100-character maximum
			{ "Display name too long", new string('x', 101), "displayName" }
		};

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateParticipantAsync"/> rejects invalid inputs with an
		/// <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="displayName">The display name to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(CreateParticipantAsync_InvalidInput_Data))]
		public async Task CreateParticipantAsync_WhenInputInvalid_ThrowsArgumentException(
			string scenario,
			string displayName,
			string expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.CreateParticipantAsync(
					         displayName: displayName,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion

		#region CreateUserAsync

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserAsync"/> normalizes whitespace-only email input to
		/// <c>null</c>.
		/// </summary>
		/// <remarks>
		/// This test creates a participant row first and then asserts that the reloaded user has
		/// <see cref="UserEntity.Email"/> set to <c>null</c>.
		/// </remarks>
		[Fact]
		public async Task CreateUserAsync_WhenEmailWhitespace_BecomesNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			ParticipantEntity participant = new()
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Alice",
				CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
			};
			Fixture.DbContext.Participants.Add(participant);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			UserEntity user = await service.CreateUserAsync(
				                  participantId: participant.Id,
				                  username: "alice",
				                  email: "   ",
				                  passwordHash: "hash",
				                  utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Special: CreateUserAsync() treats whitespace-only email as "no email" and normalizes it to null.
			UserEntity? reloaded = await Fixture.DbContext.Users
				                       .AsNoTracking()
				                       .FirstOrDefaultAsync(u => u.Id == user.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Null(reloaded.Email);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserAsync"/> trims username/email/passwordHash inputs and
		/// returns the created <see cref="UserEntity"/>.
		/// </summary>
		[Fact]
		public async Task CreateUserAsync_WhenValid_TrimsFieldsAndCreates()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			ParticipantEntity participant = new()
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Alice",
				CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
			};
			Fixture.DbContext.Participants.Add(participant);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			UserEntity user = await service.CreateUserAsync(
				                  participantId: participant.Id,
				                  username: "  alice  ",
				                  email: "  alice@example.test  ",
				                  passwordHash: "  hash  ",
				                  utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Special: CreateUserAsync() trims inputs and computes UsernameNormalized.
			UserEntity? reloaded = await Fixture.DbContext.Users
				                       .AsNoTracking()
				                       .FirstOrDefaultAsync(u => u.Id == user.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.True(reloaded.Id.Value > 0);
			Assert.Equal(participant.Id, reloaded.ParticipantId);
			Assert.Equal("alice", reloaded.Username);
			Assert.Equal("ALICE", reloaded.UsernameNormalized);
			Assert.Equal("alice@example.test", reloaded.Email);
			Assert.Equal("hash", reloaded.PasswordHash);
		}

		/// <summary>
		/// Test data for <see cref="CreateUserAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each row provides an
		/// invalid combination of username, email, and password hash that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string, string?, string, string>
			CreateUserAsync_InvalidInput_Data => new()
		{
			// Whitespace-only username
			{ "Whitespace username", "   ", null, "hash", "username" },

			// Username exceeds the 50-character maximum
			{ "Username too long", new string('x', 51), null, "hash", "username" },

			// Whitespace-only password hash
			{ "Whitespace password hash", "alice", null, "   ", "passwordHash" },

			// Password hash exceeds the 255-character maximum
			{ "Password hash too long", "alice", null, new string('x', 256), "passwordHash" },

			// Email exceeds the 254-character maximum
			{ "Email too long", "alice", new string('x', 255), "hash", "email" }
		};

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserAsync"/> rejects invalid inputs with an
		/// <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="username">The username to pass to the method.</param>
		/// <param name="email">The email to pass to the method.</param>
		/// <param name="passwordHash">The password hash to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(CreateUserAsync_InvalidInput_Data))]
		public async Task CreateUserAsync_WhenInputInvalid_ThrowsArgumentException(
			string  scenario,
			string  username,
			string? email,
			string  passwordHash,
			string  expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.CreateUserAsync(
					         participantId: new ParticipantId(1),
					         username: username,
					         email: email,
					         passwordHash: passwordHash,
					         utcNow: DateTime.UtcNow));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserAsync"/> validates <c>participantId</c> and throws
		/// <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task CreateUserAsync_WhenParticipantIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.CreateUserAsync(
					         participantId: new ParticipantId(0),
					         username: "alice",
					         email: null,
					         passwordHash: "hash",
					         utcNow: DateTime.UtcNow));
			Assert.Equal("participantId.Value", ex.ParamName);
		}

		#endregion

		#region CreateUserWithParticipantAsync

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserWithParticipantAsync"/> assigns the default "user" role
		/// when it exists and default-role assignment is enabled. The toggle parameter asserts the contract — both the
		/// regular EF branch and the compiled hot-path branch (delegating to <c>RoleQueries.GetByName</c>) must yield
		/// the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task CreateUserWithParticipantAsync_WhenDefaultRolePresent_AssignsRole(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var role = new RoleEntity { Name = "user" };
			Fixture.DbContext.Roles.Add(role);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			UserEntity user = await service.CreateUserWithParticipantAsync(
				                  displayName: "Alice",
				                  username: "alice",
				                  email: "alice@example.test",
				                  passwordHash: "hash",
				                  assignDefaultUserRole: true,
				                  utcNow: utcNow);

			// Assert
			Assert.True(user.Id.Value > 0);
			Assert.True(user.ParticipantId.Value > 0);

			bool roleAssigned = await Fixture.DbContext.UserRoles
				                    .AsNoTracking()
				                    .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);

			Assert.True(roleAssigned);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserWithParticipantAsync"/> normalizes whitespace-only
		/// <c>email</c> to <c>null</c>.
		/// </summary>
		[Fact]
		public async Task CreateUserWithParticipantAsync_WhenEmailWhitespace_BecomesNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act
			UserEntity user = await service.CreateUserWithParticipantAsync(
				                  displayName: "Alice",
				                  username: "alice",
				                  email: "   ",
				                  passwordHash: "hash",
				                  assignDefaultUserRole: false,
				                  utcNow: utcNow);

			UserEntity? reloaded = await Fixture.DbContext.Users
				                       .AsNoTracking()
				                       .FirstOrDefaultAsync(u => u.Id == user.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Null(reloaded.Email);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserWithParticipantAsync"/> trims display name,
		/// username, email, and password hash before persisting.
		/// </summary>
		[Fact]
		public async Task CreateUserWithParticipantAsync_WhenValid_TrimsFields()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act
			UserEntity user = await service.CreateUserWithParticipantAsync(
				                  displayName: "  Alice  ",
				                  username: "  alice  ",
				                  email: "  alice@example.test  ",
				                  passwordHash: "  hash  ",
				                  assignDefaultUserRole: false,
				                  utcNow: utcNow);

			UserEntity? reloadedUser = await Fixture.DbContext.Users
				                           .AsNoTracking()
				                           .Include(u => u.Participant)
				                           .FirstOrDefaultAsync(u => u.Id == user.Id);

			// Assert
			Assert.NotNull(reloadedUser);
			Assert.Equal("alice", reloadedUser.Username);
			Assert.Equal("ALICE", reloadedUser.UsernameNormalized);
			Assert.Equal("alice@example.test", reloadedUser.Email);
			Assert.Equal("hash", reloadedUser.PasswordHash);
			Assert.NotNull(reloadedUser.Participant);
			Assert.Equal("Alice", reloadedUser.Participant!.DisplayName);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserWithParticipantAsync"/> creates the user and participant
		/// without attempting role assignment when <c>assignDefaultUserRole</c> is <c>false</c>.
		/// </summary>
		[Fact]
		public async Task CreateUserWithParticipantAsync_WhenAssignDefaultUserRoleFalse_DoesNotAssignRole()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act
			UserEntity user = await service.CreateUserWithParticipantAsync(
				                  displayName: "Alice",
				                  username: "alice",
				                  email: "alice@example.test",
				                  passwordHash: "hash",
				                  assignDefaultUserRole: false,
				                  utcNow: utcNow);

			// Assert
			Assert.True(user.Id.Value > 0);
			Assert.True(user.ParticipantId.Value > 0);
			bool anyRoles = await Fixture.DbContext.UserRoles
				                .AsNoTracking()
				                .AnyAsync(ur => ur.UserId == user.Id);

			Assert.False(anyRoles);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserWithParticipantAsync"/> throws when default role assignment
		/// is requested but the default role does not exist.
		/// </summary>
		[Fact]
		public async Task CreateUserWithParticipantAsync_WhenDefaultRoleMissing_ThrowsInvalidOperationException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			// Special: CreateUserWithParticipantAsync() optionally assigns the default "user" role.
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
				         service.CreateUserWithParticipantAsync(
					         displayName: "Alice",
					         username: "alice",
					         email: "alice@example.test",
					         passwordHash: "hash",
					         assignDefaultUserRole: true,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("Default role 'user' does not exist.", ex.Message);
		}

		/// <summary>
		/// Test data for <see cref="CreateUserWithParticipantAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each
		/// row provides an invalid combination of fields that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string, string, string?, string, string>
			CreateUserWithParticipantAsync_InvalidInput_Data => new()
		{
			// Whitespace-only display name
			{ "Whitespace display name", "   ", "alice", "alice@example.test", "hash", "displayName" },

			// Whitespace-only username
			{ "Whitespace username", "Alice", "   ", "alice@example.test", "hash", "username" },

			// Whitespace-only password hash
			{ "Whitespace password hash", "Alice", "alice", "alice@example.test", "   ", "passwordHash" },

			// Display name exceeds the 100-character maximum
			{
				"Display name too long", new string('x', 101), "alice", "alice@example.test", "hash",
				"displayName"
			},

			// Username exceeds the 50-character maximum
			{
				"Username too long", "Alice", new string('x', 51), "alice@example.test", "hash",
				"username"
			},

			// Password hash exceeds the 255-character maximum
			{
				"Password hash too long", "Alice", "alice", "alice@example.test", new string('x', 256),
				"passwordHash"
			},

			// Email exceeds the 254-character maximum
			{ "Email too long", "Alice", "alice", new string('x', 255), "hash", "email" }
		};

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserWithParticipantAsync"/> rejects invalid inputs with an
		/// <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="displayName">The display name to pass to the method.</param>
		/// <param name="username">The username to pass to the method.</param>
		/// <param name="email">The email to pass to the method.</param>
		/// <param name="passwordHash">The password hash to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(CreateUserWithParticipantAsync_InvalidInput_Data))]
		public async Task CreateUserWithParticipantAsync_WhenInputInvalid_ThrowsArgumentException(
			string  scenario,
			string  displayName,
			string  username,
			string? email,
			string  passwordHash,
			string  expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.CreateUserWithParticipantAsync(
					         displayName: displayName,
					         username: username,
					         email: email,
					         passwordHash: passwordHash,
					         assignDefaultUserRole: false,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion

		#region GetUserByEmailAsync

		/// <summary>
		/// Verifies that <see cref="IUserDataService.GetUserByEmailAsync"/> returns <see langword="null"/> when no user
		/// with the email exists. The toggle parameter asserts the contract — both the regular EF branch and the
		/// compiled hot-path branch must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetUserByEmailAsync_WhenNotFound_ReturnsNull(bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			// Act
			UserEntity? user = await service.GetUserByEmailAsync("nobody@example.test");

			// Assert
			Assert.Null(user);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.GetUserByEmailAsync"/> trims email input and returns the matching
		/// user including <see cref="UserEntity.Participant"/>. The toggle parameter asserts the contract — both the
		/// regular EF branch and the compiled hot-path branch must yield the same result (trimming happens in C# before
		/// the query, so it must work uniformly across branches).
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetUserByEmailAsync_WhenEmailTrimmed_ReturnsUserIncludingParticipant(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act
			// Special: GetUserByEmailAsync() trims input and includes Participant navigation.
			UserEntity? reloaded = await service.GetUserByEmailAsync("  alice@example.test  ");

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal("alice", reloaded.Username);
			Assert.NotNull(reloaded.Participant);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.GetUserByEmailAsync"/> returns an entity that is not tracked by the
		/// underlying <see cref="DbContext"/>.
		/// </summary>
		/// <remarks>
		/// The service uses <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}(IQueryable{TEntity})"/>, so
		/// the entity state in the test context should be <see cref="EntityState.Detached"/>.
		/// </remarks>
		[Fact]
		public async Task GetUserByEmailAsync_WhenFound_ReturnsDetachedEntity()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act
			// Special: GetUserByEmailAsync() uses AsNoTracking.
			UserEntity? user = await service.GetUserByEmailAsync("alice@example.test");

			// Assert
			Assert.NotNull(user);
			EntityState userState = Fixture.DbContext.Entry(user).State;
			Assert.Equal(EntityState.Detached, userState);
		}

		/// <summary>
		/// Test data for <see cref="GetUserByEmailAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid email that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string> GetUserByEmailAsync_InvalidInput_Data => new()
		{
			// Whitespace-only email
			{ "Whitespace email", "   " },

			// Email exceeds the 254-character maximum
			{ "Email too long", new string('x', 255) }
		};

		/// <summary>
		/// Verifies that <see cref="IUserDataService.GetUserByEmailAsync"/> rejects invalid email inputs with an
		/// <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="email">The email to pass to the method.</param>
		[Theory]
		[MemberData(nameof(GetUserByEmailAsync_InvalidInput_Data))]
		public async Task GetUserByEmailAsync_WhenInputInvalid_ThrowsArgumentException(string scenario, string email)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GetUserByEmailAsync(email));
			Assert.Equal("email", ex.ParamName);
		}

		#endregion

		#region GetUserByUsernameAsync

		/// <summary>
		/// Verifies that <see cref="IUserDataService.GetUserByUsernameAsync"/> returns <see langword="null"/> when no
		/// user with the username exists. The toggle parameter asserts the contract — both the regular EF branch and
		/// the compiled hot-path branch must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetUserByUsernameAsync_WhenNotFound_ReturnsNull(bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			// Act
			UserEntity? user = await service.GetUserByUsernameAsync("nobody");

			// Assert
			Assert.Null(user);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.GetUserByUsernameAsync"/> trims username input and returns the
		/// matching user including <see cref="UserEntity.Participant"/>. The toggle parameter asserts the contract —
		/// both the regular EF branch and the compiled hot-path branch must yield the same result (trimming happens in
		/// C# before the query, so it must work uniformly across branches).
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetUserByUsernameAsync_WhenUsernameTrimmed_ReturnsUserIncludingParticipant(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act
			// Special: GetUserByUsernameAsync() trims input and includes Participant navigation.
			UserEntity? reloaded = await service.GetUserByUsernameAsync("  alice  ");

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal("alice@example.test", reloaded.Email);
			Assert.NotNull(reloaded.Participant);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.GetUserByUsernameAsync"/> performs case-insensitive lookup
		/// via <c>UsernameNormalized</c>.
		/// </summary>
		[Fact]
		public async Task GetUserByUsernameAsync_WhenCaseDiffers_ReturnsUser()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act
			// Special: GetUserByUsernameAsync() normalizes input to uppercase for comparison.
			UserEntity? reloaded = await service.GetUserByUsernameAsync("ALICE");

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal("alice", reloaded.Username);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.GetUserByUsernameAsync"/> returns an entity that is not tracked
		/// by the underlying <see cref="DbContext"/>.
		/// </summary>
		/// <remarks>
		/// The service uses <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}(IQueryable{TEntity})"/>,
		/// so the entity state in the test context should be <see cref="EntityState.Detached"/>.
		/// </remarks>
		[Fact]
		public async Task GetUserByUsernameAsync_WhenFound_ReturnsDetachedEntity()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act
			UserEntity? user = await service.GetUserByUsernameAsync("alice");

			// Assert
			Assert.NotNull(user);
			EntityState userState = Fixture.DbContext.Entry(user).State;
			Assert.Equal(EntityState.Detached, userState);
		}

		/// <summary>
		/// Test data for <see cref="GetUserByUsernameAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid username that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string> GetUserByUsernameAsync_InvalidInput_Data => new()
		{
			// Whitespace-only username
			{ "Whitespace username", "   " },

			// Username exceeds the 50-character maximum
			{ "Username too long", new string('x', 51) }
		};

		/// <summary>
		/// Verifies that <see cref="IUserDataService.GetUserByUsernameAsync"/> rejects invalid username inputs with an
		/// <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="username">The username to pass to the method.</param>
		[Theory]
		[MemberData(nameof(GetUserByUsernameAsync_InvalidInput_Data))]
		public async Task GetUserByUsernameAsync_WhenInputInvalid_ThrowsArgumentException(
			string scenario,
			string username)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GetUserByUsernameAsync(username));
			Assert.Equal("username", ex.ParamName);
		}

		#endregion

		#region EmailExistsAsync

		/// <summary>
		/// Verifies that <see cref="IUserDataService.EmailExistsAsync"/> returns <see langword="true"/> when a user with
		/// the email exists. The toggle parameter asserts the contract — both the regular EF branch and the compiled
		/// hot-path branch must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task EmailExistsAsync_WhenEmailExists_ReturnsTrue(bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			await CreateUserParticipantAsync(
				"alice",
				"alice@example.test",
				new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Act
			bool exists = await service.EmailExistsAsync("alice@example.test");

			// Assert
			Assert.True(exists);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.EmailExistsAsync"/> trims the input email before checking existence.
		/// </summary>
		[Fact]
		public async Task EmailExistsAsync_WhenEmailTrimmed_ReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			await CreateUserParticipantAsync(
				"alice",
				"alice@example.test",
				new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Act
			// Special: EmailExistsAsync() trims inputs.
			bool exists = await service.EmailExistsAsync("  alice@example.test  ");

			// Assert
			Assert.True(exists);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.EmailExistsAsync"/> returns <see langword="false"/> when no user
		/// with the email exists. The toggle parameter asserts the contract — both the regular EF branch and the
		/// compiled hot-path branch must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task EmailExistsAsync_WhenEmailDoesNotExist_ReturnsFalse(bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			// Act
			bool exists = await service.EmailExistsAsync("nobody@example.test");

			// Assert
			Assert.False(exists);
		}

		/// <summary>
		/// Test data for <see cref="EmailExistsAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each row provides
		/// an invalid email that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string> EmailExistsAsync_InvalidInput_Data => new()
		{
			// Whitespace-only email
			{ "Whitespace email", "   " },

			// Email exceeds the 254-character maximum
			{ "Email too long", new string('x', 255) }
		};

		/// <summary>
		/// Verifies that <see cref="IUserDataService.EmailExistsAsync"/> rejects invalid email inputs with an
		/// <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="email">The email to pass to the method.</param>
		[Theory]
		[MemberData(nameof(EmailExistsAsync_InvalidInput_Data))]
		public async Task EmailExistsAsync_WhenInputInvalid_ThrowsArgumentException(string scenario, string email)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.EmailExistsAsync(email));
			Assert.Equal("email", ex.ParamName);
		}

		#endregion

		#region UsernameExistsAsync

		/// <summary>
		/// Verifies that <see cref="IUserDataService.UsernameExistsAsync"/> returns <see langword="true"/> when a user
		/// with the username exists. The toggle parameter asserts the contract — both the regular EF branch and the
		/// compiled hot-path branch must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task UsernameExistsAsync_WhenUsernameExists_ReturnsTrue(bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			await CreateUserParticipantAsync(
				"alice",
				"alice@example.test",
				new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Act
			bool exists = await service.UsernameExistsAsync("alice");

			// Assert
			Assert.True(exists);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.UsernameExistsAsync"/> trims the input username before checking
		/// existence.
		/// </summary>
		[Fact]
		public async Task UsernameExistsAsync_WhenUsernameTrimmed_ReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			await CreateUserParticipantAsync(
				"alice",
				"alice@example.test",
				new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Act
			// Special: UsernameExistsAsync() trims inputs.
			bool exists = await service.UsernameExistsAsync("  alice  ");

			// Assert
			Assert.True(exists);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.UsernameExistsAsync"/> performs case-insensitive comparison
		/// via <c>UsernameNormalized</c>.
		/// </summary>
		[Fact]
		public async Task UsernameExistsAsync_WhenCaseDiffers_ReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			await CreateUserParticipantAsync(
				"alice",
				"alice@example.test",
				new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Act
			// Special: UsernameExistsAsync() normalizes input to uppercase for comparison.
			bool exists = await service.UsernameExistsAsync("ALICE");

			// Assert
			Assert.True(exists);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.UsernameExistsAsync"/> returns <see langword="false"/> when no user
		/// with the username exists. The toggle parameter asserts the contract — both the regular EF branch and the
		/// compiled hot-path branch must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task UsernameExistsAsync_WhenUsernameDoesNotExist_ReturnsFalse(bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			// Act
			bool exists = await service.UsernameExistsAsync("nobody");

			// Assert
			Assert.False(exists);
		}

		/// <summary>
		/// Test data for <see cref="UsernameExistsAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid username that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string> UsernameExistsAsync_InvalidInput_Data => new()
		{
			// Whitespace-only username
			{ "Whitespace username", "   " },

			// Username exceeds the 50-character maximum
			{ "Username too long", new string('x', 51) }
		};

		/// <summary>
		/// Verifies that <see cref="IUserDataService.UsernameExistsAsync"/> rejects invalid username inputs with an
		/// <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="username">The username to pass to the method.</param>
		[Theory]
		[MemberData(nameof(UsernameExistsAsync_InvalidInput_Data))]
		public async Task UsernameExistsAsync_WhenInputInvalid_ThrowsArgumentException(string scenario, string username)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UsernameExistsAsync(username));
			Assert.Equal("username", ex.ParamName);
		}

		#endregion

		#region DeleteUserAndScrubParticipantAsync

		/// <summary>
		/// Verifies that <see cref="IUserDataService.DeleteUserAndScrubParticipantAsync"/> deletes the user row, scrubs the
		/// related participant profile, and (when enabled) redacts messages authored by that participant.
		/// </summary>
		/// <remarks>
		/// The test configures the service to redact messages but to keep conversations intact, making the assertions
		/// deterministic.
		/// </remarks>
		[Fact]
		public async Task DeleteUserAndScrubParticipantAsync_WhenUserExists_DeletesUserAndScrubsParticipant()
		{
			// Arrange
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			(ParticipantEntity participant, UserEntity user, ConversationEntity _, MessageEntity message) =
				await CreateUserWithConversationAndMessageAsync(utcNow);

			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o =>
				{
					o.UserDeletion.RedactMessages = true;
					o.UserDeletion.DeletePrivateConversations = false;
				});

			// Act
			bool deleted = await service.DeleteUserAndScrubParticipantAsync(user.Id);

			// Assert
			Assert.True(deleted);

			UserEntity? userAfter = await Fixture.DbContext.Users
				                        .AsNoTracking()
				                        .FirstOrDefaultAsync(u => u.Id == user.Id);
			Assert.Null(userAfter);

			ParticipantEntity? participantAfter = await Fixture.DbContext.Participants
				                                      .AsNoTracking()
				                                      .FirstOrDefaultAsync(p => p.Id == participant.Id);
			Assert.NotNull(participantAfter);
			Assert.Equal("Deleted user", participantAfter.DisplayName);

			MessageEntity? messageAfter = await Fixture.DbContext.Messages
				                              .AsNoTracking()
				                              .FirstOrDefaultAsync(m => m.Id == message.Id);
			Assert.NotNull(messageAfter);
			Assert.Null(messageAfter.Content);
			Assert.Equal(MessageRedactionReason.UserDeleted, messageAfter.RedactionReason);
			Assert.NotNull(messageAfter.RedactedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.DeleteUserAndScrubParticipantAsync"/> returns <c>false</c> when the user
		/// does not exist.
		/// </summary>
		[Fact]
		public async Task DeleteUserAndScrubParticipantAsync_WhenUserDoesNotExist_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool deleted = await service.DeleteUserAndScrubParticipantAsync(userId: new UserId(12345));

			// Assert
			Assert.False(deleted);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.DeleteUserAndScrubParticipantAsync"/> does not redact message content
		/// when <see cref="DatabaseOptions.UserDeletionOptions.RedactMessages"/> is disabled.
		/// </summary>
		[Fact]
		public async Task DeleteUserAndScrubParticipantAsync_WhenRedactionDisabled_DoesNotRedactMessages()
		{
			// Arrange
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			(ParticipantEntity _, UserEntity user, ConversationEntity _, MessageEntity message) =
				await CreateUserWithConversationAndMessageAsync(utcNow);

			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o =>
				{
					o.UserDeletion.RedactMessages = false;
					o.UserDeletion.DeletePrivateConversations = false;
				});

			// Act
			bool deleted = await service.DeleteUserAndScrubParticipantAsync(user.Id);

			// Assert
			Assert.True(deleted);

			MessageEntity? messageAfter = await Fixture.DbContext.Messages
				                              .AsNoTracking()
				                              .FirstOrDefaultAsync(m => m.Id == message.Id);

			Assert.NotNull(messageAfter);
			Assert.Equal("Hi", messageAfter.Content);
			Assert.Null(messageAfter.RedactedAtUtc);
			Assert.Null(messageAfter.RedactionReason);
		}

		/// <summary>
		/// Verifies the branch where message redaction is enabled, but the participant has authored no messages.
		/// </summary>
		[Fact]
		public async Task
			DeleteUserAndScrubParticipantAsync_WhenRedactionEnabledButNoMessages_ScrubsParticipantAndReturnsTrue()
		{
			// Arrange
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			(ParticipantEntity participant, UserEntity user) =
				await CreateUserParticipantWithUserAsync("alice", "alice@example.test", utcNow);

			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o =>
				{
					o.UserDeletion.RedactMessages = true;
					o.UserDeletion.DeletePrivateConversations = false;
				});

			// Act
			bool deleted = await service.DeleteUserAndScrubParticipantAsync(user.Id);

			// Assert
			Assert.True(deleted);

			ParticipantEntity? participantAfter = await Fixture.DbContext.Participants
				                                      .AsNoTracking()
				                                      .FirstOrDefaultAsync(p => p.Id == participant.Id);

			Assert.NotNull(participantAfter);
			Assert.Equal("Deleted user", participantAfter.DisplayName);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.DeleteUserAndScrubParticipantAsync"/> deletes the user's private
		/// conversations when <see cref="DatabaseOptions.UserDeletionOptions.DeletePrivateConversations"/> is enabled.
		/// </summary>
		[Fact]
		public async Task
			DeleteUserAndScrubParticipantAsync_WhenDeletePrivateConversationsEnabled_DeletesPrivateConversation()
		{
			// Arrange
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			(ParticipantEntity participant, UserEntity user) =
				await CreateUserParticipantWithUserAsync("alice", "alice@example.test", utcNow);

			// A private conversation: only one user participant (the deleted user).
			var privateConversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "Private",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(privateConversation);
			await Fixture.DbContext.SaveChangesAsync();

			Fixture.DbContext.ConversationParticipants.Add(
				new ConversationParticipantEntity
				{
					ConversationId = privateConversation.Id,
					ParticipantId = participant.Id,
					Role = ConversationParticipantRole.Owner,
					JoinedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o =>
				{
					o.UserDeletion.DeletePrivateConversations = true;
					o.UserDeletion.RedactMessages = false;
				});

			// Act
			bool deleted = await service.DeleteUserAndScrubParticipantAsync(user.Id);

			// Assert
			Assert.True(deleted);

			bool conversationStillExists = await Fixture.DbContext.Conversations
				                               .AsNoTracking()
				                               .AnyAsync(c => c.Id == privateConversation.Id);
			Assert.False(conversationStillExists);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.DeleteUserAndScrubParticipantAsync"/> validates the user id and throws
		/// <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task DeleteUserAndScrubParticipantAsync_WhenUserIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.DeleteUserAndScrubParticipantAsync(userId: new UserId(0)));
			Assert.Equal("userId.Value", ex.ParamName);
		}

		/// <summary>
		/// Creates a user participant with a linked conversation, conversation membership, and message for deletion
		/// scenario tests.
		/// </summary>
		/// <param name="utcNow">The UTC timestamp used for all <c>CreatedAtUtc</c> fields.</param>
		/// <returns>
		/// A tuple containing the created <see cref="ParticipantEntity"/>, <see cref="UserEntity"/>,
		/// <see cref="ConversationEntity"/>, and <see cref="MessageEntity"/>.
		/// </returns>
		private async Task<(
				ParticipantEntity Participant,
				UserEntity User,
				ConversationEntity Conversation,
				MessageEntity Message
				)>
			CreateUserWithConversationAndMessageAsync(DateTime utcNow)
		{
			var participant = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Alice",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(participant);
			await Fixture.DbContext.SaveChangesAsync();

			var user = new UserEntity
			{
				ParticipantId = participant.Id,
				CreatedAtUtc = utcNow,
				Username = "alice",
				UsernameNormalized = "ALICE",
				Email = "alice@example.test",
				PasswordHash = "hash"
			};
			Fixture.DbContext.Users.Add(user);
			await Fixture.DbContext.SaveChangesAsync();

			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "Conversation",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);
			await Fixture.DbContext.SaveChangesAsync();

			Fixture.DbContext.ConversationParticipants.Add(
				new ConversationParticipantEntity
				{
					ConversationId = conversation.Id,
					ParticipantId = participant.Id,
					Role = ConversationParticipantRole.Owner,
					JoinedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			var message = new MessageEntity
			{
				PublicId = Guid.NewGuid(),
				ConversationId = conversation.Id,
				SenderId = participant.Id,
				Content = "Hi",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Messages.Add(message);
			await Fixture.DbContext.SaveChangesAsync();

			return (participant, user, conversation, message);
		}

		#endregion

		#region UTC clock fallback

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateParticipantAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for <see cref="ParticipantEntity.CreatedAtUtc"/> when the optional
		/// <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c> and silently
		/// captures wall-clock time instead.
		/// </remarks>
		[Fact]
		public async Task CreateParticipantAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			// Act
			ParticipantEntity participant = await service.CreateParticipantAsync(displayName: "Alice");

			// Assert
			Assert.Equal(fixedNow, participant.CreatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for <see cref="UserEntity.CreatedAtUtc"/> when the optional <c>utcNow</c>
		/// argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c>.
		/// </remarks>
		[Fact]
		public async Task CreateUserAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			ParticipantEntity participant = await service.CreateParticipantAsync(
				                                displayName: "Alice",
				                                utcNow: fixedNow.AddMinutes(-1));

			// Act
			UserEntity user = await service.CreateUserAsync(
				                  participant.Id,
				                  username: "alice",
				                  email: "alice@example.test",
				                  passwordHash: "hash");

			// Assert
			Assert.Equal(fixedNow, user.CreatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.CreateUserWithParticipantAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for both the participant and user <c>CreatedAtUtc</c> values when the optional
		/// <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c>.
		/// </remarks>
		[Fact]
		public async Task CreateUserWithParticipantAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			// Act
			UserEntity user = await service.CreateUserWithParticipantAsync(
				                  displayName: "Alice",
				                  username: "alice",
				                  email: "alice@example.test",
				                  passwordHash: "hash",
				                  assignDefaultUserRole: false);

			UserEntity? reloaded = await Fixture.DbContext.Users
				                       .AsNoTracking()
				                       .Include(u => u.Participant)
				                       .FirstOrDefaultAsync(u => u.Id == user.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal(fixedNow, reloaded.CreatedAtUtc);
			Assert.NotNull(reloaded.Participant);
			Assert.Equal(fixedNow, reloaded.Participant.CreatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IUserDataService.DeleteUserAndScrubParticipantAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for <see cref="MessageEntity.RedactedAtUtc"/> on bulk message redaction when
		/// the optional <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c>.
		/// </remarks>
		[Fact]
		public async Task DeleteUserAndScrubParticipantAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				// Keep the conversation around so the message survives deletion and we can observe its
				// redaction timestamp written by the bulk ExecuteUpdateAsync path.
				configure: o => o.UserDeletion.DeletePrivateConversations = false,
				timeProvider: clock);

			DateTime seedNow = fixedNow.AddDays(-1);
			(ParticipantEntity _, UserEntity user, ConversationEntity _, MessageEntity message) =
				await CreateUserWithConversationAndMessageAsync(seedNow);

			// Act
			bool deleted = await service.DeleteUserAndScrubParticipantAsync(user.Id);

			// Assert
			Assert.True(deleted);
			MessageEntity? reloaded = await Fixture.DbContext.Messages
				                          .AsNoTracking()
				                          .FirstOrDefaultAsync(m => m.Id == message.Id);
			Assert.NotNull(reloaded);
			Assert.Equal(fixedNow, reloaded.RedactedAtUtc);
		}

		#endregion
	}
}
