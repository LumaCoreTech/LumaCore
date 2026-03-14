// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Indicates why a message was redacted (content removed).
/// </summary>
/// <remarks>
///     <para>
///     The database stores only the reason code. User-facing text should be provided by the UI layer and localized
///     based on this value.
///     </para>
///     <para>
///     Redaction is used as a privacy mechanic to remove message content while preserving conversation structure for
///     other participants.
///     </para>
/// </remarks>
public enum MessageRedactionReason
{
	/// <summary>
	/// The message content was removed because the author deleted their account.
	/// </summary>
	UserDeleted = 1,

	/// <summary>
	/// The message content was removed due to moderation or a policy violation.
	/// </summary>
	Moderation = 2,

	/// <summary>
	/// The message content was removed because the author requested deletion of their content.
	/// </summary>
	UserRequestedDeletion = 3,

	/// <summary>
	/// The message content was removed for another reason not covered by a dedicated enum value.
	/// </summary>
	Other = 99
}
