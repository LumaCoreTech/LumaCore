// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// A single page of messages returned by <see cref="IMessageDataService.ListMessagesByConversationAsync"/>,
/// together with the paging coordinates that produced it and the total number of messages in the conversation.
/// </summary>
/// <param name="Messages">
/// Messages for the requested page, ordered by creation time (oldest first). May be empty if
/// <see cref="Offset"/> is past the end of the conversation. Entities are returned untracked.
/// </param>
/// <param name="TotalCount">
/// Total number of messages in the conversation, regardless of paging. Useful for computing the
/// number of pages on the caller side (e.g. <c>Math.Ceiling(TotalCount / (double)Limit)</c>).
/// </param>
/// <param name="Offset">
/// Zero-based position of the first returned message in the full ordered list.
/// Echoed back so the page is self-describing.
/// </param>
/// <param name="Limit">
/// Maximum number of messages the caller requested for this page.
/// The actual number of returned messages is <c>Messages.Count</c>, which is <c>&lt;= Limit</c>.
/// </param>
/// <remarks>
/// Replaces the earlier value-tuple return shape so the page contract is named, extensible,
/// and self-documenting at the call site (<c>page.Messages</c>, <c>page.TotalCount</c>, …)
/// instead of relying on positional destructuring.
/// </remarks>
public readonly record struct MessagePage(
	IReadOnlyList<MessageEntity> Messages,
	int                          TotalCount,
	int                          Offset,
	int                          Limit);
