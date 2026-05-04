// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// Provides data integrity checks and cleanup operations.
/// </summary>
public interface IDataIntegrityService
{
	#region Projection APIs

	/// <summary>
	/// Lists conversations that have no user participants.
	/// </summary>
	/// <param name="limit">Maximum number of IDs to return.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A list of internal conversation identifiers that currently have zero user participants.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="limit"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// This is a data integrity check. Under normal operation, every conversation should include at least one
	/// user participant. Conversations without users are typically unreachable and indicate inconsistent data.
	/// </remarks>
	Task<IReadOnlyList<ConversationId>> ListConversationIdsWithNoUsersAsync(
		int               limit,
		CancellationToken cancellationToken = default);

	#endregion

	#region Mutation APIs

	/// <summary>
	/// Deletes all conversations that currently have no user participants.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of conversations deleted.</returns>
	/// <remarks>
	/// This is a cleanup operation for inconsistent/unreachable data.
	/// Under normal operation, conversations without user participants should not exist.
	/// </remarks>
	Task<int> CleanupConversationsWithNoUsersAsync(CancellationToken cancellationToken = default);

	#endregion
}
