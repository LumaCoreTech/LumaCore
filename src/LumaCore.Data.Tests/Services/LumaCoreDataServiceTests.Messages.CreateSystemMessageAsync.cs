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
	public sealed partial class Messages
	{
		#region CreateSystemMessageAsync

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateSystemMessageAsync"/> creates a
		/// <see cref="MessageEntity"/> with <see cref="MessageType.System"/>, no sender, trimmed content,
		/// and that it updates <see cref="ConversationEntity.UpdatedAtUtc"/>.
		/// </summary>
		[Fact]
		public async Task CreateSystemMessageAsync_WhenContentValid_CreatesSystemMessageAndUpdatesConversation()
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
			DateTime systemUtcNow = utcNow.AddMinutes(1);
			MessageEntity message = await service.CreateSystemMessageAsync(
				                        conversationId: conversation.Id,
				                        content: "  alice joined the conversation  ",
				                        utcNow: systemUtcNow);

			// Assert
			Assert.True(message.Id.Value > 0);
			Assert.NotEqual(Guid.Empty, message.PublicId);
			Assert.Equal(conversation.Id, message.ConversationId);
			Assert.Null(message.SenderId);
			Assert.Equal(MessageType.System, message.Type);
			Assert.Equal("alice joined the conversation", message.Content);
			Assert.Equal(systemUtcNow, message.CreatedAtUtc);

			// Special: CreateSystemMessageAsync() also updates Conversation.UpdatedAtUtc.
			ConversationEntity? reloadedConversation = await Fixture.DbContext.Conversations
				                                           .AsNoTracking()
				                                           .FirstOrDefaultAsync(c => c.Id == conversation.Id);

			Assert.NotNull(reloadedConversation);
			Assert.Equal(systemUtcNow, reloadedConversation.UpdatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateSystemMessageAsync"/> throws
		/// <see cref="ArgumentNullException"/> when <c>content</c> is <see langword="null"/>.
		/// </summary>
		[Fact]
		public async Task CreateSystemMessageAsync_WhenContentNull_ThrowsArgumentNullException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
				         service.CreateSystemMessageAsync(
					         conversationId: new ConversationId(1),
					         content: null,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("content", ex.ParamName);
		}

		/// <summary>
		/// Test data for <see cref="CreateSystemMessageAsync_WhenContentEmptyOrWhitespace_ThrowsArgumentException"/>.
		/// Each row provides a content value that is empty after trimming.
		/// </summary>
		public static TheoryData<string, string> CreateSystemMessageAsync_EmptyContent_Data => new()
		{
			// Empty string
			{ "Empty", string.Empty },

			// Spaces only
			{ "Spaces", "   " },

			// Mixed whitespace
			{ "Mixed whitespace", "\t\n " }
		};

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateSystemMessageAsync"/> throws
		/// <see cref="ArgumentException"/> when <c>content</c> is empty or contains only whitespace
		/// (system messages are not attachment-only and require text).
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="content">The content value to pass to the method.</param>
		[Theory]
		[MemberData(nameof(CreateSystemMessageAsync_EmptyContent_Data))]
		public async Task CreateSystemMessageAsync_WhenContentEmptyOrWhitespace_ThrowsArgumentException(
			string scenario,
			string content)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.CreateSystemMessageAsync(
					         conversationId: new ConversationId(1),
					         content: content,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("content", ex.ParamName);
			Assert.Equal("Message content must not be empty. (Parameter 'content')", ex.Message);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateSystemMessageAsync"/> validates the conversation id and
		/// throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task CreateSystemMessageAsync_WhenConversationIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.CreateSystemMessageAsync(
					         conversationId: new ConversationId(0),
					         content: "system message",
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("conversationId.Value", ex.ParamName);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.CreateSystemMessageAsync"/> throws
		/// <see cref="InvalidOperationException"/> when the referenced conversation does not exist.
		/// </summary>
		[Fact]
		public async Task CreateSystemMessageAsync_WhenConversationDoesNotExist_ThrowsInvalidOperationException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
				         service.CreateSystemMessageAsync(
					         conversationId: new ConversationId(12345),
					         content: "system message",
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Matches(@"^Conversation '.+' does not exist\.$", ex.Message);

			// Negative expectation: the failed transaction must roll back; no message should be persisted.
			Assert.Empty(await Fixture.DbContext.Messages.AsNoTracking().ToListAsync());
		}

		#endregion
	}
}
