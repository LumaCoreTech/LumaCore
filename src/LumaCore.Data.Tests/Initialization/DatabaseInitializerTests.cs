// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

/// <summary>
/// Integration tests for <see cref="DatabaseInitializer"/>.
/// </summary>
/// <remarks>
///     <para>
///     Tests use a real database (via <c>CreateHarness</c>) so that EF Core migrations, raw SQL
///     checkpoints, and seeding run against a real database engine. Locally, this defaults to SQLite
///     file-based. In CI, the same tests execute against PostgreSQL and SQL Server via
///     <c>LUMACORE_TESTS__Db__Provider</c> and <c>LUMACORE_TESTS__Db__ConnectionString</c>.
///     </para>
///     <para>
///     The test class is split across multiple files that follow the initializer's decision tree
///     from first boot to catastrophic failure recovery:
///     </para>
///     <list type="number">
///         <item>
///         <b>StartAsync</b> — General lifecycle: happy path, configuration gates, error classification,
///         seeding, retry mechanics. Covers everything that happens on a <em>new</em> database and the
///         cross-cutting concerns (cancellation, failure counter, escalation) that apply everywhere.
///         </item>
///         <item>
///         <b>HandleUpdateMigrations</b> — The escalation chain when an <em>existing</em> database has
///         pending migrations: simple success → backup → migration failure without backup →
///         migration failure with backup (transient vs. non-transient) → double failure (migration +
///         restore both fail) → checkpoint detection at next startup → edge cases.
///         </item>
///         <item>
///         <b>ResumeRestore</b> — The 6-phase restore pipeline in isolation: schema cleanup → recreation →
///         data import → post-import cleanup. Exercises each phase boundary independently of the
///         migration trigger that normally kicks it off.
///         </item>
///         <item>
///         <b>CleanupOldBackups</b> — Retention-based backup file cleanup: age determination via embedded
///         metadata vs. filesystem timestamp, corrupt-file fallback, and per-file error isolation.
///         </item>
///         <item>
///         <b>Checkpoint</b> — Low-level restore checkpoint operations: read, write, update phase, drop
///         table. These are the building blocks used by HandleUpdateMigrations and ResumeRestore.
///         </item>
///         <item>
///         <b>Helpers</b> — Shared <c>TestHarness</c>, factory methods, assertion helpers. Not tests,
///         but the infrastructure that makes the above files concise.
///         </item>
///     </list>
///     <para>
///     <b>Reading order:</b> Start with <b>StartAsync</b> for the big picture, then
///     <b>HandleUpdateMigrations</b> for the migration-specific decision tree, then
///     <b>ResumeRestore</b> for the restore internals. <b>CleanupOldBackups</b> and
///     <b>Checkpoint</b> can be read independently — they cover isolated utility methods.
///     Each file has a file-level narrative comment that maps its internal test progression.
///     </para>
/// </remarks>
[Trait("Category", "Initialization")]
public sealed partial class DatabaseInitializerTests;
