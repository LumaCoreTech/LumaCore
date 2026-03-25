// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using LumaCore.Api.Features.Auth;

using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

// CreateToken(): from base claims to cryptographic validation.
//
// These tests verify the complete token lifecycle from creation through parsing:
//
//   1. Base claims: sub, jti, iss, aud are present and well-formed; the exact claim-type set
//      contains only the expected types (no unexpected claims leak in).
//   2. Uniqueness: each call generates a distinct jti (token ID).
//   3. Application claims: additional claims are embedded alongside the base claims;
//      the claim-type set grows by exactly the application types.
//   4. Temporal correctness: nbf and exp reflect the FakeTimeProvider and configured lifetime.
//   5. Cryptographic integrity: the token signature validates under the configured signing key.
public sealed partial class JwtTokenFactoryTests
{
	// --- 1. Base claims ---

	/// <summary>
	/// Verifies that a token created without additional claims contains exactly the expected base
	/// claims (<c>sub</c>, <c>jti</c>, <c>iss</c>, <c>aud</c>, <c>nbf</c>, <c>exp</c>) with correct
	/// values, and no other claim types are present.
	/// </summary>
	[Fact]
	public void CreateToken_WithValidInput_ContainsExpectedBaseClaims()
	{
		// Arrange
		JwtTokenFactory sut = CreateSut();

		// Act
		string tokenString = sut.CreateToken(TestSubject, []);

		// Assert
		JwtSecurityToken token = ParseToken(tokenString);

		// Identity claims.
		Assert.Equal(TestSubject, token.Subject);
		Assert.NotNull(token.Id);
		Assert.Matches("^[0-9a-f]{32}$", token.Id);

		// Configuration claims.
		Assert.Equal(TestIssuer, token.Issuer);
		string audience = Assert.Single(token.Audiences);
		Assert.Equal(TestAudience, audience);

		// Exact claim-type set — no unexpected claims leak in.
		string[] expectedTypes = ["aud", "exp", "iss", "jti", "nbf", "sub"];
		string[] actualTypes = token.Claims.Select(c => c.Type).Distinct().Order().ToArray();
		Assert.Equal(expectedTypes, actualTypes);
	}

	// --- 2. Uniqueness ---

	/// <summary>
	/// Verifies that consecutive calls to <see cref="JwtTokenFactory.CreateToken"/> produce tokens with
	/// distinct <c>jti</c> (JWT ID) values, preventing token confusion or replay.
	/// </summary>
	[Fact]
	public void CreateToken_CalledTwice_GeneratesUniqueJtiPerCall()
	{
		// Arrange
		JwtTokenFactory sut = CreateSut();

		// Act
		JwtSecurityToken first = ParseToken(sut.CreateToken(TestSubject, []));
		JwtSecurityToken second = ParseToken(sut.CreateToken(TestSubject, []));

		// Assert
		Assert.NotEqual(first.Id, second.Id);
	}

	// --- 3. Application claims ---

	/// <summary>
	/// Verifies that additional claims (roles, custom fields) passed to
	/// <see cref="JwtTokenFactory.CreateToken"/> are embedded in the generated token and that the
	/// complete claim-type set reflects both base and application claim types.
	/// </summary>
	[Fact]
	public void CreateToken_WithAdditionalClaims_EmbedsAllClaims()
	{
		// Arrange
		JwtTokenFactory sut = CreateSut();
		var claims = new List<Claim>
		{
			new("role", "Admin"),
			new("role", "Editor"),
			new("custom_field", "custom_value")
		};

		// Act
		string tokenString = sut.CreateToken(TestSubject, claims);

		// Assert
		JwtSecurityToken token = ParseToken(tokenString);

		// Role claims are embedded (multiple values for the same claim type).
		List<string> roles = token.Claims
			.Where(c => c.Type == "role")
			.Select(c => c.Value)
			.ToList();
		Assert.Equal(2, roles.Count);
		Assert.Contains("Admin", roles);
		Assert.Contains("Editor", roles);

		// Custom claim is embedded.
		Claim customClaim = Assert.Single(token.Claims, c => c.Type == "custom_field");
		Assert.Equal("custom_value", customClaim.Value);

		// Exact claim-type set — base claims plus the two application claim types.
		string[] expectedTypes =
			["aud", "custom_field", "exp", "iss", "jti", "nbf", "role", "sub"];
		string[] actualTypes = token.Claims.Select(c => c.Type).Distinct().Order().ToArray();
		Assert.Equal(expectedTypes, actualTypes);
	}

	// --- 4. Temporal correctness ---

	/// <summary>
	/// Verifies that the <c>nbf</c> (not before) and <c>exp</c> (expiration) claims reflect the
	/// <see cref="FakeTimeProvider"/>'s current time and the configured
	/// <see cref="JwtOptions.AccessTokenLifetimeMinutes"/>.
	/// </summary>
	[Fact]
	public void CreateToken_WithValidInput_SetsCorrectLifetime()
	{
		// Arrange — use a non-default lifetime to prove the value is read from options.
		JwtOptions options = CreateValidOptions();
		options.AccessTokenLifetimeMinutes = 30;
		JwtTokenFactory sut = CreateSut(options);

		// Act
		string tokenString = sut.CreateToken(TestSubject, []);

		// Assert
		JwtSecurityToken token = ParseToken(tokenString);
		DateTime expectedNotBefore = FixedUtcNow.UtcDateTime;
		DateTime expectedExpires = expectedNotBefore.AddMinutes(30);
		Assert.Equal(expectedNotBefore, token.ValidFrom);
		Assert.Equal(expectedExpires, token.ValidTo);
	}

	// --- 5. Cryptographic integrity ---

	/// <summary>
	/// Verifies that the generated token carries a valid HMAC-SHA256 signature that can be verified
	/// using the same signing key configured in <see cref="JwtOptions"/>.
	/// </summary>
	[Fact]
	public void CreateToken_WithValidInput_ProducesValidSignature()
	{
		// Arrange
		JwtTokenFactory sut = CreateSut();
		var handler = new JwtSecurityTokenHandler();
		byte[] keyBytes = Encoding.UTF8.GetBytes(TestSigningKey);

		var validationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = TestIssuer,
			ValidateAudience = true,
			ValidAudience = TestAudience,
			ValidateLifetime = false, // Lifetime is verified in a dedicated test.
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
		};

		// Act
		string tokenString = sut.CreateToken(TestSubject, []);

		// Assert — ValidateToken() throws if the signature is invalid.
		ClaimsPrincipal principal = handler.ValidateToken(
			tokenString,
			validationParameters,
			out SecurityToken validatedToken);
		Assert.NotNull(principal);

		// Verify the header matches the production signing credential and token type.
		var jwtToken = Assert.IsType<JwtSecurityToken>(validatedToken);
		Assert.Equal(SecurityAlgorithms.HmacSha256, jwtToken.Header.Alg);
		Assert.Equal("JWT", jwtToken.Header.Typ);
	}
}
