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
	/// Verifies that <see cref="MessageQueries.CountByConversationId"/> returns the correct message count.
	/// </summary>
	[Fact]
	public async Task MessageQueries_CountByConversationId_ReturnsCount()
	{
		// Act
		int count = await MessageQueries.CountByConversationId(mFixture.DbContext, mConversationId);

		// Assert
		Assert.Equal(1, count);
	}

	/// <summary>
	/// Verifies that <see cref="MessageQueries.GetByConversationId"/> returns messages ordered by creation time.
	/// </summary>
	[Fact]
	public async Task MessageQueries_GetByConversationId_ReturnsMessages()
	{
		// Act
		List<MessageEntity> messages = await ToListAsync(
			                               MessageQueries.GetByConversationId(mFixture.DbContext, mConversationId));

		// Assert
		Assert.Single(messages);
		Assert.Equal("Hello, world!", messages[0].Content);
	}

	/// <summary>
	/// Verifies that <see cref="MessageQueries.GetByPublicId"/> returns the message matching the given
	/// public ID.
	/// </summary>
	[Fact]
	public async Task MessageQueries_GetByPublicId_ReturnsMessage()
	{
		// Act
		MessageEntity? result = await MessageQueries.GetByPublicId(mFixture.DbContext, mMessagePublicId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("Hello, world!", result.Content);
	}

	/// <summary>
	/// Verifies that <see cref="MessageQueries.GetRecentByConversationId"/> returns the most recent messages
	/// up to the specified limit.
	/// </summary>
	[Fact]
	public async Task MessageQueries_GetRecentByConversationId_ReturnsLimitedMessages()
	{
		// Act
		List<MessageEntity> recent = await ToListAsync(
			                             MessageQueries.GetRecentByConversationId(
				                             mFixture.DbContext,
				                             mConversationId,
				                             10));

		// Assert
		Assert.Single(recent);
		Assert.Equal("Hello, world!", recent[0].Content);
	}
}
