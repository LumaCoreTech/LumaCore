// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Claims;

namespace LumaCore.Api.Features.UserManagement;

/// <summary>
/// Represents the identity of a successfully authenticated user, including the canonical username
/// and the roles assigned to the account.
/// </summary>
/// <param name="Username">
/// The canonical (trimmed) username. This value is used as the JWT <c>sub</c> claim and as
/// <see cref="ClaimTypes.Name"/>.
/// </param>
/// <param name="Roles">
/// The roles assigned to the user. Each entry is added as a
/// <see cref="ClaimTypes.Role"/> claim in the issued JWT.
/// </param>
sealed record AuthenticatedUser(string Username, IReadOnlyList<string> Roles);
