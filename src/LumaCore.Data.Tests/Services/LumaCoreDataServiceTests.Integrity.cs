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
	/// Tests for <see cref="IDataIntegrityService"/> methods.
	/// </summary>
	/// <remarks>
	/// These tests cover integrity queries and cleanup routines that detect and remove invalid data shapes,
	/// such as conversations that have no associated user participants. The primary goal is to validate correctness
	/// and parameter bounds (limits) for set-based operations.
	/// </remarks>
	[Trait("Category", "Data")]
	public sealed class Integrity : TestBase
	{
		#region CleanupConversationsWithNoUsersAsync

		/// <summary>
		/// Verifies that <see cref="IDataIntegrityService.CleanupConversationsWithNoUsersAsync"/> deletes conversations that
		/// have no user participants and returns the number of deleted rows.
		/// </summary>
		[Fact]
		public async Task
			CleanupConversationsWithNoUsersAsync_WhenConversationsWithNoUsersExist_DeletesThoseConversations()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Special: CleanupConversationsWithNoUsersAsync() is a single set-based DELETE.
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var noUserConversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "NoUser",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(noUserConversation);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			int deleted = await service.CleanupConversationsWithNoUsersAsync();

			// Assert
			Assert.Equal(1, deleted);

			bool stillExists = await Fixture.DbContext.Conversations
				                   .AsNoTracking()
				                   .AnyAsync(c => c.Id == noUserConversation.Id);
			Assert.False(stillExists);
		}

		/// <summary>
		/// Verifies that <see cref="IDataIntegrityService.CleanupConversationsWithNoUsersAsync"/> deletes a conversation
		/// that has a non-user participant (e.g. persona/bot) but no user-backed participant.
		/// </summary>
		/// <remarks>
		/// This tests the actual join logic: the implementation joins <c>ConversationParticipants</c> with <c>Users</c>,
		/// so a participant without a backing <see cref="UserEntity"/> does not count.
		/// </remarks>
		[Fact]
		public async Task
			CleanupConversationsWithNoUsersAsync_WhenOnlyNonUserParticipant_DeletesConversation()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Special: A plain ParticipantEntity without a UserEntity acts as a persona/bot.
			var persona = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Bot",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(persona);

			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "BotOnly",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);
			await Fixture.DbContext.SaveChangesAsync();

			Fixture.DbContext.ConversationParticipants.Add(
				new ConversationParticipantEntity
				{
					ConversationId = conversation.Id,
					ParticipantId = persona.Id,
					Role = ConversationParticipantRole.Member,
					JoinedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			int deleted = await service.CleanupConversationsWithNoUsersAsync();

			// Assert
			Assert.Equal(1, deleted);

			bool stillExists = await Fixture.DbContext.Conversations
				                   .AsNoTracking()
				                   .AnyAsync(c => c.Id == conversation.Id);
			Assert.False(stillExists);
		}

		/// <summary>
		/// Verifies that <see cref="IDataIntegrityService.CleanupConversationsWithNoUsersAsync"/> selectively deletes only
		/// conversations with no user participants, preserving conversations that have at least one user.
		/// </summary>
		[Fact]
		public async Task
			CleanupConversationsWithNoUsersAsync_WhenMixOfConversationsExist_DeletesOnlyOrphanConversations()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var orphanConversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "Orphan",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(orphanConversation);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			ConversationEntity healthyConversation = await service.CreateConversationAsync(
				                                         title: "Healthy",
				                                         creatorParticipantId: participant.Id,
				                                         utcNow: utcNow);

			await Fixture.DbContext.SaveChangesAsync();

			// Act
			int deleted = await service.CleanupConversationsWithNoUsersAsync();

			// Assert
			Assert.Equal(1, deleted);

			bool orphanStillExists = await Fixture.DbContext.Conversations
				                         .AsNoTracking()
				                         .AnyAsync(c => c.Id == orphanConversation.Id);
			Assert.False(orphanStillExists);

			bool healthyStillExists = await Fixture.DbContext.Conversations
				                          .AsNoTracking()
				                          .AnyAsync(c => c.Id == healthyConversation.Id);
			Assert.True(healthyStillExists);
		}

		/// <summary>
		/// Verifies that <see cref="IDataIntegrityService.CleanupConversationsWithNoUsersAsync"/> returns 0 when all
		/// conversations have at least one user participant.
		/// </summary>
		[Fact]
		public async Task CleanupConversationsWithNoUsersAsync_WhenAllConversationsHaveUsers_ReturnsZero()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			await service.CreateConversationAsync(
				title: "Healthy",
				creatorParticipantId: participant.Id,
				utcNow: utcNow);

			// Act
			int deleted = await service.CleanupConversationsWithNoUsersAsync();

			// Assert
			Assert.Equal(0, deleted);
		}

		#endregion

		#region ListConversationIdsWithNoUsersAsync

		/// <summary>
		/// Verifies that <see cref="IDataIntegrityService.ListConversationIdsWithNoUsersAsync(int, CancellationToken)"/>
		/// returns only conversations that have no user participants.
		/// </summary>
		/// <remarks>
		/// The test creates one conversation without any memberships and one conversation created via the service (which
		/// automatically creates an Owner membership backed by a <see cref="UserEntity"/>).
		/// </remarks>
		[Fact]
		public async Task
			ListConversationIdsWithNoUsersAsync_WhenMixOfConversationsExist_ReturnsOnlyConversationsWithoutUsers()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Special: this conversation has no ConversationParticipants/User links.
			var noUserConversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "NoUser",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(noUserConversation);

			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			// Special: creating via service ensures the creator is joined as Owner.
			ConversationEntity withUserConversation = await service.CreateConversationAsync(
				                                          title: "WithUser",
				                                          creatorParticipantId: participant.Id,
				                                          utcNow: utcNow);

			await Fixture.DbContext.SaveChangesAsync();

			// Act
			List<ConversationId> ids = await service.ListConversationIdsWithNoUsersAsync(limit: 100);

			// Assert
			Assert.Equal(1, ids.Count);
			Assert.Contains(noUserConversation.Id, ids);
			Assert.DoesNotContain(withUserConversation.Id, ids);
		}

		/// <summary>
		/// Verifies that <see cref="IDataIntegrityService.ListConversationIdsWithNoUsersAsync(int, CancellationToken)"/>
		/// includes a conversation whose only participant is a non-user (persona/bot).
		/// </summary>
		/// <remarks>
		/// This tests the join logic: the implementation joins <c>ConversationParticipants</c> with <c>Users</c>,
		/// so a participant without a backing <see cref="UserEntity"/> does not satisfy the "has users" condition.
		/// </remarks>
		[Fact]
		public async Task
			ListConversationIdsWithNoUsersAsync_WhenOnlyNonUserParticipant_IncludesConversation()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var persona = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = "Bot",
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(persona);

			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = "BotOnly",
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Conversations.Add(conversation);
			await Fixture.DbContext.SaveChangesAsync();

			Fixture.DbContext.ConversationParticipants.Add(
				new ConversationParticipantEntity
				{
					ConversationId = conversation.Id,
					ParticipantId = persona.Id,
					Role = ConversationParticipantRole.Member,
					JoinedAtUtc = utcNow
				});
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			List<ConversationId> ids = await service.ListConversationIdsWithNoUsersAsync(limit: 100);

			// Assert
			ConversationId singleId = Assert.Single(ids);
			Assert.Equal(conversation.Id, singleId);
		}

		/// <summary>
		/// Verifies that <see cref="IDataIntegrityService.ListConversationIdsWithNoUsersAsync(int, CancellationToken)"/>
		/// respects the <c>limit</c> parameter when there are more matching conversations than the limit.
		/// </summary>
		[Fact]
		public async Task ListConversationIdsWithNoUsersAsync_WhenMoreResultsThanLimit_RespectsLimit()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Seed three conversations without any user participants.
			for (int i = 0; i < 3; i++)
			{
				Fixture.DbContext.Conversations.Add(
					new ConversationEntity
					{
						PublicId = Guid.NewGuid(),
						Title = $"NoUser-{i}",
						CreatedAtUtc = utcNow,
						UpdatedAtUtc = utcNow
					});
			}

			await Fixture.DbContext.SaveChangesAsync();

			// Act — request only 2 of the 3 matching conversations.
			List<ConversationId> ids = await service.ListConversationIdsWithNoUsersAsync(limit: 2);

			// Assert
			Assert.Equal(2, ids.Count);
		}

		/// <summary>
		/// Verifies that <see cref="IDataIntegrityService.ListConversationIdsWithNoUsersAsync(int, CancellationToken)"/>
		/// returns an empty list when all conversations have at least one user participant.
		/// </summary>
		[Fact]
		public async Task ListConversationIdsWithNoUsersAsync_WhenAllConversationsHaveUsers_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ParticipantEntity participant = await CreateUserParticipantAsync("alice", "alice@example.test", utcNow);

			await service.CreateConversationAsync(
				title: "Healthy",
				creatorParticipantId: participant.Id,
				utcNow: utcNow);

			// Act
			List<ConversationId> ids = await service.ListConversationIdsWithNoUsersAsync(limit: 100);

			// Assert
			Assert.Empty(ids);
		}

		/// <summary>
		/// Test data for
		/// <see cref="ListConversationIdsWithNoUsersAsync_WhenLimitInvalid_ThrowsArgumentOutOfRangeException"/>.
		/// Each row provides an invalid limit value that triggers an <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		public static TheoryData<string, int> ListConversationIdsWithNoUsersAsync_InvalidLimit_Data => new()
		{
			// Zero limit
			{ "Zero limit", 0 },

			// Negative limit
			{ "Negative limit", -1 }
		};

		/// <summary>
		/// Verifies that <see cref="IDataIntegrityService.ListConversationIdsWithNoUsersAsync(int, CancellationToken)"/>
		/// validates the <c>limit</c> parameter and throws <see cref="ArgumentOutOfRangeException"/> when outside the
		/// allowed range.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="limit">The limit value to pass to the method.</param>
		[Theory]
		[MemberData(nameof(ListConversationIdsWithNoUsersAsync_InvalidLimit_Data))]
		public async Task ListConversationIdsWithNoUsersAsync_WhenLimitInvalid_ThrowsArgumentOutOfRangeException(
			string scenario,
			int    limit)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.ListConversationIdsWithNoUsersAsync(limit: limit));
			Assert.Equal("limit", ex.ParamName);
		}

		#endregion
	}
}
