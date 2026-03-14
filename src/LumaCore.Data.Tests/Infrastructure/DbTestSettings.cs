// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Settings used by data tests to select and configure the database provider.
/// </summary>
public sealed class DbTestSettings
{
	/// <summary>
	/// The database provider to use.
	/// </summary>
	public DbProvider Provider { get; init; } = DbProvider.SqliteInMemory;

	/// <summary>
	/// Connection string providing transport and authentication for external database providers
	/// (host, port, credentials, TLS settings, etc.).
	/// </summary>
	/// <remarks>
	/// This connection string must <b>not</b> include a database name — the database is derived from
	/// <see cref="DatabasePrefix"/> instead. <see cref="DbFixture"/> combines the prefix with a GUID suffix
	/// (e.g., <c>lumacore_test_a1b2c3…</c>) and injects the resulting name into the connection before opening it.
	/// This keeps the configured value honest: it describes <em>how to reach the server</em>, nothing more.
	/// </remarks>
	public string? ConnectionString { get; init; }

	/// <summary>
	/// Prefix for the unique per-fixture database name used by external providers.
	/// </summary>
	/// <remarks>
	/// <see cref="DbFixture"/> appends <c>_{GUID}</c> to this prefix so that every test class gets an isolated
	/// database and can run in parallel (e.g., <c>lumacore_test_a1b2c3…</c>). The fixture drops the database on
	/// disposal (best-effort).
	/// </remarks>
	public string DatabasePrefix { get; init; } = "lumacore_test";
}
