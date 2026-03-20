// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Tests.Infrastructure;

namespace LumaCore.Data.Tests.Providers;

// Delegates harness creation to the shared IntegrationTestHarness.
// EnsureCreatedAsync is called (default) because provider-operations tests need real tables.
public sealed partial class ProviderOperationsIntegrationTests
{
	/// <summary>
	/// Creates a fresh <see cref="IntegrationTestHarness"/> with the schema pre-created via
	/// <c>EnsureCreatedAsync()</c>.
	/// </summary>
	/// <returns>A disposable harness containing the provider operations and all infrastructure.</returns>
	private static Task<IntegrationTestHarness> CreateHarnessAsync() => IntegrationTestHarness.CreateAsync("provops");
}
