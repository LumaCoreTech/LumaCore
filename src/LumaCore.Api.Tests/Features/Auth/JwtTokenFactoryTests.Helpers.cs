// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.IdentityModel.Tokens.Jwt;

using LumaCore.Api.Features.Auth;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LumaCore.Api.Tests.Features.Auth;

public sealed partial class JwtTokenFactoryTests
{
	/// <summary>
	/// The issuer value used across all tests.
	/// </summary>
	private const string TestIssuer = "TestIssuer";

	/// <summary>
	/// The audience value used across all tests.
	/// </summary>
	private const string TestAudience = "TestAudience";

	/// <summary>
	/// A 32-character signing key that satisfies the <see cref="JwtOptions.SigningKey"/> minimum length requirement.
	/// </summary>
	private const string TestSigningKey = "ThisIsATestSigningKeyThatIs32Ch!";

	/// <summary>
	/// The subject (user identity) used across all tests.
	/// </summary>
	private const string TestSubject = "testuser";

	/// <summary>
	/// The fixed point in time used by the <see cref="FakeTimeProvider"/> across all tests.
	/// </summary>
	private static readonly DateTimeOffset FixedUtcNow = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

	/// <summary>
	/// Creates a <see cref="JwtOptions"/> instance with all properties set to valid test values.
	/// </summary>
	private static JwtOptions CreateValidOptions() => new()
	{
		Issuer = TestIssuer,
		Audience = TestAudience,
		SigningKey = TestSigningKey,
		AccessTokenLifetimeMinutes = 60
	};

	/// <summary>
	/// Creates a <see cref="JwtTokenFactory"/> using the specified <paramref name="options"/> (or valid defaults)
	/// and a <see cref="FakeTimeProvider"/> fixed at <see cref="FixedUtcNow"/>.
	/// </summary>
	/// <param name="options">
	/// Optional <see cref="JwtOptions"/> to use. When <see langword="null"/>, <see cref="CreateValidOptions"/>
	/// is used.
	/// </param>
	private static JwtTokenFactory CreateSut(JwtOptions? options = null)
	{
		JwtOptions effectiveOptions = options ?? CreateValidOptions();
		var timeProvider = new FakeTimeProvider(FixedUtcNow);
		return new JwtTokenFactory(Options.Create(effectiveOptions), timeProvider);
	}

	/// <summary>
	/// Parses a serialized JWT string into a <see cref="JwtSecurityToken"/> for assertion.
	/// </summary>
	/// <param name="tokenString">The serialized JWT to parse.</param>
	private static JwtSecurityToken ParseToken(string tokenString)
	{
		var handler = new JwtSecurityTokenHandler();
		return handler.ReadJwtToken(tokenString);
	}
}
