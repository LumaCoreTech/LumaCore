// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Services;

/// <summary>
/// Result of deleting private conversations for a user participant.
/// </summary>
/// <param name="Deleted">The number of private conversations that were deleted.</param>
/// <param name="SkippedMultiUser">The number of conversations skipped because they contain multiple users.</param>
public readonly record struct DeletePrivateConversationsResult(
	int Deleted,
	int SkippedMultiUser);
