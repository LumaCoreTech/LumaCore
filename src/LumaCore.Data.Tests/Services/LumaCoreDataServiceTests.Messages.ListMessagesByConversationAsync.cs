// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LumaCoreDataServiceTests
{
	public sealed partial class Messages
	{
		#region ListMessagesByConversationAsync

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListMessagesByConversationAsync"/> returns messages ordered by
		/// ascending creation time and that the <see cref="MessageEntity.Sender"/> navigation is populated for every
		/// returned row.
		/// </summary>
		/// <remarks>
		/// The Sender assertion guards against regressions in the projected pagination query, which must explicitly
		/// project <c>m.Sender</c> so EF's identity fixup attaches it to <see cref="MessageEntity.Sender"/>.
		/// </remarks>
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

			// Special: insert messages in REVERSE chronological order (later CreatedAtUtc first, earlier
			// CreatedAtUtc second). This decouples insertion order from CreatedAtUtc order, so a buggy
			// implementation that orders by Id/insertion (instead of CreatedAtUtc) would return [m2, m1]
			// and fail the assertions below.
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
			MessagePage page = await service.ListMessagesByConversationAsync(conversationId: conversation.Id);

			// Assert
			Assert.Equal(2, page.TotalCount);
			Assert.Equal(0, page.Offset);
			Assert.Equal(int.MaxValue, page.Limit);
			IReadOnlyList<MessageEntity> messages = page.Messages;
			Assert.Equal(2, messages.Count);
			Assert.Equal(m1.Id, messages[0].Id);
			Assert.Equal(m2.Id, messages[1].Id);

			// Sender navigation must be populated by the projected query (regression guard for the
			// single-roundtrip pagination implementation).
			Assert.NotNull(messages[0].Sender);
			Assert.Equal(participant.Id, messages[0].Sender!.Id);
			Assert.NotNull(messages[1].Sender);
			Assert.Equal(participant.Id, messages[1].Sender!.Id);
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
			MessagePage page = await service.ListMessagesByConversationAsync(conversation.Id);

			// Assert
			Assert.Empty(page.Messages);
			Assert.Equal(0, page.TotalCount);
			Assert.Equal(0, page.Offset);
			Assert.Equal(int.MaxValue, page.Limit);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListMessagesByConversationAsync"/> honors <c>offset</c>
		/// and <c>limit</c> by returning the requested page slice while reporting the full
		/// <c>TotalCount</c> across all messages of the conversation.
		/// </summary>
		[Fact]
		public async Task
			ListMessagesByConversationAsync_WhenOffsetAndLimitProvided_ReturnsRequestedPageWithFullTotalCount()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Paging",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			var created = new MessageEntity[5];
			for (int i = 0; i < 5; i++)
			{
				created[i] = await service.CreateMessageAsync(
					             conversationId: conversation.Id,
					             senderParticipantId: participant.Id,
					             content: $"m{i}",
					             utcNow: utcNow.AddSeconds(i + 1));
			}

			// Act — skip the first message, take the next two (positions 1 and 2 of an ascending order).
			MessagePage page = await service.ListMessagesByConversationAsync(conversation.Id, offset: 1, limit: 2);

			// Assert
			Assert.Equal(5, page.TotalCount);
			Assert.Equal(1, page.Offset);
			Assert.Equal(2, page.Limit);
			IReadOnlyList<MessageEntity> messages = page.Messages;
			Assert.Equal(2, messages.Count);
			Assert.Equal(created[1].Id, messages[0].Id);
			Assert.Equal(created[2].Id, messages[1].Id);

			// Defense-in-depth: Sender must also be populated on paginated slices (regression guard
			// for the AsNoTrackingWithIdentityResolution projection in the paging code path).
			Assert.NotNull(messages[0].Sender);
			Assert.Equal(participant.Id, messages[0].Sender!.Id);
			Assert.NotNull(messages[1].Sender);
			Assert.Equal(participant.Id, messages[1].Sender!.Id);
		}

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListMessagesByConversationAsync"/> returns an empty page but the
		/// correct total count when <c>offset</c> exceeds the available number of messages. This exercises the
		/// fallback path where the projected page yields no rows and a separate count query must run.
		/// The toggle parameter asserts the contract — both the regular EF branch and the compiled hot-path
		/// branch (delegating to <c>MessageQueries.CountByConversationId</c>) must yield the same total count.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task ListMessagesByConversationAsync_WhenOffsetBeyondData_ReturnsEmptyListWithCorrectTotalCount(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);
			ConversationEntity conversation = await service.CreateConversationAsync(
				                                  title: "Beyond",
				                                  creatorParticipantId: participant.Id,
				                                  utcNow: utcNow);

			await service.CreateMessageAsync(
				conversationId: conversation.Id,
				senderParticipantId: participant.Id,
				content: "First",
				utcNow: utcNow);
			await service.CreateMessageAsync(
				conversationId: conversation.Id,
				senderParticipantId: participant.Id,
				content: "Second",
				utcNow: utcNow.AddSeconds(1));

			// Act — skip past all rows so the count fallback runs.
			MessagePage page = await service.ListMessagesByConversationAsync(conversation.Id, offset: 5, limit: 10);

			// Assert
			Assert.Empty(page.Messages);
			Assert.Equal(2, page.TotalCount);
			Assert.Equal(5, page.Offset);
			Assert.Equal(10, page.Limit);
		}

		/// <summary>
		/// Test data for <see cref="ListMessagesByConversationAsync_WhenArgumentInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// Each row provides an invalid argument combination that triggers an <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, ConversationId, int, int, string>
			ListMessagesByConversationAsync_InvalidArgument_Data => new()
		{
			// Conversation id is zero
			{ "Zero conversationId", new ConversationId(0), 0, int.MaxValue, "conversationId.Value" },

			// Offset is negative
			{ "Negative offset", new ConversationId(1), -1, int.MaxValue, "offset" },

			// Limit is zero
			{ "Zero limit", new ConversationId(1), 0, 0, "limit" },

			// Limit is negative
			{ "Negative limit", new ConversationId(1), 0, -1, "limit" }
		};

		/// <summary>
		/// Verifies that <see cref="IMessageDataService.ListMessagesByConversationAsync"/> validates its arguments and
		/// throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids, negative <c>offset</c> and
		/// non-positive <c>limit</c>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="conversationId">The conversation id to pass to the method.</param>
		/// <param name="offset">The offset value to pass to the method.</param>
		/// <param name="limit">The limit value to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(ListMessagesByConversationAsync_InvalidArgument_Data))]
		public async Task ListMessagesByConversationAsync_WhenArgumentInvalid_ThrowsArgumentOutOfRangeException(
			string         scenario,
			ConversationId conversationId,
			int            offset,
			int            limit,
			string         expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.ListMessagesByConversationAsync(conversationId, offset, limit));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion
	}
}
