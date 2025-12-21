// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.Auth;

/// <summary>
/// Information about the current authenticated user.
/// </summary>
/// <param name="Name">The logical name of the authenticated user.</param>
/// <param name="Roles">The set of roles associated with the user.</param>
/// <param name="Claims">The raw claims associated with the user principal.</param>
public sealed record AuthWhoAmIResponse(
	string                       Name,
	IReadOnlyList<string>        Roles,
	IReadOnlyList<AuthClaimItem> Claims);
