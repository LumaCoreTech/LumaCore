// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

/// <summary>
/// Integration tests for <see cref="IDatabaseProviderOperations"/> methods that execute provider-specific SQL.
/// </summary>
/// <remarks>
///     <para>
///     Tests run against the database configured via <c>DbTestSettingsLoader</c>: SQLite in-memory (default)
///     or file-based locally, PostgreSQL or SQL Server in CI (via <c>LUMACORE_TESTS__Db__Provider</c> and
///     <c>LUMACORE_TESTS__Db__ConnectionString</c>). This validates that provider-specific SQL dialects
///     (identifier quoting, schema qualification, <c>information_schema</c> queries) work correctly against
///     real database engines.
///     </para>
///     <para>
///     The test class is split across multiple files:
///     </para>
///     <list type="number">
///         <item>
///         <b>TableExistsAsync</b> — Metadata queries against real catalog tables.
///         </item>
///         <item>
///         <b>Checkpoint</b> — Full checkpoint lifecycle: create table, write, read, update, drop.
///         </item>
///         <item>
///         <b>DropSchemaObjectsAsync</b> — Schema cleanup with and without preserved tables.
///         </item>
///         <item>
///         <b>Helpers</b> — Shared <c>TestHarness</c> and factory method.
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Providers")]
public sealed partial class ProviderOperationsIntegrationTests;
