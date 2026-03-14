// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Defines the role of a participant within a conversation.
/// </summary>
/// <remarks>
/// This role is specific to the conversation context and determines what actions a participant can perform
/// within that conversation. It is distinct from system-wide roles like 'admin' or 'moderator'.
/// The value is persisted in the database; permission checks are enforced by application logic.
/// </remarks>
public enum ConversationParticipantRole
{
	/// <summary>
	/// The participant who initiated the conversation.
	/// </summary>
	/// <remarks>
	/// Typically has full control including the ability to add/remove participants and delete the conversation.
	/// </remarks>
	Owner = 0,

	/// <summary>
	/// A regular participant who can read and send messages.
	/// </summary>
	Member = 1,

	/// <summary>
	/// A participant with read-only access.
	/// </summary>
	/// <remarks>
	/// Useful for archived participants or observers who should not contribute to the conversation.
	/// </remarks>
	Observer = 2
}
