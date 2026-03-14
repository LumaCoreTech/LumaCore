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
	/// Tests for <see cref="IConversationDataService"/> methods.
	/// </summary>
	/// <remarks>
	/// These tests cover creation, membership management, title updates, deletion, and lookup behaviors.
	/// The suite intentionally exercises both guard clauses (input validation, missing rows) and
	/// persistence behaviors (join rows, timestamps) to ensure the implementation is robust across EF Core providers.
	/// </remarks>
	[Trait("Category", "Data")]
	public sealed class Conversations : TestBase
	{
		#region CreateConversationAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.CreateConversationAsync"/> creates a fully populated
		/// conversation and joins the creator as <see cref="ConversationParticipantRole.Owner"/>.
		/// </summary>
		[Fact]
		public async Task CreateConversationAsync_WhenValid_CreatesConversationAndJoinsCreatorAsOwner()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Hello",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			ConversationEntity? reloaded = await Fixture.DbContext.Conversations
				                               .AsNoTracking()
				                               .FirstOrDefaultAsync(c => c.Id == conversation.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.NotEqual(Guid.Empty, reloaded.PublicId);
			Assert.Equal("Hello", reloaded.Title);
			Assert.Equal(utcNow, reloaded.CreatedAtUtc);
			Assert.Equal(utcNow, reloaded.UpdatedAtUtc);

			// Verify the creator was joined as Owner.
			ConversationParticipantEntity? joinRow = await Fixture.DbContext.ConversationParticipants
				                                         .AsNoTracking()
				                                         .FirstOrDefaultAsync(cp =>
					                                         cp.ConversationId == conversation.Id &&
					                                         cp.ParticipantId == participant.Id);

			Assert.NotNull(joinRow);
			Assert.Equal(ConversationParticipantRole.Owner, joinRow.Role);
			Assert.Equal(utcNow, joinRow.JoinedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.CreateConversationAsync"/> trims leading and trailing
		/// whitespace from the title.
		/// </summary>
		/// <remarks>
		/// The test asserts that the persisted <see cref="ConversationEntity.Title"/> is stored in normalized form.
		/// </remarks>
		[Fact]
		public async Task CreateConversationAsync_WhenTitleHasLeadingTrailingWhitespace_TrimsTitle()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var participant = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Alice",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(participant);
			await Fixture.DbContext.SaveChangesAsync();

			Fixture.DbContext.Users.Add(
				new UserEntity
				{
					ParticipantId = participant.Id,
					Username = "alice",
					UsernameNormalized = "ALICE",
					Email = "alice@example.test",
					PasswordHash = "hash"
				});
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "  Hello  ",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			// Assert
			Assert.Equal("Hello", conversation.Title);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.CreateConversationAsync"/> rejects creators that are not
		/// backed by a <see cref="UserEntity"/>.
		/// </summary>
		/// <remarks>
		/// This test seeds only a <see cref="ParticipantEntity"/> (no <see cref="UserEntity"/> referencing it) and asserts
		/// that the service throws <see cref="InvalidOperationException"/> instead of creating a conversation.
		/// </remarks>
		[Fact]
		public async Task CreateConversationAsync_WhenCreatorIsNotUser_ThrowsInvalidOperationException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Special: CreateConversationAsync() is restricted to *user* participants (Participant rows referenced by a User).
			// A plain Participant without a User row behaves like a persona/bot and should be rejected.
			var participant = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Persona",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(participant);
			await Fixture.DbContext.SaveChangesAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
				         service.CreateConversationAsync(
					         title: "Conversation",
					         creatorParticipantId: participant.Id,
					         utcNow: utcNow));
			Assert.Matches(@"^Creator participant '.+' is not a user participant\.$", ex.Message);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.CreateConversationAsync"/> validates the creator participant id
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task CreateConversationAsync_WhenCreatorParticipantIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.CreateConversationAsync(
					         title: "Conversation",
					         creatorParticipantId: new ParticipantId(0),
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("creatorParticipantId.Value", ex.ParamName);
		}

		/// <summary>
		/// Test data for <see cref="CreateConversationAsync_WhenTitleInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid title that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string> CreateConversationAsync_InvalidTitle_Data => new()
		{
			// Whitespace-only title
			{ "Whitespace title", "   " },
			// Title exceeds the 200-character maximum
			{ "Title too long", new string('x', 201) }
		};

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.CreateConversationAsync"/> rejects invalid titles with an
		/// <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="title">The title to pass to the method.</param>
		[Theory]
		[MemberData(nameof(CreateConversationAsync_InvalidTitle_Data))]
		public async Task CreateConversationAsync_WhenTitleInvalid_ThrowsArgumentException(
			string scenario,
			string title)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.CreateConversationAsync(
					         title: title,
					         creatorParticipantId: new ParticipantId(1),
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("title", ex.ParamName);
		}

		#endregion

		#region AddParticipantToConversationAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.AddParticipantToConversationAsync"/> returns <c>true</c> and
		/// creates a join row when the participant is added for the first time.
		/// </summary>
		/// <remarks>
		/// The test asserts that <see cref="ConversationParticipantEntity"/> exists with the expected
		/// <see cref="ConversationParticipantEntity.Role"/> and <see cref="ConversationParticipantEntity.JoinedAtUtc"/>.
		/// </remarks>
		[Fact]
		public async Task AddParticipantToConversationAsync_WhenFirstTime_ReturnsTrueAndCreatesJoinRow()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "owner@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: owner.Id,
				                                  utcNow: utcNow);

			var member = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Member",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(member);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			DateTime joinUtcNow = utcNow.AddMinutes(1);
			bool added = await service.AddParticipantToConversationAsync(
				             conversationId: conversation.Id,
				             participantId: member.Id,
				             role: ConversationParticipantRole.Member,
				             utcNow: joinUtcNow);

			// Assert
			Assert.True(added);

			ConversationParticipantEntity? join = await Fixture.DbContext.ConversationParticipants
				                                      .AsNoTracking()
				                                      .FirstOrDefaultAsync(cp =>
					                                      cp.ConversationId == conversation.Id &&
					                                      cp.ParticipantId == member.Id);

			Assert.NotNull(join);
			Assert.Equal(ConversationParticipantRole.Member, join.Role);
			Assert.Equal(joinUtcNow, join.JoinedAtUtc);
		}

		/// <summary>
		/// Verifies the duplicate-handling branch of <see cref="IConversationDataService.AddParticipantToConversationAsync"/>
		/// by forcing a database uniqueness violation.
		/// </summary>
		/// <remarks>
		/// A second service instance backed by a fresh <see cref="DbContext"/> is used so EF does not short-circuit the
		/// attempt due to tracking identity conflicts.
		/// </remarks>
		[Fact]
		public async Task AddParticipantToConversationAsync_WhenDuplicate_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "Test",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);

			var participant = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Alice",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(participant);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			bool first = await service.AddParticipantToConversationAsync(
				             conversation.Id,
				             participant.Id,
				             ConversationParticipantRole.Member,
				             utcNow);

			// Assert
			Assert.True(first);

			// Special: This method's duplicate-handling logic lives in a DbUpdateException catch block.
			// Using a new DbContext forces the duplicate insert to be validated by the database constraint.
			LumaCoreDataService service2 = LumaCoreDataServiceFactory.Create(Fixture.CreateDbContext());
			bool second = await service2.AddParticipantToConversationAsync(
				              conversation.Id,
				              participant.Id,
				              ConversationParticipantRole.Member,
				              utcNow);
			Assert.False(second);

			int count = await Fixture.DbContext.ConversationParticipants.CountAsync();
			Assert.Equal(1, count);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.AddParticipantToConversationAsync"/> rethrows
		/// <see cref="DbUpdateException"/> when the failure is not a duplicate join row.
		/// </summary>
		/// <remarks>
		/// We provoke a foreign key violation by using non-existing ConversationId/ParticipantId. The implementation
		/// catches <see cref="DbUpdateException"/> and only returns <c>false</c> if the join already exists; otherwise it
		/// must rethrow.
		/// </remarks>
		[Fact]
		public async Task AddParticipantToConversationAsync_WhenForeignKeyViolation_RethrowsDbUpdateException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act + Assert
			await Assert.ThrowsAsync<DbUpdateException>(() =>
				service.AddParticipantToConversationAsync(
					conversationId: new ConversationId(999),
					participantId: new ParticipantId(999),
					role: ConversationParticipantRole.Member,
					utcNow: utcNow));
		}

		/// <summary>
		/// Test data for <see cref="AddParticipantToConversationAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// Each row provides an invalid id combination that triggers an <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, ConversationId, ParticipantId, string>
			AddParticipantToConversationAsync_InvalidId_Data => new()
		{
			// Conversation id is zero
			{ "Zero conversationId", new ConversationId(0), new ParticipantId(1), "conversationId.Value" },
			// Participant id is zero
			{ "Zero participantId", new ConversationId(1), new ParticipantId(0), "participantId.Value" }
		};

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.AddParticipantToConversationAsync"/> validates id parameters
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="conversationId">The conversation id to pass to the method.</param>
		/// <param name="participantId">The participant id to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentOutOfRangeException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(AddParticipantToConversationAsync_InvalidId_Data))]
		public async Task AddParticipantToConversationAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException(
			string         scenario,
			ConversationId conversationId,
			ParticipantId  participantId,
			string         expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.AddParticipantToConversationAsync(
					         conversationId,
					         participantId,
					         ConversationParticipantRole.Member,
					         utcNow));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion

		#region UpdateConversationTitleAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationTitleAsync"/> updates the stored title and
		/// sets <see cref="ConversationEntity.UpdatedAtUtc"/> when the conversation exists.
		/// </summary>
		/// <remarks>
		/// The implementation uses a set-based update; therefore, the test reloads the entity with
		/// <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}(IQueryable{TEntity})"/> to validate persisted
		/// values.
		/// </remarks>
		[Fact]
		public async Task UpdateConversationTitleAsync_WhenConversationExists_UpdatesTitleAndUpdatedAtUtc()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Initial",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			DateTime updateUtcNow = utcNow.AddMinutes(5);

			// Act
			bool updated = await service.UpdateConversationTitleAsync(
				               conversationId: conversation.Id,
				               title: "Renamed",
				               utcNow: updateUtcNow);

			// Assert
			Assert.True(updated);

			// Special: UpdateConversationTitleAsync() uses ExecuteUpdateAsync() (set-based update), so we reload.
			ConversationEntity? reloaded = await Fixture.DbContext.Conversations
				                               .AsNoTracking()
				                               .FirstOrDefaultAsync(c => c.Id == conversation.Id);

			Assert.NotNull(reloaded);
			Assert.Equal("Renamed", reloaded.Title);
			Assert.Equal(updateUtcNow, reloaded.UpdatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationTitleAsync"/> trims leading and
		/// trailing whitespace from the new title before persisting.
		/// </summary>
		[Fact]
		public async Task UpdateConversationTitleAsync_WhenTitleHasWhitespace_TrimsTitle()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Initial",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			// Act
			bool updated = await service.UpdateConversationTitleAsync(
				               conversationId: conversation.Id,
				               title: "  Renamed  ",
				               utcNow: utcNow.AddMinutes(5));

			// Special: UpdateConversationTitleAsync() trims the title via Guard.ThrowIfNullOrEmptyOrTooLong().
			ConversationEntity? reloaded = await Fixture.DbContext.Conversations
				                               .AsNoTracking()
				                               .FirstOrDefaultAsync(c => c.Id == conversation.Id);

			// Assert
			Assert.True(updated);
			Assert.NotNull(reloaded);
			Assert.Equal("Renamed", reloaded.Title);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationTitleAsync"/> returns <c>false</c> when the
		/// conversation id does not exist.
		/// </summary>
		[Fact]
		public async Task UpdateConversationTitleAsync_WhenConversationDoesNotExist_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool updated = await service.UpdateConversationTitleAsync(
				               conversationId: new ConversationId(12345),
				               title: "Renamed",
				               utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Assert
			Assert.False(updated);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationTitleAsync"/> validates the conversation id
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task UpdateConversationTitleAsync_WhenConversationIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.UpdateConversationTitleAsync(
					         conversationId: new ConversationId(0),
					         title: "Renamed",
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("conversationId.Value", ex.ParamName);
		}

		/// <summary>
		/// Test data for <see cref="UpdateConversationTitleAsync_WhenTitleInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid title that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string> UpdateConversationTitleAsync_InvalidTitle_Data => new()
		{
			// Whitespace-only title
			{ "Whitespace title", "   " },
			// Title exceeds the 200-character maximum
			{ "Title too long", new string('x', 201) }
		};

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationTitleAsync"/> rejects invalid titles with
		/// an <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="title">The title to pass to the method.</param>
		[Theory]
		[MemberData(nameof(UpdateConversationTitleAsync_InvalidTitle_Data))]
		public async Task UpdateConversationTitleAsync_WhenTitleInvalid_ThrowsArgumentException(
			string scenario,
			string title)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.UpdateConversationTitleAsync(
					         conversationId: new ConversationId(1),
					         title: title,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("title", ex.ParamName);
		}

		#endregion

		#region DeleteConversationAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.DeleteConversationAsync"/> returns <c>false</c> when the
		/// conversation cannot be found.
		/// </summary>
		[Fact]
		public async Task DeleteConversationAsync_WhenConversationNotFound_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool deleted = await service.DeleteConversationAsync(conversationId: new ConversationId(12345));

			// Assert
			Assert.False(deleted);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.DeleteConversationAsync"/> deletes an existing conversation
		/// and returns <c>true</c>.
		/// </summary>
		/// <remarks>
		/// The conversation is created through the service so it has valid creator membership. The test then asserts
		/// that the row no longer exists in <see cref="LumaCoreDbContext.Conversations"/>.
		/// </remarks>
		[Fact]
		public async Task DeleteConversationAsync_WhenConversationExists_DeletesAndReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			// Act
			bool deleted = await service.DeleteConversationAsync(conversationId: conversation.Id);

			// Assert
			Assert.True(deleted);

			bool stillExists = await Fixture.DbContext.Conversations
				                   .AsNoTracking()
				                   .AnyAsync(c => c.Id == conversation.Id);
			Assert.False(stillExists);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.DeleteConversationAsync"/> validates the conversation id and
		/// throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task DeleteConversationAsync_WhenConversationIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.DeleteConversationAsync(conversationId: new ConversationId(0)));
			Assert.Equal("conversationId.Value", ex.ParamName);
		}

		#endregion

		#region DeleteAllPrivateConversationsByUserParticipantAsync

		/// <summary>
		/// Verifies that
		/// <see cref="LumaCoreDataService.DeleteAllPrivateConversationsByUserParticipantAsync(ParticipantId, CancellationToken)"/>
		/// deletes conversations that are private to a single user and reports multi-user conversations as skipped.
		/// </summary>
		/// <remarks>
		/// "Private" is determined by counting active users (<see cref="UserEntity"/>) per conversation. The test seeds
		/// two user participants, creates a private conversation, and then creates a multi-user conversation by adding
		/// the second user as a member.
		/// </remarks>
		[Fact]
		public async Task
			DeleteAllPrivateConversationsByUserParticipantAsync_WhenMixOfPrivateAndMultiUser_DeletesPrivateAndSkipsMultiUser()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Special: "private" is determined by counting active user accounts per conversation.
			ParticipantEntity user1Participant = await CreateUserParticipantAsync("u1", "u1@example.test", utcNow);
			ParticipantEntity user2Participant = await CreateUserParticipantAsync("u2", "u2@example.test", utcNow);

			ConversationEntity privateConversation = await service.CreateConversationAsync(
				                                         title: "Private",
				                                         creatorParticipantId: user1Participant.Id,
				                                         utcNow: utcNow);

			ConversationEntity multiUserConversation = await service.CreateConversationAsync(
				                                           title: "Multi",
				                                           creatorParticipantId: user1Participant.Id,
				                                           utcNow: utcNow);

			bool added = await service.AddParticipantToConversationAsync(
				             conversationId: multiUserConversation.Id,
				             participantId: user2Participant.Id,
				             role: ConversationParticipantRole.Member,
				             utcNow: utcNow.AddMinutes(1));
			Assert.True(added);

			// Act
			DeletePrivateConversationsResult result = await service
				                                          .DeleteAllPrivateConversationsByUserParticipantAsync(
					                                          user1Participant.Id);

			// Assert
			Assert.Equal(1, result.Deleted);
			Assert.Equal(1, result.SkippedMultiUser);

			bool privateStillExists = await Fixture.DbContext.Conversations
				                          .AsNoTracking()
				                          .AnyAsync(c => c.Id == privateConversation.Id);
			Assert.False(privateStillExists);

			bool multiStillExists = await Fixture.DbContext.Conversations
				                        .AsNoTracking()
				                        .AnyAsync(c => c.Id == multiUserConversation.Id);
			Assert.True(multiStillExists);
		}

		/// <summary>
		/// Verifies the early-return branch where
		/// <see cref="LumaCoreDataService.DeleteAllPrivateConversationsByUserParticipantAsync(ParticipantId, CancellationToken)"/>
		/// finds no candidate conversations for the participant.
		/// </summary>
		[Fact]
		public async Task
			DeleteAllPrivateConversationsByUserParticipantAsync_WhenNoCandidateConversations_ReturnsZeros()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			DeletePrivateConversationsResult result = await service
				                                          .DeleteAllPrivateConversationsByUserParticipantAsync(
					                                          userParticipantId: new ParticipantId(12345));

			// Assert
			Assert.Equal(0, result.Deleted);
			Assert.Equal(0, result.SkippedMultiUser);
		}

		/// <summary>
		/// Verifies the early-return branch where
		/// <see cref="LumaCoreDataService.DeleteAllPrivateConversationsByUserParticipantAsync(ParticipantId, CancellationToken)"/>
		/// finds candidates but none qualify as private (all are multi-user).
		/// </summary>
		[Fact]
		public async Task
			DeleteAllPrivateConversationsByUserParticipantAsync_WhenNoPrivateConversations_ReturnsZeroDeletedAndSkipped()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity user1Participant = await CreateUserParticipantAsync("u1", "u1@example.test", utcNow);
			ParticipantEntity user2Participant = await CreateUserParticipantAsync("u2", "u2@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Multi",
				                                  creatorParticipantId: user1Participant.Id,
				                                  utcNow: utcNow);

			bool added = await service.AddParticipantToConversationAsync(
				             conversationId: conversation.Id,
				             participantId: user2Participant.Id,
				             role: ConversationParticipantRole.Member,
				             utcNow: utcNow.AddMinutes(1));
			Assert.True(added);

			// Act
			DeletePrivateConversationsResult result = await service
				                                          .DeleteAllPrivateConversationsByUserParticipantAsync(
					                                          user1Participant.Id);

			// Assert
			Assert.Equal(0, result.Deleted);
			Assert.Equal(1, result.SkippedMultiUser);

			bool stillExists = await Fixture.DbContext.Conversations
				                   .AsNoTracking()
				                   .AnyAsync(c => c.Id == conversation.Id);
			Assert.True(stillExists);
		}

		/// <summary>
		/// Verifies that
		/// <see cref="LumaCoreDataService.DeleteAllPrivateConversationsByUserParticipantAsync(ParticipantId, CancellationToken)"/>
		/// validates the user participant id and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task
			DeleteAllPrivateConversationsByUserParticipantAsync_WhenUserParticipantIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.DeleteAllPrivateConversationsByUserParticipantAsync(
					         userParticipantId: new ParticipantId(0)));
			Assert.Equal("userParticipantId.Value", ex.ParamName);
		}

		#endregion

		#region GetConversationByPublicIdAsync

		/// <summary>
		/// Verifies that <see cref="LumaCoreDataService.GetConversationByPublicIdAsync(Guid,CancellationToken)"/> can use the
		/// compiled-query hot path when enabled.
		/// </summary>
		/// <remarks>
		/// The compiled-query branch does not accept a <see cref="CancellationToken"/>; this test validates only functional
		/// behavior (it returns the expected conversation).
		/// </remarks>
		[Fact]
		public async Task GetConversationByPublicIdAsync_WhenPreferCompiledHotPathQueriesEnabled_ReturnsConversation()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = true);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "Conversation",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			ConversationEntity? loaded = await service.GetConversationByPublicIdAsync(conversation.PublicId);

			// Assert
			Assert.NotNull(loaded);
			Assert.Equal(conversation.Id, loaded.Id);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetConversationByPublicIdAsync"/> returns
		/// <see langword="null"/> when the compiled-query hot path is enabled and the conversation does not exist.
		/// </summary>
		[Fact]
		public async Task
			GetConversationByPublicIdAsync_WhenPreferCompiledHotPathQueriesEnabled_AndNotFound_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = true);

			// Act
			ConversationEntity? c = await service.GetConversationByPublicIdAsync(Guid.NewGuid());

			// Assert
			Assert.Null(c);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetConversationByPublicIdAsync"/> returns the matching
		/// conversation as a detached entity when the non-compiled query path is used.
		/// </summary>
		[Fact]
		public async Task GetConversationByPublicIdAsync_WhenFound_ReturnsDetachedConversation()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "Conversation",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			ConversationEntity? loaded = await service.GetConversationByPublicIdAsync(conversation.PublicId);

			// Assert
			Assert.NotNull(loaded);
			Assert.Equal(conversation.Id, loaded.Id);
			Assert.Equal("Conversation", loaded.Title);
			Assert.Equal(conversation.PublicId, loaded.PublicId);

			EntityState state = Fixture.DbContext.Entry(loaded).State;
			Assert.Equal(EntityState.Detached, state);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetConversationByPublicIdAsync"/> returns <c>null</c> when no
		/// conversation with the specified public id exists.
		/// </summary>
		[Fact]
		public async Task GetConversationByPublicIdAsync_WhenNotFound_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			ConversationEntity? c = await service.GetConversationByPublicIdAsync(Guid.NewGuid());

			// Assert
			Assert.Null(c);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetConversationByPublicIdAsync(Guid,CancellationToken)"/> validates
		/// the public id and throws <see cref="ArgumentException"/> for <see cref="Guid.Empty"/>.
		/// </summary>
		[Fact]
		public async Task GetConversationByPublicIdAsync_WhenPublicIdEmpty_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.GetConversationByPublicIdAsync(Guid.Empty));
			Assert.Equal("publicId", ex.ParamName);
		}

		#endregion

		#region ListConversationsByParticipantAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.ListConversationsByParticipantAsync"/> returns conversations
		/// ordered by descending <see cref="ConversationEntity.UpdatedAtUtc"/>.
		/// </summary>
		[Fact]
		public async Task
			ListConversationsByParticipantAsync_WhenMultipleConversationsExist_ReturnsOrderedByUpdatedAtUtcDescending()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			ConversationEntity older = await service.CreateConversationAsync("Old", participant.Id, utcNow);
			ConversationEntity newer = await service.CreateConversationAsync(
				                           "New",
				                           participant.Id,
				                           utcNow.AddMinutes(1));

			// Push UpdatedAtUtc apart via title updates.
			await service.UpdateConversationTitleAsync(older.Id, "Old2", utcNow.AddMinutes(2));
			await service.UpdateConversationTitleAsync(newer.Id, "New2", utcNow.AddMinutes(3));

			// Act
			List<ConversationEntity> list = await service.ListConversationsByParticipantAsync(participant.Id);

			// Assert
			Assert.Equal(2, list.Count);
			Assert.Equal(newer.Id, list[0].Id);
			Assert.Equal(older.Id, list[1].Id);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.ListConversationsByParticipantAsync"/> includes
		/// conversations where the participant was added as a <see cref="ConversationParticipantRole.Member"/>,
		/// not only those where they are the <see cref="ConversationParticipantRole.Owner"/>.
		/// </summary>
		[Fact]
		public async Task ListConversationsByParticipantAsync_WhenMember_IncludesConversation()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "owner@example.test", utcNow);
			ParticipantEntity member = await CreateUserParticipantAsync("member", "member@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Shared",
				                                  creatorParticipantId: owner.Id,
				                                  utcNow: utcNow);

			bool added = await service.AddParticipantToConversationAsync(
				             conversationId: conversation.Id,
				             participantId: member.Id,
				             role: ConversationParticipantRole.Member,
				             utcNow: utcNow.AddMinutes(1));
			Assert.True(added);

			// Act
			// Special: The member was not the creator — verify they still see the conversation.
			List<ConversationEntity> list = await service.ListConversationsByParticipantAsync(member.Id);

			// Assert
			Assert.Single(list);
			Assert.Equal(conversation.Id, list[0].Id);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.ListConversationsByParticipantAsync"/> returns an empty
		/// list when the participant has no conversations.
		/// </summary>
		[Fact]
		public async Task ListConversationsByParticipantAsync_WhenNoConversations_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act
			List<ConversationEntity> list = await service.ListConversationsByParticipantAsync(participant.Id);

			// Assert
			Assert.Empty(list);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.ListConversationsByParticipantAsync"/> validates the participant
		/// id and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task
			ListConversationsByParticipantAsync_WhenParticipantIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.ListConversationsByParticipantAsync(participantId: new ParticipantId(0)));
			Assert.Equal("participantId.Value", ex.ParamName);
		}

		#endregion
	}
}
