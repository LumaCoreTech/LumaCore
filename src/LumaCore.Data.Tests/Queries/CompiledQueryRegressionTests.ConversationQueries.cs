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
	/// Verifies that <see cref="ConversationQueries.CountByParticipantId"/> returns the correct count
	/// for a participant with one conversation.
	/// </summary>
	[Fact]
	public async Task ConversationQueries_CountByParticipantId_ReturnsCount()
	{
		// Act
		int count = await ConversationQueries.CountByParticipantId(mFixture.DbContext, mAliceParticipantId);

		// Assert
		Assert.Equal(1, count);
	}

	/// <summary>
	/// Verifies that <see cref="ConversationQueries.GetByParticipantId"/> returns conversations
	/// the participant is a member of.
	/// </summary>
	[Fact]
	public async Task ConversationQueries_GetByParticipantId_ReturnsConversations()
	{
		// Act
		List<ConversationEntity> conversations = await ToListAsync(
			                                         ConversationQueries.GetByParticipantId(
				                                         mFixture.DbContext,
				                                         mAliceParticipantId));

		// Assert
		Assert.Single(conversations);
		Assert.Equal(mConversationId, conversations[0].Id);
		Assert.Equal("Test conversation", conversations[0].Title);
	}

	/// <summary>
	/// Verifies that <see cref="ConversationQueries.GetByPublicId"/> returns the conversation
	/// matching the given public ID.
	/// </summary>
	[Fact]
	public async Task ConversationQueries_GetByPublicId_ReturnsConversation()
	{
		// Act
		ConversationEntity? result =
			await ConversationQueries.GetByPublicId(mFixture.DbContext, mConversationPublicId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(mConversationId, result.Id);
		Assert.Equal(mConversationPublicId, result.PublicId);
		Assert.Equal("Test conversation", result.Title);
	}

	/// <summary>
	/// Verifies that <see cref="ConversationQueries.IsParticipantInConversation"/> returns
	/// <see langword="true"/> only for participants actually joined to the conversation.
	/// </summary>
	/// <param name="participantSelector">
	/// Selector indicating whether to probe with the seeded member (<c>"member"</c>, Alice) or with a
	/// participant that is not joined to the conversation (<c>"nonMember"</c>, the bot persona's
	/// participant). Indirection via a selector keeps the seeded ID out of the
	/// <see cref="InlineDataAttribute"/> arguments, which must be compile-time constants.
	/// </param>
	/// <param name="expected">The expected outcome.</param>
	[Theory]
	[InlineData("member", true)]     // Alice is a ConversationParticipant in the seed.
	[InlineData("nonMember", false)] // The bot participant is never joined to the conversation.
	public async Task ConversationQueries_IsParticipantInConversation_ReturnsTrueOnlyForJoinedParticipant(
		string participantSelector,
		bool   expected)
	{
		// Arrange
		ParticipantId participantId = participantSelector == "member"
			                              ? mAliceParticipantId
			                              : mBotParticipantId;

		// Act
		bool actual = await ConversationQueries.IsParticipantInConversation(
			              mFixture.DbContext,
			              mConversationId,
			              participantId);

		// Assert
		Assert.Equal(expected, actual);
	}
}
