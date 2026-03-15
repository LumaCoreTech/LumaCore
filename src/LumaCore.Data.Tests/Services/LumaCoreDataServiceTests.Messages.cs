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
	/// Tests for <see cref="IMessageDataService"/> methods.
	/// </summary>
	/// <remarks>
	/// These tests validate message creation, listing (including ordering/limit behavior), and redaction APIs.
	/// They focus on persisted behavior (what ends up stored in the database) and on domain guards
	/// (e.g. membership requirements) rather than relying on database-level FK failures.
	/// </remarks>
	[Trait("Category", "Data")]
	public sealed class Messages : TestBase
	{
		#region CreateMessageAsync

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageAsync"/> creates a <see cref="MessageEntity"/> when
		/// the sender is a member of the conversation and updates <see cref="ConversationEntity.UpdatedAtUtc"/>.
		/// </summary>
		/// <remarks>
		/// The conversation is created via <see cref="IConversationDataService.CreateConversationAsync"/>, which joins the
		/// creator as Owner, ensuring the sender is a valid member.
		/// </remarks>
		[Fact]
		public async Task CreateMessageAsync_WhenSenderIsMember_CreatesMessageAndUpdatesConversation()
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
			DateTime messageUtcNow = utcNow.AddMinutes(1);
			MessageEntity message = await service.CreateMessageAsync(
				                        conversationId: conversation.Id,
				                        senderParticipantId: participant.Id,
				                        content: "Hello",
				                        utcNow: messageUtcNow);

			// Assert
			Assert.True(message.Id.Value > 0);
			Assert.NotEqual(Guid.Empty, message.PublicId);
			Assert.Equal(conversation.Id, message.ConversationId);
			Assert.Equal(participant.Id, message.SenderId);
			Assert.Equal("Hello", message.Content);
			Assert.Equal(messageUtcNow, message.CreatedAtUtc);

			// Special: CreateMessageAsync() also updates Conversation.UpdatedAtUtc for ordering.
			ConversationEntity? reloadedConversation = await Fixture.DbContext.Conversations
				                                           .AsNoTracking()
				                                           .FirstOrDefaultAsync(c => c.Id == conversation.Id);

			Assert.NotNull(reloadedConversation);
			Assert.Equal(messageUtcNow, reloadedConversation.UpdatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageAsync"/> trims leading and trailing whitespace
		/// from the content before persisting.
		/// </summary>
		[Fact]
		public async Task CreateMessageAsync_WhenContentHasWhitespace_TrimsContent()
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
			MessageEntity message = await service.CreateMessageAsync(
				                        conversationId: conversation.Id,
				                        senderParticipantId: participant.Id,
				                        content: "  Hello  ",
				                        utcNow: utcNow.AddMinutes(1));

			// Special: CreateMessageAsync() trims content via content.Trim().
			MessageEntity? reloaded = await Fixture.DbContext.Messages
				                          .AsNoTracking()
				                          .FirstOrDefaultAsync(m => m.Id == message.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal("Hello", reloaded.Content);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageAsync"/> rejects senders that are not members of the
		/// conversation.
		/// </summary>
		/// <remarks>
		/// The test asserts a domain-level <see cref="InvalidOperationException"/> instead of relying on a FK violation.
		/// </remarks>
		[Fact]
		public async Task CreateMessageAsync_WhenSenderNotMember_ThrowsInvalidOperationException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity author = await CreateUserParticipantAsync("author", "author@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: author.Id,
				                                  utcNow: utcNow);

			var otherParticipant = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Other",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(otherParticipant);
			await Fixture.DbContext.SaveChangesAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
				         service.CreateMessageAsync(
					         conversationId: conversation.Id,
					         senderParticipantId: otherParticipant.Id,
					         content: "Hello",
					         utcNow: utcNow.AddMinutes(1)));
			Assert.Matches(@"^Sender participant '.+' is not part of conversation '.+'\.$", ex.Message);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageAsync"/> throws <see cref="ArgumentNullException"/>
		/// when <c>content</c> is <c>null</c>.
		/// </summary>
		[Fact]
		public async Task CreateMessageAsync_WhenContentNull_ThrowsArgumentNullException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
				         service.CreateMessageAsync(
					         conversationId: new ConversationId(1),
					         senderParticipantId: new ParticipantId(1),
					         content: null,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("content", ex.ParamName);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageAsync"/> throws for whitespace-only <c>content</c>.
		/// </summary>
		[Fact]
		public async Task CreateMessageAsync_WhenContentWhitespace_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.CreateMessageAsync(
					         conversationId: new ConversationId(1),
					         senderParticipantId: new ParticipantId(1),
					         content: "   ",
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("content", ex.ParamName);
		}

		/// <summary>
		/// Test data for <see cref="CreateMessageAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException"/>. Each row
		/// provides an invalid id combination that triggers an <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, ConversationId, ParticipantId, string>
			CreateMessageAsync_InvalidId_Data => new()
		{
			// Conversation id is zero
			{ "Zero conversationId", new ConversationId(0), new ParticipantId(1), "conversationId.Value" },

			// Sender participant id is zero
			{ "Zero senderParticipantId", new ConversationId(1), new ParticipantId(0), "senderParticipantId.Value" }
		};

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageAsync"/> validates id parameters and throws
		/// <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="conversationId">The conversation id to pass to the method.</param>
		/// <param name="senderParticipantId">The sender participant id to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentOutOfRangeException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(CreateMessageAsync_InvalidId_Data))]
		public async Task CreateMessageAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException(
			string         scenario,
			ConversationId conversationId,
			ParticipantId  senderParticipantId,
			string         expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.CreateMessageAsync(
					         conversationId: conversationId,
					         senderParticipantId: senderParticipantId,
					         content: "Hello",
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageAsync"/> throws <see cref="InvalidOperationException"/>
		/// when the referenced conversation does not exist.
		/// </summary>
		[Fact]
		public async Task CreateMessageAsync_WhenConversationDoesNotExist_ThrowsInvalidOperationException()
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

			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
				         service.CreateMessageAsync(
					         conversationId: new ConversationId(12345),
					         senderParticipantId: participant.Id,
					         content: "Hello",
					         utcNow: utcNow));
			Assert.Matches(@"^Conversation '.+' does not exist\.$", ex.Message);
		}

		/// <summary>
		/// Verifies the guard branch where <see cref="IMessageDataService.CreateMessageAsync"/> rejects a sender participant
		/// id that does not exist.
		/// </summary>
		[Fact]
		public async Task CreateMessageAsync_WhenSenderMissing_ThrowsInvalidOperationException()
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
			await Fixture.DbContext.SaveChangesAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
				         service.CreateMessageAsync(conversation.Id, new ParticipantId(123456), "Hello", utcNow));
			Assert.Matches(@"^Sender participant '.+' does not exist\.$", ex.Message);
		}

		#endregion

		#region ListMessagesByConversationAsync

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListMessagesByConversationAsync"/> returns messages ordered by
		/// ascending creation time.
		/// </summary>
		[Fact]
		public async Task ListMessagesByConversationAsync_WhenMultipleMessagesExist_ReturnsInAscendingCreatedAtOrder()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			MessageEntity m2 = await service.CreateMessageAsync(
				                   conversationId: conversation.Id,
				                   senderParticipantId: participant.Id,
				                   content: "Second",
				                   utcNow: utcNow.AddSeconds(2));

			MessageEntity m1 = await service.CreateMessageAsync(
				                   conversationId: conversation.Id,
				                   senderParticipantId: participant.Id,
				                   content: "First",
				                   utcNow: utcNow.AddSeconds(1));

			// Act
			List<MessageEntity> messages =
				await service.ListMessagesByConversationAsync(conversationId: conversation.Id);

			// Assert
			Assert.Equal(2, messages.Count);
			Assert.Equal(m1.Id, messages[0].Id);
			Assert.Equal(m2.Id, messages[1].Id);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListMessagesByConversationAsync"/> returns an empty list when
		/// the conversation has no messages.
		/// </summary>
		[Fact]
		public async Task ListMessagesByConversationAsync_WhenNoMessages_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Empty",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			// Act
			List<MessageEntity> messages = await service.ListMessagesByConversationAsync(conversation.Id);

			// Assert
			Assert.Empty(messages);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListMessagesByConversationAsync"/> validates the conversation id
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task ListMessagesByConversationAsync_WhenConversationIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.ListMessagesByConversationAsync(conversationId: new ConversationId(0)));
			Assert.Equal("conversationId.Value", ex.ParamName);
		}

		#endregion

		#region ListRecentMessagesByConversationAsync

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListRecentMessagesByConversationAsync"/> returns the most recent
		/// messages first and respects the provided limit.
		/// </summary>
		[Fact]
		public async Task ListRecentMessagesByConversationAsync_WhenMultipleMessagesExist_ReturnsMostRecentMessages()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			MessageEntity m1 = await service.CreateMessageAsync(
				                   conversationId: conversation.Id,
				                   senderParticipantId: participant.Id,
				                   content: "1",
				                   utcNow: utcNow.AddSeconds(1));

			MessageEntity m2 = await service.CreateMessageAsync(
				                   conversationId: conversation.Id,
				                   senderParticipantId: participant.Id,
				                   content: "2",
				                   utcNow: utcNow.AddSeconds(2));

			MessageEntity m3 = await service.CreateMessageAsync(
				                   conversationId: conversation.Id,
				                   senderParticipantId: participant.Id,
				                   content: "3",
				                   utcNow: utcNow.AddSeconds(3));

			// Act
			List<MessageEntity> recent = await service.ListRecentMessagesByConversationAsync(
				                             conversationId: conversation.Id,
				                             limit: 2);

			// Assert
			Assert.Equal(2, recent.Count);
			Assert.Equal(m3.Id, recent[0].Id);
			Assert.Equal(m2.Id, recent[1].Id);
			Assert.DoesNotContain(recent, m => m.Id == m1.Id);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListRecentMessagesByConversationAsync"/> returns an empty list
		/// when the conversation has no messages.
		/// </summary>
		[Fact]
		public async Task ListRecentMessagesByConversationAsync_WhenNoMessages_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Empty",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			// Act
			List<MessageEntity> recent =
				await service.ListRecentMessagesByConversationAsync(conversation.Id, limit: 10);

			// Assert
			Assert.Empty(recent);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListRecentMessagesByConversationAsync"/> validates the <c>limit</c>
		/// parameter and throws <see cref="ArgumentOutOfRangeException"/> for non-positive values.
		/// </summary>
		[Fact]
		public async Task ListRecentMessagesByConversationAsync_WhenLimitInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.ListRecentMessagesByConversationAsync(
					         conversationId: new ConversationId(1),
					         limit: 0));
			Assert.Equal("limit", ex.ParamName);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListRecentMessagesByConversationAsync"/> validates the conversation
		/// id and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task
			ListRecentMessagesByConversationAsync_WhenConversationIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.ListRecentMessagesByConversationAsync(
					         conversationId: new ConversationId(0),
					         limit: 1));
			Assert.Equal("conversationId.Value", ex.ParamName);
		}

		#endregion

		#region RedactMessageAsync

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessageAsync"/> performs a redaction (nulls content, sets the
		/// reason and timestamp) and returns <c>true</c> when the message exists.
		/// </summary>
		[Fact]
		public async Task RedactMessageAsync_WhenMessageExists_RedactsAndReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			MessageEntity message = await service.CreateMessageAsync(
				                        conversationId: conversation.Id,
				                        senderParticipantId: participant.Id,
				                        content: "Hello",
				                        utcNow: utcNow);

			// Act
			DateTime redactUtcNow = utcNow.AddMinutes(1);
			bool redacted = await service.RedactMessageAsync(
				                messageId: message.Id,
				                reason: MessageRedactionReason.UserRequestedDeletion,
				                utcNow: redactUtcNow);

			// Assert
			Assert.True(redacted);

			MessageEntity? reloaded = await Fixture.DbContext.Messages
				                          .AsNoTracking()
				                          .FirstOrDefaultAsync(m => m.Id == message.Id);

			Assert.NotNull(reloaded);
			Assert.Null(reloaded.Content);
			Assert.Equal(MessageRedactionReason.UserRequestedDeletion, reloaded.RedactionReason);
			Assert.Equal(redactUtcNow, reloaded.RedactedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessageAsync"/> returns <c>false</c> when the message id does
		/// not exist.
		/// </summary>
		[Fact]
		public async Task RedactMessageAsync_WhenMessageNotFound_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool redacted = await service.RedactMessageAsync(
				                messageId: new MessageId(12345),
				                reason: MessageRedactionReason.Other,
				                utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Assert
			Assert.False(redacted);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessageAsync"/> validates the message id and throws
		/// <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task RedactMessageAsync_WhenMessageIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.RedactMessageAsync(
					         messageId: new MessageId(0),
					         reason: MessageRedactionReason.Other,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("messageId.Value", ex.ParamName);
		}

		#endregion

		#region RedactMessageByAuthorAsync

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessageByAuthorAsync"/> redacts the message and returns
		/// <c>true</c> when the author matches and the message is not already redacted.
		/// </summary>
		[Fact]
		public async Task RedactMessageByAuthorAsync_WhenAuthorMatches_RedactsAndReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity author = await CreateUserParticipantAsync("author", "author@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: author.Id,
				                                  utcNow: utcNow);

			MessageEntity message = await service.CreateMessageAsync(
				                        conversationId: conversation.Id,
				                        senderParticipantId: author.Id,
				                        content: "Hello",
				                        utcNow: utcNow);

			// Act
			DateTime redactUtcNow = utcNow.AddMinutes(1);
			bool ok = await service.RedactMessageByAuthorAsync(
				          messageId: message.Id,
				          authorParticipantId: author.Id,
				          utcNow: redactUtcNow);

			// Assert
			Assert.True(ok);

			MessageEntity? messageAfter = await Fixture.DbContext.Messages
				                              .AsNoTracking()
				                              .FirstOrDefaultAsync(m => m.Id == message.Id);

			Assert.NotNull(messageAfter);
			Assert.Null(messageAfter.Content);
			Assert.Equal(MessageRedactionReason.UserRequestedDeletion, messageAfter.RedactionReason);
			Assert.Equal(redactUtcNow, messageAfter.RedactedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessageByAuthorAsync"/> returns <c>false</c> when the provided
		/// author participant does not match the sender of the message.
		/// </summary>
		[Fact]
		public async Task RedactMessageByAuthorAsync_WhenAuthorMismatch_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity author = await CreateUserParticipantAsync("author", "author@example.test", utcNow);
			ParticipantEntity other = await CreateUserParticipantAsync("other", "other@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: author.Id,
				                                  utcNow: utcNow);

			MessageEntity message = await service.CreateMessageAsync(
				                        conversationId: conversation.Id,
				                        senderParticipantId: author.Id,
				                        content: "Hello",
				                        utcNow: utcNow);

			// Act
			bool redacted = await service.RedactMessageByAuthorAsync(
				                messageId: message.Id,
				                authorParticipantId: other.Id,
				                utcNow: utcNow.AddMinutes(1));

			// Assert
			Assert.False(redacted);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessageByAuthorAsync"/> returns <c>false</c> when the message
		/// is already redacted.
		/// </summary>
		[Fact]
		public async Task RedactMessageByAuthorAsync_WhenAlreadyRedacted_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity author = await CreateUserParticipantAsync("author", "author@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: author.Id,
				                                  utcNow: utcNow);

			MessageEntity message = await service.CreateMessageAsync(
				                        conversationId: conversation.Id,
				                        senderParticipantId: author.Id,
				                        content: "Hello",
				                        utcNow: utcNow);

			DateTime redactUtcNow = utcNow.AddMinutes(1);
			await service.RedactMessageAsync(message.Id, MessageRedactionReason.Other, redactUtcNow);

			// Act
			bool redacted2 = await service.RedactMessageByAuthorAsync(
				                 messageId: message.Id,
				                 authorParticipantId: author.Id,
				                 utcNow: redactUtcNow.AddMinutes(1));

			// Assert
			Assert.False(redacted2);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessageByAuthorAsync"/> uses a "soft" contract for missing
		/// messages and returns <c>false</c> instead of throwing.
		/// </summary>
		[Fact]
		public async Task RedactMessageByAuthorAsync_WhenMessageNotFound_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool ok = await service.RedactMessageByAuthorAsync(
				          new MessageId(12345),
				          new ParticipantId(1),
				          DateTime.UtcNow);

			// Assert
			Assert.False(ok);
		}

		/// <summary>
		/// Test data for <see cref="RedactMessageByAuthorAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// Each row provides an invalid id combination that triggers an <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, MessageId, ParticipantId, string>
			RedactMessageByAuthorAsync_InvalidId_Data => new()
		{
			// Message id is zero
			{ "Zero messageId", new MessageId(0), new ParticipantId(1), "messageId.Value" },

			// Author participant id is zero
			{ "Zero authorParticipantId", new MessageId(1), new ParticipantId(0), "authorParticipantId.Value" }
		};

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessageByAuthorAsync"/> validates ids and throws
		/// <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="messageId">The message id to pass to the method.</param>
		/// <param name="authorParticipantId">The author participant id to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentOutOfRangeException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(RedactMessageByAuthorAsync_InvalidId_Data))]
		public async Task RedactMessageByAuthorAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException(
			string        scenario,
			MessageId     messageId,
			ParticipantId authorParticipantId,
			string        expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.RedactMessageByAuthorAsync(
					         messageId: messageId,
					         authorParticipantId: authorParticipantId,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion

		#region RedactMessagesByParticipantAsync

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessagesByParticipantAsync"/> returns 0 when the participant
		/// has no messages.
		/// </summary>
		[Fact]
		public async Task RedactMessagesByParticipantAsync_WhenNoMessages_ReturnsZero()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			int redacted = await service.RedactMessagesByParticipantAsync(
				               participantId: new ParticipantId(12345),
				               reason: MessageRedactionReason.Moderation,
				               utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			// Assert
			Assert.Equal(0, redacted);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessagesByParticipantAsync"/> redacts all messages created by
		/// the specified participant and returns the number of affected messages.
		/// </summary>
		[Fact]
		public async Task RedactMessagesByParticipantAsync_WhenMessagesExist_RedactsAllAndReturnsCount()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			MessageEntity m1 = await service.CreateMessageAsync(
				                   conversationId: conversation.Id,
				                   senderParticipantId: participant.Id,
				                   content: "A",
				                   utcNow: utcNow);

			MessageEntity m2 = await service.CreateMessageAsync(
				                   conversationId: conversation.Id,
				                   senderParticipantId: participant.Id,
				                   content: "B",
				                   utcNow: utcNow.AddSeconds(1));

			// Act
			int redacted = await service.RedactMessagesByParticipantAsync(
				               participantId: participant.Id,
				               reason: MessageRedactionReason.Moderation,
				               utcNow: utcNow.AddMinutes(1));

			// Assert
			Assert.Equal(2, redacted);

			List<MessageEntity> reloaded = await Fixture.DbContext.Messages
				                               .AsNoTracking()
				                               .Where(m => m.Id == m1.Id || m.Id == m2.Id)
				                               .ToListAsync();

			Assert.Equal(2, reloaded.Count);
			Assert.All(
				reloaded,
				m =>
				{
					Assert.Null(m.Content);
					Assert.Equal(MessageRedactionReason.Moderation, m.RedactionReason);
					Assert.NotNull(m.RedactedAtUtc);
				});
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessagesByParticipantAsync"/> skips messages that are
		/// already redacted, preserving their original <see cref="MessageRedactionReason"/> and timestamp.
		/// </summary>
		[Fact]
		public async Task
			RedactMessagesByParticipantAsync_WhenSomeMessagesAlreadyRedacted_SkipsAlreadyRedactedMessages()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			MessageEntity preRedacted = await service.CreateMessageAsync(
				                            conversationId: conversation.Id,
				                            senderParticipantId: participant.Id,
				                            content: "Moderated",
				                            utcNow: utcNow);

			MessageEntity unredacted = await service.CreateMessageAsync(
				                           conversationId: conversation.Id,
				                           senderParticipantId: participant.Id,
				                           content: "Normal",
				                           utcNow: utcNow.AddSeconds(1));

			// Pre-redact one message with a different reason.
			DateTime moderationUtc = utcNow.AddMinutes(1);
			await service.RedactMessageAsync(preRedacted.Id, MessageRedactionReason.Moderation, moderationUtc);

			// Act — bulk-redact for UserDeleted; the pre-redacted message should be skipped.
			DateTime bulkUtc = utcNow.AddMinutes(2);
			int redacted = await service.RedactMessagesByParticipantAsync(
				               participantId: participant.Id,
				               reason: MessageRedactionReason.UserDeleted,
				               utcNow: bulkUtc);

			// Assert — only the unredacted message should have been affected.
			Assert.Equal(1, redacted);

			MessageEntity? preRedactedAfter = await Fixture.DbContext.Messages
				                                  .AsNoTracking()
				                                  .FirstOrDefaultAsync(m => m.Id == preRedacted.Id);

			Assert.NotNull(preRedactedAfter);
			Assert.Equal(MessageRedactionReason.Moderation, preRedactedAfter.RedactionReason);
			Assert.Equal(moderationUtc, preRedactedAfter.RedactedAtUtc);

			MessageEntity? unredactedAfter = await Fixture.DbContext.Messages
				                                 .AsNoTracking()
				                                 .FirstOrDefaultAsync(m => m.Id == unredacted.Id);

			Assert.NotNull(unredactedAfter);
			Assert.Null(unredactedAfter.Content);
			Assert.Equal(MessageRedactionReason.UserDeleted, unredactedAfter.RedactionReason);
			Assert.Equal(bulkUtc, unredactedAfter.RedactedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.RedactMessagesByParticipantAsync"/> validates the participant id
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task RedactMessagesByParticipantAsync_WhenParticipantIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.RedactMessagesByParticipantAsync(
					         participantId: new ParticipantId(0),
					         reason: MessageRedactionReason.Other,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("participantId.Value", ex.ParamName);
		}

		#endregion

		#region CreateMessageGenerationMetadataAsync

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageGenerationMetadataAsync"/> persists all metadata
		/// fields — including <see cref="MessageGenerationMetadataEntity.FullPrompt"/> — when
		/// <see cref="DatabaseOptions.StoreFullPrompts"/> is enabled.
		/// </summary>
		[Fact]
		public async Task
			CreateMessageGenerationMetadataAsync_WhenStoreFullPromptsEnabled_PersistsAllFieldsIncludingFullPrompt()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.StoreFullPrompts = true);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			MessageEntity message = await service.CreateMessageAsync(
				                        conversationId: conversation.Id,
				                        senderParticipantId: participant.Id,
				                        content: "Hello",
				                        utcNow: utcNow);

			ModelEndpointEntity endpoint = await service.CreateModelEndpointAsync(
				                               publicId: Guid.NewGuid(),
				                               providerType: "ollama",
				                               baseUrl: "https://example.test/api",
				                               name: "Test Endpoint",
				                               description: null,
				                               credentials: null,
				                               utcNow: utcNow);

			var source = new MessageGenerationMetadataEntity
			{
				ModelEndpointId = endpoint.Id,
				Model = "mistral:7b",
				PromptTokens = 100,
				CompletionTokens = 50,
				ResponseTime = TimeSpan.FromMilliseconds(1500),
				MaxTokens = 2048,
				Temperature = 0.7,
				TopP = 0.9,
				FullPrompt = "System: You are helpful.\nUser: Hello"
			};

			// Act
			MessageGenerationMetadataEntity created =
				await service.CreateMessageGenerationMetadataAsync(message.Id, source);

			MessageGenerationMetadataEntity? reloaded = await Fixture.DbContext.MessageGenerationMetadata
				                                            .AsNoTracking()
				                                            .FirstOrDefaultAsync(m => m.MessageId == message.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal(message.Id, reloaded.MessageId);
			Assert.Equal(endpoint.Id, reloaded.ModelEndpointId);
			Assert.Equal("mistral:7b", reloaded.Model);
			Assert.Equal(100, reloaded.PromptTokens);
			Assert.Equal(50, reloaded.CompletionTokens);
			Assert.Equal(TimeSpan.FromMilliseconds(1500), reloaded.ResponseTime);
			Assert.Equal(2048, reloaded.MaxTokens);
			Assert.Equal(0.7, reloaded.Temperature);
			Assert.Equal(0.9, reloaded.TopP);
			Assert.Equal("System: You are helpful.\nUser: Hello", reloaded.FullPrompt);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageGenerationMetadataAsync"/> prunes
		/// <see cref="MessageGenerationMetadataEntity.FullPrompt"/> when
		/// <see cref="DatabaseOptions.StoreFullPrompts"/> is disabled (the default).
		/// </summary>
		[Fact]
		public async Task CreateMessageGenerationMetadataAsync_WhenStoreFullPromptsDisabled_PrunesFullPrompt()
		{
			// Arrange
			// Special: StoreFullPrompts defaults to false; CreateMessageGenerationMetadataAsync() prunes FullPrompt.
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Conversation",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			MessageEntity message = await service.CreateMessageAsync(
				                        conversationId: conversation.Id,
				                        senderParticipantId: participant.Id,
				                        content: "Hello",
				                        utcNow: utcNow);

			ModelEndpointEntity endpoint = await service.CreateModelEndpointAsync(
				                               publicId: Guid.NewGuid(),
				                               providerType: "ollama",
				                               baseUrl: "https://example.test/api",
				                               name: "Test Endpoint",
				                               description: null,
				                               credentials: null,
				                               utcNow: utcNow);

			var source = new MessageGenerationMetadataEntity
			{
				ModelEndpointId = endpoint.Id,
				Model = "mistral:7b",
				FullPrompt = "System: You are helpful.\nUser: Hello"
			};

			// Act
			await service.CreateMessageGenerationMetadataAsync(message.Id, source);

			MessageGenerationMetadataEntity? reloaded = await Fixture.DbContext.MessageGenerationMetadata
				                                            .AsNoTracking()
				                                            .FirstOrDefaultAsync(m => m.MessageId == message.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Null(reloaded.FullPrompt);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageGenerationMetadataAsync"/> throws
		/// <see cref="InvalidOperationException"/> when the referenced message does not exist.
		/// </summary>
		[Fact]
		public async Task
			CreateMessageGenerationMetadataAsync_WhenMessageDoesNotExist_ThrowsInvalidOperationException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ModelEndpointEntity endpoint = await service.CreateModelEndpointAsync(
				                               publicId: Guid.NewGuid(),
				                               providerType: "ollama",
				                               baseUrl: "https://example.test/api",
				                               name: "Endpoint",
				                               description: null,
				                               credentials: null,
				                               utcNow: utcNow);

			var source = new MessageGenerationMetadataEntity
			{
				ModelEndpointId = endpoint.Id,
				Model = "mistral:7b"
			};

			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
				         service.CreateMessageGenerationMetadataAsync(new MessageId(12345), source));
			Assert.Matches(@"^Message '.+' does not exist\.$", ex.Message);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageGenerationMetadataAsync"/> throws
		/// <see cref="ArgumentNullException"/> when <c>metadata</c> is <see langword="null"/>.
		/// </summary>
		[Fact]
		public async Task CreateMessageGenerationMetadataAsync_WhenMetadataNull_ThrowsArgumentNullException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
				         service.CreateMessageGenerationMetadataAsync(new MessageId(1), null!));
			Assert.Equal("metadata", ex.ParamName);
		}

		/// <summary>
		/// Test data for
		/// <see cref="CreateMessageGenerationMetadataAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// Each row provides an invalid id combination that triggers an
		/// <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, MessageId, ModelEndpointId, string>
			CreateMessageGenerationMetadataAsync_InvalidId_Data => new()
		{
			// Message id is zero
			{ "Zero messageId", new MessageId(0), new ModelEndpointId(1), "messageId.Value" },

			// Model endpoint id is zero
			{ "Zero modelEndpointId", new MessageId(1), new ModelEndpointId(0), "metadata.ModelEndpointId.Value" }
		};

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateMessageGenerationMetadataAsync"/> validates id
		/// parameters and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="messageId">The message id to pass to the method.</param>
		/// <param name="modelEndpointId">The model endpoint id to set on the metadata.</param>
		/// <param name="expectedParamName">
		/// The expected <see cref="ArgumentOutOfRangeException.ParamName"/>.
		/// </param>
		[Theory]
		[MemberData(nameof(CreateMessageGenerationMetadataAsync_InvalidId_Data))]
		public async Task CreateMessageGenerationMetadataAsync_WhenIdInvalid_ThrowsArgumentOutOfRangeException(
			string          scenario,
			MessageId       messageId,
			ModelEndpointId modelEndpointId,
			string          expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			var source = new MessageGenerationMetadataEntity
			{
				ModelEndpointId = modelEndpointId,
				Model = "mistral:7b"
			};

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.CreateMessageGenerationMetadataAsync(messageId, source));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion
	}
}
