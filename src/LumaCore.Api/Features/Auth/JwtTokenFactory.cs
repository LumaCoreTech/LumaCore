// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Implements <see cref="IJwtTokenFactory"/> to produce signed JSON Web Tokens.
/// </summary>
/// <remarks>
///     <para>
///     This factory is a thin, stateless wrapper around <see cref="JwtSecurityTokenHandler"/>
///     and uses the configured <see cref="JwtOptions"/> to determine issuer, audience, lifetime,
///     and signing key.
///     </para>
///     <para>
///     The factory does not perform any kind of user authentication. It assumes that the caller
///     has already validated credentials and supplies the correct subject and claims.
///     </para>
/// </remarks>
public sealed class JwtTokenFactory : IJwtTokenFactory
{
	private readonly JwtOptions mOptions;
	private readonly byte[]     mSigningKeyBytes;

	/// <summary>
	/// Initializes a new instance of the <see cref="JwtTokenFactory"/> class.
	/// </summary>
	/// <param name="options">The configured JWT options.</param>
	public JwtTokenFactory(IOptions<JwtOptions> options)
	{
		mOptions = options.Value;
		mSigningKeyBytes = Encoding.UTF8.GetBytes(mOptions.SigningKey);
	}

	/// <inheritdoc/>
	public string CreateToken(string subject, IEnumerable<Claim> claims)
	{
		DateTime utcNow = DateTime.UtcNow;

		// Base claims required for most JWT consumers (subject + unique token ID).
		var allClaims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Sub, subject),
			new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
		};

		// Application-specific claims (roles, permissions, etc.).
		allClaims.AddRange(claims);

		// Create signing credentials using the configured symmetric key.
		var signingCredentials = new SigningCredentials(
			new SymmetricSecurityKey(mSigningKeyBytes),
			SecurityAlgorithms.HmacSha256);

		// Build token using configured issuer, audience and lifetime.
		var token = new JwtSecurityToken(
			issuer: mOptions.Issuer,
			audience: mOptions.Audience,
			claims: allClaims,
			notBefore: utcNow,
			expires: utcNow.AddMinutes(mOptions.AccessTokenLifetimeMinutes),
			signingCredentials: signingCredentials);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
