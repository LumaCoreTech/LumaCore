// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Definitions;

/// <summary>
/// Describes a default role for database seeding.
/// </summary>
/// <param name="Name">The role name as stored in the database.</param>
/// <param name="Description">A human-readable description of the role's purpose.</param>
public readonly record struct RoleDefinition(string Name, string Description);
