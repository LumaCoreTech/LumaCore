// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.Auth;

/// <summary>
/// A single claim in the authentication principal.
/// </summary>
/// <param name="Type">The claim type (for example a URI or well-known claim name).</param>
/// <param name="Value">The claim value.</param>
public sealed record AuthClaimItem(
	string Type,
	string Value);
