// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Queries;
using LumaCore.Data.Services;

using Xunit;

namespace LumaCore.Data.Tests.Queries;

public sealed partial class CompiledQueryRegressionTests
{
	/// <summary>
	/// Verifies that <see cref="MessageQueries.CountByConversationId"/> returns the correct message count.
	/// </summary>
	[Fact]
	public async Task MessageQueries_CountByConversationId_ReturnsCount()
	{
		// Act
		int count = await MessageQueries.CountByConversationId(mFixture.DbContext, mConversationId);

		// Assert
		Assert.Equal(3, count);
	}

	/// <summary>
	/// Verifies that <see cref="MessageQueries.GetByConversationId"/> returns messages ordered by creation time
	/// with the <see cref="MessageEntity.Sender"/> navigation populated.
	/// </summary>
	/// <remarks>
	/// The Sender assertion guards against accidental removal of the <c>Include(m =&gt; m.Sender)</c> in the
	/// compiled query — a regression that would silently break the Sender-population contract documented on
	/// <see cref="IMessageDataService"/> list APIs.
	/// </remarks>
	[Fact]
	public async Task MessageQueries_GetByConversationId_ReturnsMessagesInChronologicalOrderWithSender()
	{
		// Act
		List<MessageEntity> messages = await ToListAsync(
			                               MessageQueries.GetByConversationId(mFixture.DbContext, mConversationId));

		// Assert
		Assert.Equal(3, messages.Count);
		Assert.Equal("First message", messages[0].Content);
		Assert.Equal("Middle message", messages[1].Content);
		Assert.Equal("Last message", messages[2].Content);
		Assert.All(messages, m => Assert.Equal(mConversationId, m.ConversationId));

		// Sender-population contract: every message in the seed has Alice as sender.
		Assert.All(
			messages,
			m =>
			{
				Assert.NotNull(m.Sender);
				Assert.Equal(mAliceParticipantId, m.Sender.Id);
				Assert.Equal("Alice", m.Sender.DisplayName);
			});
	}

	/// <summary>
	/// Verifies that <see cref="MessageQueries.GetByPublicId"/> returns the message matching the given
	/// public ID with the <see cref="MessageEntity.Sender"/> navigation populated.
	/// </summary>
	/// <remarks>
	/// The Sender assertion guards against accidental removal of the <c>Include(m =&gt; m.Sender)</c> in the
	/// compiled query — the REST-facing <see cref="IMessageDataService.GetMessageByPublicIdAsync"/> relies on
	/// it to render the message without a follow-up roundtrip.
	/// </remarks>
	[Fact]
	public async Task MessageQueries_GetByPublicId_ReturnsMessageWithSender()
	{
		// Act
		MessageEntity? result = await MessageQueries.GetByPublicId(mFixture.DbContext, mMessagePublicId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(mMessagePublicId, result.PublicId);
		Assert.Equal(mConversationId, result.ConversationId);
		Assert.Equal(mAliceParticipantId, result.SenderId);
		Assert.Equal("Middle message", result.Content);

		// Sender-population contract.
		Assert.NotNull(result.Sender);
		Assert.Equal(mAliceParticipantId, result.Sender.Id);
		Assert.Equal("Alice", result.Sender.DisplayName);
	}

	/// <summary>
	/// Verifies that <see cref="MessageQueries.GetRecentByConversationId"/> returns the most recent messages
	/// up to the specified limit, newest first, excludes older messages beyond the limit, and populates the
	/// <see cref="MessageEntity.Sender"/> navigation.
	/// </summary>
	/// <remarks>
	/// The Sender assertion guards against accidental removal of the <c>Include(m =&gt; m.Sender)</c> in the
	/// compiled query — <see cref="IMessageDataService.ListRecentMessagesByConversationAsync"/> documents that
	/// callers can rely on Sender being populated to avoid N+1 lookups in chat-pane rendering.
	/// </remarks>
	[Fact]
	public async Task MessageQueries_GetRecentByConversationId_ReturnsLimitedMessagesNewestFirstWithSender()
	{
		// Act
		// Limit is 2 — with three seeded messages this verifies (a) Take() truncates the result,
		// (b) OrderByDescending picks the newest, (c) the oldest message is excluded.
		List<MessageEntity> recent = await ToListAsync(
			                             MessageQueries.GetRecentByConversationId(
				                             mFixture.DbContext,
				                             mConversationId,
				                             2));

		// Assert
		Assert.Equal(2, recent.Count);
		Assert.Equal("Last message", recent[0].Content);
		Assert.Equal("Middle message", recent[1].Content);
		Assert.DoesNotContain(recent, m => m.Content == "First message");

		// Sender-population contract.
		Assert.All(
			recent,
			m =>
			{
				Assert.NotNull(m.Sender);
				Assert.Equal(mAliceParticipantId, m.Sender.Id);
				Assert.Equal("Alice", m.Sender.DisplayName);
			});
	}
}
