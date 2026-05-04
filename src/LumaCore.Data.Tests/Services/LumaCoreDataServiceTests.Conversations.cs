// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

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
	[Trait("Category", "Services")]
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
			Assert.Null(reloaded.Description);
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
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

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

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.CreateConversationAsync"/> persists the optional
		/// <see cref="ConversationEntity.Description"/> when provided, including whitespace trimming.
		/// </summary>
		[Fact]
		public async Task CreateConversationAsync_WhenDescriptionProvided_PersistsDescription()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Described",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow,
				                                  description: "  A conversation about testing  ");

			ConversationEntity? reloaded = await Fixture.DbContext.Conversations
				                               .AsNoTracking()
				                               .FirstOrDefaultAsync(c => c.Id == conversation.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal("Described", reloaded.Title);
			Assert.Equal("A conversation about testing", reloaded.Description);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.CreateConversationAsync"/> rejects descriptions that exceed
		/// <see cref="EntityLimits.ConversationDescriptionMaxLength"/>.
		/// </summary>
		[Fact]
		public async Task CreateConversationAsync_WhenDescriptionTooLong_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.CreateConversationAsync(
					         title: "Valid",
					         creatorParticipantId: new ParticipantId(1),
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
					         description: new string('x', 1001)));
			Assert.Equal("description", ex.ParamName);
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
		/// We provoke a foreign key violation by using a non-existing <see cref="ConversationId"/> while keeping the
		/// <see cref="ParticipantId"/> valid. This isolates the failure to a single FK so the test name's promise
		/// ("ForeignKeyViolation") is unambiguous. The implementation catches <see cref="DbUpdateException"/> and only
		/// returns <c>false</c> if the join already exists; otherwise it must rethrow.
		/// </remarks>
		[Fact]
		public async Task AddParticipantToConversationAsync_WhenForeignKeyViolation_RethrowsDbUpdateException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Special: use a real participant so only the conversation FK is missing — pinpoints the failure cause.
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act + Assert
			await Assert.ThrowsAsync<DbUpdateException>(() =>
				service.AddParticipantToConversationAsync(
					conversationId: new ConversationId(99999),
					participantId: participant.Id,
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
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
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

		#region UpdateConversationAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationAsync"/> updates the stored title and
		/// sets <see cref="ConversationEntity.UpdatedAtUtc"/> when the conversation exists.
		/// </summary>
		/// <remarks>
		/// The implementation uses a set-based update; therefore, the test reloads the entity with
		/// <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}(IQueryable{TEntity})"/> to validate persisted
		/// values.
		/// </remarks>
		[Fact]
		public async Task UpdateConversationAsync_WhenConversationExists_UpdatesTitleAndUpdatedAtUtc()
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
			bool updated = await service.UpdateConversationAsync(
				               conversationId: conversation.Id,
				               title: "Renamed",
				               description: null,
				               utcNow: updateUtcNow);

			// Assert
			Assert.True(updated);

			// Special: UpdateConversationAsync() uses ExecuteUpdateAsync() (set-based update), so we reload.
			ConversationEntity? reloaded = await Fixture.DbContext.Conversations
				                               .AsNoTracking()
				                               .FirstOrDefaultAsync(c => c.Id == conversation.Id);

			Assert.NotNull(reloaded);
			Assert.Equal("Renamed", reloaded.Title);
			Assert.Equal(updateUtcNow, reloaded.UpdatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationAsync"/> trims leading and
		/// trailing whitespace from the new title before persisting.
		/// </summary>
		[Fact]
		public async Task UpdateConversationAsync_WhenTitleHasWhitespace_TrimsTitle()
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
			bool updated = await service.UpdateConversationAsync(
				               conversationId: conversation.Id,
				               title: "  Renamed  ",
				               description: null,
				               utcNow: utcNow.AddMinutes(5));

			// Special: UpdateConversationAsync() trims the title via Guard.ThrowIfNullOrEmptyOrTooLong().
			ConversationEntity? reloaded = await Fixture.DbContext.Conversations
				                               .AsNoTracking()
				                               .FirstOrDefaultAsync(c => c.Id == conversation.Id);

			// Assert
			Assert.True(updated);
			Assert.NotNull(reloaded);
			Assert.Equal("Renamed", reloaded.Title);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationAsync"/> returns <c>false</c> when the
		/// conversation id does not exist.
		/// </summary>
		[Fact]
		public async Task UpdateConversationAsync_WhenConversationDoesNotExist_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool updated = await service.UpdateConversationAsync(
				               conversationId: new ConversationId(12345),
				               title: "Renamed",
				               description: null,
				               utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Assert
			Assert.False(updated);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationAsync"/> validates the conversation id
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task UpdateConversationAsync_WhenConversationIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.UpdateConversationAsync(
					         conversationId: new ConversationId(0),
					         title: "Renamed",
					         description: null,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("conversationId.Value", ex.ParamName);
		}

		/// <summary>
		/// Test data for <see cref="UpdateConversationAsync_WhenTitleInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid title that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string> UpdateConversationAsync_InvalidTitle_Data => new()
		{
			// Whitespace-only title
			{ "Whitespace title", "   " },

			// Title exceeds the 200-character maximum
			{ "Title too long", new string('x', 201) }
		};

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationAsync"/> rejects invalid titles with
		/// an <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="title">The title to pass to the method.</param>
		[Theory]
		[MemberData(nameof(UpdateConversationAsync_InvalidTitle_Data))]
		public async Task UpdateConversationAsync_WhenTitleInvalid_ThrowsArgumentException(
			string scenario,
			string title)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.UpdateConversationAsync(
					         conversationId: new ConversationId(1),
					         title: title,
					         description: null,
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

		#region RemoveParticipantFromConversationAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.RemoveParticipantFromConversationAsync"/> removes the join
		/// row when the participant is a member and returns <see langword="true"/>, while leaving other members untouched.
		/// </summary>
		[Fact]
		public async Task RemoveParticipantFromConversationAsync_WhenParticipantIsMember_ReturnsTrueAndRemovesJoinRow()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "owner@example.test", utcNow);
			ParticipantEntity member = await CreateUserParticipantAsync("member", "member@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync("Conv", owner.Id, utcNow);
			bool added = await service.AddParticipantToConversationAsync(
				             conversation.Id,
				             member.Id,
				             ConversationParticipantRole.Member,
				             utcNow.AddMinutes(1));
			Assert.True(added);

			// Act
			bool removed = await service.RemoveParticipantFromConversationAsync(conversation.Id, member.Id);

			// Assert
			Assert.True(removed);

			bool memberStillJoined = await Fixture.DbContext.ConversationParticipants
				                         .AsNoTracking()
				                         .AnyAsync(cp => cp.ConversationId == conversation.Id &&
				                                         cp.ParticipantId == member.Id);
			Assert.False(memberStillJoined);

			// Special: verify the owner's join row is untouched (set-based delete must be scoped correctly).
			bool ownerStillJoined = await Fixture.DbContext.ConversationParticipants
				                        .AsNoTracking()
				                        .AnyAsync(cp => cp.ConversationId == conversation.Id &&
				                                        cp.ParticipantId == owner.Id);
			Assert.True(ownerStillJoined);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.RemoveParticipantFromConversationAsync"/> returns
		/// <see langword="false"/> when the participant is not a member of the conversation, leaving the existing
		/// membership intact.
		/// </summary>
		[Fact]
		public async Task RemoveParticipantFromConversationAsync_WhenParticipantNotMember_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "owner@example.test", utcNow);
			ParticipantEntity outsider = await CreateUserParticipantAsync("outsider", "outsider@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync("Conv", owner.Id, utcNow);

			// Act
			bool removed = await service.RemoveParticipantFromConversationAsync(conversation.Id, outsider.Id);

			// Assert
			Assert.False(removed);

			bool ownerStillJoined = await Fixture.DbContext.ConversationParticipants
				                        .AsNoTracking()
				                        .AnyAsync(cp => cp.ConversationId == conversation.Id &&
				                                        cp.ParticipantId == owner.Id);
			Assert.True(ownerStillJoined);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.RemoveParticipantFromConversationAsync"/> returns
		/// <see langword="false"/> for a non-existent conversation
		/// </summary>
		[Fact]
		public async Task RemoveParticipantFromConversationAsync_WhenConversationDoesNotExist_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Special: use a real participant so the test isolates the "conversation does not exist" branch
			// (otherwise both FKs would be missing simultaneously and the test name would be misleading).
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Act
			bool removed = await service.RemoveParticipantFromConversationAsync(
				               conversationId: new ConversationId(99999),
				               participantId: participant.Id);

			// Assert
			Assert.False(removed);
		}

		/// <summary>
		/// Test data for <see cref="RemoveParticipantFromConversationAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// Each row provides an invalid id combination that triggers an <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, ConversationId, ParticipantId, string>
			RemoveParticipantFromConversationAsync_InvalidId_Data => new()
		{
			// Conversation id is zero
			{ "Zero conversationId", new ConversationId(0), new ParticipantId(1), "conversationId.Value" },

			// Participant id is zero
			{ "Zero participantId", new ConversationId(1), new ParticipantId(0), "participantId.Value" }
		};

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.RemoveParticipantFromConversationAsync"/> validates id
		/// parameters and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="conversationId">The conversation id to pass to the method.</param>
		/// <param name="participantId">The participant id to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(RemoveParticipantFromConversationAsync_InvalidId_Data))]
		public async Task RemoveParticipantFromConversationAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException(
			string         scenario,
			ConversationId conversationId,
			ParticipantId  participantId,
			string         expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.RemoveParticipantFromConversationAsync(conversationId, participantId));
			Assert.Equal(expectedParamName, ex.ParamName);
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

		#region GetConversationParticipantsAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetConversationParticipantsAsync"/> returns all participants
		/// with their <see cref="ParticipantEntity"/> and <see cref="PersonaEntity"/> navigation properties eagerly loaded.
		/// </summary>
		/// <remarks>
		/// Seeds a conversation with both a user participant (owner) and a persona participant (member).
		/// Asserts that every returned <see cref="ConversationParticipantEntity"/> has a non-null
		/// <see cref="ConversationParticipantEntity.Participant"/>, and that the persona participant's
		/// <see cref="ParticipantEntity.Persona"/> navigation is loaded while the user participant's is
		/// <see langword="null"/>.
		/// </remarks>
		[Fact]
		public async Task GetConversationParticipantsAsync_WhenParticipantsExist_ReturnsWithLoadedNavigationProperties()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "NavProps",
				                                  creatorParticipantId: owner.Id,
				                                  utcNow: utcNow);

			// Seed a persona participant so we can verify Persona navigation loading.
			var personaParticipant = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Bot",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(personaParticipant);
			await Fixture.DbContext.SaveChangesAsync();

			var persona = new PersonaEntity
			{
				ParticipantId = personaParticipant.Id,
				IsActive = true,
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Set<PersonaEntity>().Add(persona);
			await Fixture.DbContext.SaveChangesAsync();

			bool added = await service.AddParticipantToConversationAsync(
				             conversationId: conversation.Id,
				             participantId: personaParticipant.Id,
				             role: ConversationParticipantRole.Member,
				             utcNow: utcNow.AddMinutes(1));
			Assert.True(added);

			// Act
			IReadOnlyList<ConversationParticipantEntity> result =
				await service.GetConversationParticipantsAsync(conversation.Id);

			// Assert
			Assert.Equal(2, result.Count);

			// Both entries must have the Participant navigation loaded.
			Assert.All(result, cp => Assert.NotNull(cp.Participant));

			ConversationParticipantEntity userCp = Assert.Single(result, cp => cp.ParticipantId == owner.Id);
			Assert.Null(userCp.Participant!.Persona);

			ConversationParticipantEntity personaCp = Assert.Single(
				result,
				cp => cp.ParticipantId == personaParticipant.Id);
			Assert.NotNull(personaCp.Participant!.Persona);
			Assert.Equal(persona.Id, personaCp.Participant.Persona!.Id);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetConversationParticipantsAsync"/> returns an empty list
		/// when the conversation has no join rows.
		/// </summary>
		[Fact]
		public async Task GetConversationParticipantsAsync_WhenNoParticipants_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Seed a conversation directly (bypassing CreateConversationAsync which auto-joins the creator).
			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "Empty",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			IReadOnlyList<ConversationParticipantEntity> result =
				await service.GetConversationParticipantsAsync(conversation.Id);

			// Assert
			Assert.Empty(result);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetConversationParticipantsAsync"/> validates the
		/// conversation id and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task GetConversationParticipantsAsync_WhenConversationIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.GetConversationParticipantsAsync(new ConversationId(0)));
			Assert.Equal("conversationId.Value", ex.ParamName);
		}

		#endregion

		#region GetOwnedPersonaParticipantsInConversationAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetOwnedPersonaParticipantsInConversationAsync"/>
		/// returns only personas in the conversation whose <see cref="PersonaEntity.CreatedByParticipantId"/>
		/// matches the owner — excluding personas owned by other users and system-created personas.
		/// </summary>
		[Fact]
		public async Task
			GetOwnedPersonaParticipantsInConversationAsync_WhenMixedOwnership_ReturnsOnlyOwnedPersonas()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "owner@example.test", utcNow);
			ParticipantEntity otherUser = await CreateUserParticipantAsync("other", "other@example.test", utcNow);

			// Special: persona created by `owner` — must be returned.
			ParticipantEntity ownedPersona = await CreatePersonaParticipantAsync(
				                                 "OwnedPersona",
				                                 utcNow,
				                                 createdByParticipantId: owner.Id);

			// Special: persona created by another user — must NOT be returned.
			ParticipantEntity otherUsersPersona = await CreatePersonaParticipantAsync(
				                                      "OthersPersona",
				                                      utcNow,
				                                      createdByParticipantId: otherUser.Id);

			// Special: system-created persona (CreatedByParticipantId = null) — must NOT be returned.
			ParticipantEntity systemPersona = await CreatePersonaParticipantAsync(
				                                  "SystemPersona",
				                                  utcNow,
				                                  createdByParticipantId: null);

			ConversationEntity conversation = await service.CreateConversationAsync("Conv", owner.Id, utcNow);
			foreach (ParticipantId pid in new[] { ownedPersona.Id, otherUsersPersona.Id, systemPersona.Id })
			{
				bool added = await service.AddParticipantToConversationAsync(
					             conversation.Id,
					             pid,
					             ConversationParticipantRole.Member,
					             utcNow.AddMinutes(1));
				Assert.True(added);
			}

			// Act
			IReadOnlyList<ParticipantEntity> owned = await service
				                                         .GetOwnedPersonaParticipantsInConversationAsync(
					                                         conversation.Id,
					                                         owner.Id);

			// Assert
			Assert.Single(owned);
			Assert.Equal(ownedPersona.Id, owned[0].Id);
			Assert.Equal("OwnedPersona", owned[0].DisplayName);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetOwnedPersonaParticipantsInConversationAsync"/>
		/// returns an empty list when the owner has personas in the system but none of them are members of the
		/// specified conversation.
		/// </summary>
		[Fact]
		public async Task
			GetOwnedPersonaParticipantsInConversationAsync_WhenOwnerHasNoPersonasInConversation_ReturnsEmpty()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "owner@example.test", utcNow);

			// Owner has a persona, but it is not added to the conversation under test.
			_ = await CreatePersonaParticipantAsync("Unrelated", utcNow, createdByParticipantId: owner.Id);

			ConversationEntity conversation = await service.CreateConversationAsync("Conv", owner.Id, utcNow);

			// Act
			IReadOnlyList<ParticipantEntity> owned = await service
				                                         .GetOwnedPersonaParticipantsInConversationAsync(
					                                         conversation.Id,
					                                         owner.Id);

			// Assert
			Assert.Empty(owned);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetOwnedPersonaParticipantsInConversationAsync"/>
		/// returns an empty list when the conversation contains no personas at all.
		/// </summary>
		[Fact]
		public async Task
			GetOwnedPersonaParticipantsInConversationAsync_WhenConversationHasNoPersonas_ReturnsEmpty()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "owner@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync("Conv", owner.Id, utcNow);

			// Act
			IReadOnlyList<ParticipantEntity> owned = await service
				                                         .GetOwnedPersonaParticipantsInConversationAsync(
					                                         conversation.Id,
					                                         owner.Id);

			// Assert
			Assert.Empty(owned);
		}

		/// <summary>
		/// Test data for
		/// <see cref="GetOwnedPersonaParticipantsInConversationAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, ConversationId, ParticipantId, string>
			GetOwnedPersonaParticipantsInConversationAsync_InvalidId_Data => new()
		{
			// Conversation id is zero
			{ "Zero conversationId", new ConversationId(0), new ParticipantId(1), "conversationId.Value" },

			// Owner participant id is zero
			{ "Zero ownerParticipantId", new ConversationId(1), new ParticipantId(0), "ownerParticipantId.Value" }
		};

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetOwnedPersonaParticipantsInConversationAsync"/>
		/// validates id parameters and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="conversationId">The conversation id to pass to the method.</param>
		/// <param name="ownerParticipantId">The owner participant id to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(GetOwnedPersonaParticipantsInConversationAsync_InvalidId_Data))]
		public async Task
			GetOwnedPersonaParticipantsInConversationAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException(
				string         scenario,
				ConversationId conversationId,
				ParticipantId  ownerParticipantId,
				string         expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.GetOwnedPersonaParticipantsInConversationAsync(conversationId, ownerParticipantId));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion

		#region GetParticipantCountsAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetParticipantCountsAsync"/> returns the correct participant
		/// count for each conversation in the input list.
		/// </summary>
		/// <remarks>
		/// Seeds two conversations — one with a single participant (the owner) and one with two participants — then
		/// asserts both counts in the returned dictionary.
		/// </remarks>
		[Fact]
		public async Task GetParticipantCountsAsync_WhenConversationsHaveParticipants_ReturnsCorrectCounts()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "owner@example.test", utcNow);
			ParticipantEntity member = await CreateUserParticipantAsync("member", "member@example.test", utcNow);

			ConversationEntity single = await service.CreateConversationAsync(
				                            title: "Single",
				                            creatorParticipantId: owner.Id,
				                            utcNow: utcNow);

			ConversationEntity duo = await service.CreateConversationAsync(
				                         title: "Duo",
				                         creatorParticipantId: owner.Id,
				                         utcNow: utcNow);

			bool added = await service.AddParticipantToConversationAsync(
				             conversationId: duo.Id,
				             participantId: member.Id,
				             role: ConversationParticipantRole.Member,
				             utcNow: utcNow.AddMinutes(1));
			Assert.True(added);

			// Act
			IReadOnlyDictionary<ConversationId, int> counts =
				await service.GetParticipantCountsAsync([single.Id, duo.Id]);

			// Assert
			Assert.Equal(2, counts.Count);
			Assert.Equal(1, counts[single.Id]);
			Assert.Equal(2, counts[duo.Id]);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetParticipantCountsAsync"/> returns an empty dictionary
		/// when the input list is empty (short-circuit path).
		/// </summary>
		[Fact]
		public async Task GetParticipantCountsAsync_WhenListIsEmpty_ReturnsEmptyDictionary()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			IReadOnlyDictionary<ConversationId, int> counts = await service.GetParticipantCountsAsync([]);

			// Assert
			Assert.Empty(counts);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetParticipantCountsAsync"/> omits conversations that have
		/// no join rows from the returned dictionary rather than returning a zero count.
		/// </summary>
		[Fact]
		public async Task GetParticipantCountsAsync_WhenConversationHasNoParticipants_OmitsFromDictionary()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Seed a conversation directly so it has no join rows.
			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "NoMembers",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			IReadOnlyDictionary<ConversationId, int> counts =
				await service.GetParticipantCountsAsync([conversation.Id]);

			// Assert
			Assert.Empty(counts);
		}

		#endregion

		#region GetPersonaParticipantsAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetPersonaParticipantsAsync"/> returns persona
		/// participants ordered by ascending <see cref="ParticipantId"/>, excluding non-persona members.
		/// </summary>
		[Fact]
		public async Task GetPersonaParticipantsAsync_WhenPersonasAndUsersPresent_ReturnsOnlyPersonasOrderedById()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity user = await CreateUserParticipantAsync("user", "u@example.test", utcNow);
			ParticipantEntity persona1 = await CreatePersonaParticipantAsync("P1", utcNow);
			ParticipantEntity persona2 = await CreatePersonaParticipantAsync("P2", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync("Conv", user.Id, utcNow);

			// Special: insert in reverse order to verify ordering is by ParticipantId, not insertion order.
			foreach (ParticipantId pid in new[] { persona2.Id, persona1.Id })
			{
				bool added = await service.AddParticipantToConversationAsync(
					             conversation.Id,
					             pid,
					             ConversationParticipantRole.Member,
					             utcNow.AddMinutes(1));
				Assert.True(added);
			}

			// Act
			IReadOnlyList<ParticipantEntity> personas = await service.GetPersonaParticipantsAsync(conversation.Id);

			// Assert
			Assert.Equal(2, personas.Count);
			Assert.Equal(persona1.Id, personas[0].Id);
			Assert.Equal(persona2.Id, personas[1].Id);
			Assert.DoesNotContain(personas, p => p.Id == user.Id);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetPersonaParticipantsAsync"/> returns an empty list
		/// when the conversation contains only user participants.
		/// </summary>
		[Fact]
		public async Task GetPersonaParticipantsAsync_WhenOnlyUserParticipants_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity user = await CreateUserParticipantAsync("user", "u@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync("Conv", user.Id, utcNow);

			// Act
			IReadOnlyList<ParticipantEntity> personas = await service.GetPersonaParticipantsAsync(conversation.Id);

			// Assert
			Assert.Empty(personas);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetPersonaParticipantsAsync"/> returns an empty list
		/// when the conversation has no participants at all.
		/// </summary>
		[Fact]
		public async Task GetPersonaParticipantsAsync_WhenConversationHasNoParticipants_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Special: insert a conversation directly to bypass CreateConversationAsync, which would auto-add
			// the creator as a participant.
			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "Empty",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			IReadOnlyList<ParticipantEntity> personas = await service.GetPersonaParticipantsAsync(conversation.Id);

			// Assert
			Assert.Empty(personas);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.GetPersonaParticipantsAsync"/> validates the
		/// conversation id and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task GetPersonaParticipantsAsync_WhenConversationIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.GetPersonaParticipantsAsync(new ConversationId(0)));
			Assert.Equal("conversationId.Value", ex.ParamName);
		}

		#endregion

		#region HasUserParticipantsAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.HasUserParticipantsAsync"/> returns
		/// <see langword="true"/> when at least one participant has a corresponding row in the <c>Users</c> table.
		/// </summary>
		[Fact]
		public async Task HasUserParticipantsAsync_WhenUserParticipantPresent_ReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity user = await CreateUserParticipantAsync("user", "u@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync("Conv", user.Id, utcNow);

			// Act
			bool hasUsers = await service.HasUserParticipantsAsync(conversation.Id);

			// Assert
			Assert.True(hasUsers);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.HasUserParticipantsAsync"/> returns
		/// <see langword="false"/> when the conversation contains only persona participants.
		/// </summary>
		[Fact]
		public async Task HasUserParticipantsAsync_WhenOnlyPersonaParticipants_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Special: a user is required to create the conversation, but is removed afterwards so only the
			// persona remains — exactly the post-leave state HasUserParticipantsAsync is meant to detect.
			ParticipantEntity user = await CreateUserParticipantAsync("user", "u@example.test", utcNow);
			ParticipantEntity persona = await CreatePersonaParticipantAsync("P", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync("Conv", user.Id, utcNow);
			bool added = await service.AddParticipantToConversationAsync(
				             conversation.Id,
				             persona.Id,
				             ConversationParticipantRole.Member,
				             utcNow.AddMinutes(1));
			Assert.True(added);

			bool removed = await service.RemoveParticipantFromConversationAsync(conversation.Id, user.Id);
			Assert.True(removed);

			// Act
			bool hasUsers = await service.HasUserParticipantsAsync(conversation.Id);

			// Assert
			Assert.False(hasUsers);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.HasUserParticipantsAsync"/> returns
		/// <see langword="false"/> when the conversation has no participants at all.
		/// </summary>
		[Fact]
		public async Task HasUserParticipantsAsync_WhenConversationHasNoParticipants_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "Empty",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			bool hasUsers = await service.HasUserParticipantsAsync(conversation.Id);

			// Assert
			Assert.False(hasUsers);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.HasUserParticipantsAsync"/> validates the
		/// conversation id and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task HasUserParticipantsAsync_WhenConversationIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.HasUserParticipantsAsync(new ConversationId(0)));
			Assert.Equal("conversationId.Value", ex.ParamName);
		}

		#endregion

		#region IsParticipantInConversationAsync

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.IsParticipantInConversationAsync"/> returns
		/// <see langword="true"/> when the participant is a member of the conversation.
		/// </summary>
		[Fact]
		public async Task IsParticipantInConversationAsync_WhenParticipantIsMember_ReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity user = await CreateUserParticipantAsync("user", "u@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync("Conv", user.Id, utcNow);

			// Act
			bool isMember = await service.IsParticipantInConversationAsync(conversation.Id, user.Id);

			// Assert
			Assert.True(isMember);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.IsParticipantInConversationAsync"/> returns
		/// <see langword="false"/> when the participant is not a member of the conversation.
		/// </summary>
		[Fact]
		public async Task IsParticipantInConversationAsync_WhenParticipantNotMember_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "o@example.test", utcNow);
			ParticipantEntity outsider = await CreateUserParticipantAsync("outsider", "out@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync("Conv", owner.Id, utcNow);

			// Act
			bool isMember = await service.IsParticipantInConversationAsync(conversation.Id, outsider.Id);

			// Assert
			Assert.False(isMember);
		}

		/// <summary>
		/// Verifies that
		/// <see cref="LumaCoreDataService.IsParticipantInConversationAsync(ConversationId, ParticipantId, CancellationToken)"/>
		/// returns <see langword="true"/> via the compiled-query hot path when enabled.
		/// </summary>
		[Fact]
		public async Task IsParticipantInConversationAsync_WhenPreferCompiledHotPathQueriesEnabled_ReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = true);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity user = await CreateUserParticipantAsync("user", "u@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync("Conv", user.Id, utcNow);

			// Act
			bool isMember = await service.IsParticipantInConversationAsync(conversation.Id, user.Id);

			// Assert
			Assert.True(isMember);
		}

		/// <summary>
		/// Verifies that
		/// <see cref="LumaCoreDataService.IsParticipantInConversationAsync(ConversationId, ParticipantId, CancellationToken)"/>
		/// returns <see langword="false"/> via the compiled-query hot path when the participant is not a member.
		/// </summary>
		[Fact]
		public async Task
			IsParticipantInConversationAsync_WhenPreferCompiledHotPathQueriesEnabled_AndNotMember_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = true);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity owner = await CreateUserParticipantAsync("owner", "o@example.test", utcNow);
			ParticipantEntity outsider = await CreateUserParticipantAsync("outsider", "out@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync("Conv", owner.Id, utcNow);

			// Act
			bool isMember = await service.IsParticipantInConversationAsync(conversation.Id, outsider.Id);

			// Assert
			Assert.False(isMember);
		}

		/// <summary>
		/// Test data for <see cref="IsParticipantInConversationAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, ConversationId, ParticipantId, string>
			IsParticipantInConversationAsync_InvalidId_Data => new()
		{
			// Conversation id is zero
			{ "Zero conversationId", new ConversationId(0), new ParticipantId(1), "conversationId.Value" },

			// Participant id is zero
			{ "Zero participantId", new ConversationId(1), new ParticipantId(0), "participantId.Value" }
		};

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.IsParticipantInConversationAsync"/> validates id
		/// parameters and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="conversationId">The conversation id to pass to the method.</param>
		/// <param name="participantId">The participant id to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(IsParticipantInConversationAsync_InvalidId_Data))]
		public async Task IsParticipantInConversationAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException(
			string         scenario,
			ConversationId conversationId,
			ParticipantId  participantId,
			string         expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.IsParticipantInConversationAsync(conversationId, participantId));
			Assert.Equal(expectedParamName, ex.ParamName);
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
			await service.UpdateConversationAsync(older.Id, "Old2", null, utcNow.AddMinutes(2));
			await service.UpdateConversationAsync(newer.Id, "New2", null, utcNow.AddMinutes(3));

			// Act
			IReadOnlyList<ConversationEntity> list = await service.ListConversationsByParticipantAsync(participant.Id);

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
			IReadOnlyList<ConversationEntity> list = await service.ListConversationsByParticipantAsync(member.Id);

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
			IReadOnlyList<ConversationEntity> list = await service.ListConversationsByParticipantAsync(participant.Id);

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

		#region UTC clock fallback

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.AddParticipantToConversationAsync"/> falls back to the
		/// injected <see cref="TimeProvider"/> for <see cref="ConversationParticipantEntity.JoinedAtUtc"/> when the
		/// optional <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c> and silently
		/// captures wall-clock time instead.
		/// </remarks>
		[Fact]
		public async Task AddParticipantToConversationAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			DateTime seedNow = fixedNow.AddDays(-1);
			ParticipantEntity owner = await CreateUserParticipantAsync("alice", "alice@example.test", seedNow);
			ParticipantEntity persona = await CreatePersonaParticipantAsync("Bot", seedNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: owner.Id,
				                                  utcNow: seedNow);

			// Act
			bool added = await service.AddParticipantToConversationAsync(
				             conversation.Id,
				             persona.Id,
				             ConversationParticipantRole.Member);

			// Assert
			Assert.True(added);
			ConversationParticipantEntity? join = await Fixture.DbContext.ConversationParticipants
				                                      .AsNoTracking()
				                                      .FirstOrDefaultAsync(cp => cp.ConversationId == conversation.Id &&
				                                                                 cp.ParticipantId == persona.Id);
			Assert.NotNull(join);
			Assert.Equal(fixedNow, join.JoinedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.CreateConversationAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for both <see cref="ConversationEntity.CreatedAtUtc"/> and
		/// <see cref="ConversationEntity.UpdatedAtUtc"/> when the optional <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c>.
		/// </remarks>
		[Fact]
		public async Task CreateConversationAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			ParticipantEntity owner = await CreateUserParticipantAsync(
				                          "alice",
				                          "alice@example.test",
				                          fixedNow.AddDays(-1));

			// Act
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: owner.Id);

			// Assert
			Assert.Equal(fixedNow, conversation.CreatedAtUtc);
			Assert.Equal(fixedNow, conversation.UpdatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IConversationDataService.UpdateConversationAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for <see cref="ConversationEntity.UpdatedAtUtc"/> when the optional
		/// <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c>.
		/// </remarks>
		[Fact]
		public async Task UpdateConversationAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			DateTime seedNow = fixedNow.AddDays(-1);
			ParticipantEntity owner = await CreateUserParticipantAsync("alice", "alice@example.test", seedNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: owner.Id,
				                                  utcNow: seedNow);

			// Act
			bool updated = await service.UpdateConversationAsync(
				               conversation.Id,
				               title: "Updated",
				               description: null);

			// Assert
			Assert.True(updated);
			ConversationEntity? reloaded = await Fixture.DbContext.Conversations
				                               .AsNoTracking()
				                               .FirstOrDefaultAsync(c => c.Id == conversation.Id);
			Assert.NotNull(reloaded);
			Assert.Equal(fixedNow, reloaded.UpdatedAtUtc);
		}

		#endregion
	}
}
