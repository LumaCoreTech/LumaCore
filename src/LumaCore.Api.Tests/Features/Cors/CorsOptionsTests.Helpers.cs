// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Cors;

namespace LumaCore.Api.Tests.Features.Cors;

public sealed partial class CorsOptionsTests
{
	/// <summary>
	/// Creates a fully populated <see cref="CorsOptions"/> instance that passes all data-annotation and
	/// <see cref="CorsOptions.Validate"/> validations.
	/// </summary>
	/// <returns>A valid <see cref="CorsOptions"/> instance suitable for mutation in validation tests.</returns>
	private static CorsOptions CreateValidOptions() => new()
	{
		Enabled = true,
		AllowCredentials = true,
		AllowedOrigins = ["https://example.com"],
		AllowedMethods = ["GET", "POST"],
		AllowedHeaders = ["Content-Type", "Authorization"],
		ExposedHeaders = ["X-Request-Id"],
		PreflightMaxAge = 3600,
	};
}
