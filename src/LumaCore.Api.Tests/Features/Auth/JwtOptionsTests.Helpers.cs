// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;

namespace LumaCore.Api.Tests.Features.Auth;

public sealed partial class JwtOptionsTests
{
	/// <summary>
	/// Creates a fully populated <see cref="JwtOptions"/> instance that passes all data-annotation validations.
	/// </summary>
	/// <returns>A valid <see cref="JwtOptions"/> instance suitable for mutation in validation tests.</returns>
	private static JwtOptions CreateValidOptions() => new()
	{
		Issuer = "TestIssuer",
		Audience = "TestAudience",
		SigningKey = "ThisIsATestSigningKeyThatIs32Ch!",
	};
}
