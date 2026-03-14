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
	/// Verifies that <see cref="ConversationQueries.GetById"/> returns the seeded conversation.
	/// </summary>
	[Fact]
	public async Task ConversationQueries_GetById_ReturnsConversation()
	{
		// Act
		ConversationEntity? result = await ConversationQueries.GetById(mFixture.DbContext, mConversationId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("Test conversation", result.Title);
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
		Assert.Equal("Test conversation", result.Title);
	}
}
