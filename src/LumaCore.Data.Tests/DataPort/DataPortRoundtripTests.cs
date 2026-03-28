// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;
using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Integration tests for the full DataPort roundtrip: seed a source database with real EF Core entities,
/// export it to a <c>.shuttle.sqlite</c> file via <see cref="DataPortService.RunExportAsync"/>,
/// import the shuttle into a fresh target database via <see cref="DataPortService.RunImportAsync"/>,
/// and verify that all data survived the trip.
/// </summary>
/// <remarks>
///     <para>
///     These tests run against the database configured via <c>DbTestSettingsLoader</c> (SQLite in-memory by
///     default, switchable to file-based SQLite, PostgreSQL, or SQL Server). The export reader and import
///     writer are obtained from <see cref="IDatabaseProviderOperations.CreateExportReader"/> and
///     <see cref="IDatabaseProviderOperations.CreateImportWriter"/>, so the test exercises the
///     exact same code paths as a production export/import.
///     </para>
///     <para>
///     The test class is split across multiple files:
///     </para>
///     <list type="number">
///         <item>
///         <b>ExportImport</b> — The roundtrip test: migrate, seed, export, import, verify.
///         </item>
///         <item>
///         <b>Helpers</b> — Harness factories, seed data helpers, and verification helpers.
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "DataPort")]
public sealed partial class DataPortRoundtripTests;
