// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Security;
using LumaCore.Data.Services;

using Microsoft.Extensions.Options;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Factory for creating <see cref="LumaCoreDataService"/> instances with sensible defaults for test scenarios.
/// </summary>
static class LumaCoreDataServiceFactory
{
	/// <summary>
	/// Creates a new <see cref="LumaCoreDataService"/> backed by the specified <paramref name="dbContext"/> with
	/// default <see cref="DatabaseOptions"/> and a test-only encryption key.
	/// </summary>
	/// <param name="dbContext">The EF Core context to use for data access.</param>
	/// <param name="configure">
	/// An optional callback to override specific <see cref="DatabaseOptions"/> properties for targeted branch
	/// testing.
	/// </param>
	/// <returns>A fully configured <see cref="LumaCoreDataService"/> instance ready for testing.</returns>
	public static LumaCoreDataService Create(LumaCoreDbContext dbContext, Action<DatabaseOptions>? configure = null)
	{
		// Special test helper: most tests don't care about DatabaseOptions, so we start from defaults.
		// The optional configure callback allows targeted branch testing for option-driven behavior.
		var options = new DatabaseOptions
		{
			EncryptionKey = "DEV-ONLY-CHANGE-THIS-TO-A-LONG-RANDOM-SECRET-STRING"
		};
		configure?.Invoke(options);
		var protector = new AesGcmSecretProtector(Options.Create(options));
		return new LumaCoreDataService(dbContext, Options.Create(options), protector, TimeProvider.System);
	}
}
