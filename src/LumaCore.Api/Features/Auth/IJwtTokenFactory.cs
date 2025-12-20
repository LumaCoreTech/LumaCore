// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Claims;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Defines a service that can create JSON Web Tokens (JWT) for authenticated users.
/// </summary>
interface IJwtTokenFactory
{
	/// <summary>
	/// Creates a signed access token for the specified subject.
	/// </summary>
	/// <param name="subject">The logical identity of the user (e.g. username).</param>
	/// <param name="claims">Additional claims to embed into the token.</param>
	/// <returns>A serialized and signed JWT.</returns>
	string CreateToken(string subject, IEnumerable<Claim> claims);
}
