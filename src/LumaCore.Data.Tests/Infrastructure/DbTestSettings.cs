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
	/// Connection string for external database providers.
	/// </summary>
	public string? ConnectionString { get; init; }

	/// <summary>
	/// When <see langword="true"/>, the database is deleted before initializing the schema.
	/// </summary>
	public bool EnsureDeleted { get; init; }
}
