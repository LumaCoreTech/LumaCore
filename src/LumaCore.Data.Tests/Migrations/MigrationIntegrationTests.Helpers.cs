// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore.Migrations;

namespace LumaCore.Data.Tests.Migrations;

// Delegates harness creation to the shared IntegrationTestHarness.
// EnsureCreated is false because migration tests drive schema creation via IMigrator.
public sealed partial class MigrationIntegrationTests
{
	/// <summary>
	/// Creates a fresh <see cref="IntegrationTestHarness"/> with an empty database — schema creation is
	/// driven exclusively through <see cref="IMigrator"/>.
	/// </summary>
	/// <returns>A disposable harness containing the migrator and all infrastructure.</returns>
	private static Task<IntegrationTestHarness> CreateHarnessAsync() =>
		IntegrationTestHarness.CreateAsync("migration", ensureCreated: false);
}
