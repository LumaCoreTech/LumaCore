// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Configuration;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Loads <see cref="DbTestSettings"/> from configuration sources to allow tests to run against different
/// database providers without code changes.
/// </summary>
/// <remarks>
///     <para>
///     Configuration is resolved in the following priority order (last wins):
///     </para>
///     <list type="number">
///         <item>
///         <c>appsettings.json</c> — Committed to the repository with <c>sqlitememory</c> as default and all
///         other providers as commented-out templates. Developers switch providers by uncommenting.
///         </item>
///         <item><c>appsettings.Development.json</c> (optional, in test output directory)</item>
///         <item>
///         Environment variables with prefix <c>LUMACORE_TESTS__</c>
///         (e.g., <c>LUMACORE_TESTS__Db__Provider</c>) — used in CI to select the target provider.
///         </item>
///     </list>
///     <para>
///     Supported keys under <c>Db:</c>:
///     </para>
///     <list type="bullet">
///         <item>
///         <c>Provider</c> — provider name (<c>sqlite</c>, <c>sqlitememory</c>, <c>postgresql</c>,
///         <c>sqlserver</c>, <c>mysql</c>)
///         </item>
///         <item>
///         <c>ConnectionString</c> — transport/auth only (host, port, credentials); must <b>not</b> include a
///         database name
///         </item>
///         <item>
///         <c>DatabasePrefix</c> — prefix for the per-fixture database name (default: <c>lumacore_test</c>)
///         </item>
///     </list>
///     <para>
///     When no configuration is provided (no <c>Db:Provider</c> key), defaults to
///     <see cref="DbProvider.SqliteInMemory"/> for fast, hermetic unit tests. Use <c>sqlite</c> to match
///     production file-based behavior.
///     </para>
/// </remarks>
static class DbTestSettingsLoader
{
	/// <summary>
	/// Loads test database settings from configuration files and environment variables.
	/// </summary>
	/// <returns>A <see cref="DbTestSettings"/> instance reflecting the resolved configuration.</returns>
	public static DbTestSettings Load()
	{
		// Special: Tests can run against different providers (e.g. in CI) without code changes.
		// Default is sqlite in-memory for fast, hermetic unit tests.
		IConfigurationRoot configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", optional: true)
			.AddJsonFile("appsettings.Development.json", optional: true)
			.AddEnvironmentVariables(prefix: "LUMACORE_TESTS__")
			.Build();

		// Note: environment vars use prefix LUMACORE_TESTS__ (see AddEnvironmentVariables above).
		string provider = configuration["Db:Provider"]?.Trim().ToLowerInvariant() ?? "sqlitememory";
		string? connectionString = configuration["Db:ConnectionString"];
		string? databasePrefix = configuration["Db:DatabasePrefix"];

		return new DbTestSettings
		{
			Provider = provider switch
			{
				"sqlite"       => DbProvider.Sqlite,
				"sqlitememory" => DbProvider.SqliteInMemory,
				"postgresql"   => DbProvider.PostgreSql,
				"sqlserver"    => DbProvider.SqlServer,
				"mysql"        => DbProvider.MySql,
				var _          => DbProvider.SqliteInMemory
			},
			ConnectionString = connectionString,
			DatabasePrefix = databasePrefix ?? "lumacore_test"
		};
	}
}
