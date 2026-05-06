// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LumaCore.Data;

/// <summary>
/// Provides a design-time factory for <see cref="LumaCoreDbContext"/> used by Entity Framework Core tools.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this factory exists</b>
///     </para>
///     <para>
///     EF Core CLI commands such as <c>dotnet ef migrations add</c> need a <see cref="LumaCoreDbContext"/> instance
///     to analyze the model. Normally, EF Core would build one through the application's DI container — but that
///     requires the entire hosting pipeline to start (<c>Program.cs</c>, configuration, secrets, database connection).
///     </para>
///     <para>
///     This fails in common scenarios:
///     </para>
///     <list type="bullet">
///         <item><b>CI/CD:</b> No database server available during build.</item>
///         <item><b>New developer:</b> No <c>appsettings.Development.json</c> configured yet.</item>
///         <item>
///         <b>Missing secrets:</b> <see cref="DatabaseOptions.EncryptionKey"/> validation fails → startup crash
///         → migration tooling fails.
///         </item>
///     </list>
///     <para>
///         <b>How it works</b>
///     </para>
///     <para>
///     EF Core automatically discovers classes implementing <see cref="IDesignTimeDbContextFactory{TContext}"/> and
///     uses them instead of the DI container during design-time operations. This factory provides a minimal,
///     deterministic <see cref="LumaCoreDbContext"/> for migration scaffolding — no external database server,
///     no secrets, no configuration required. EF Core does not open a database connection during
///     <c>dotnet ef migrations add</c>; the connection string is never used.
///     </para>
///     <para>
///         <b>Why SQL Server instead of SQLite</b>
///     </para>
///     <para>
///     Every relational EF Core provider emits provider-specific <c>HasColumnType</c> strings into the scaffolded
///     Designer files and the model snapshot — SQLite emits <c>"INTEGER"</c>/<c>"TEXT"</c>/<c>"REAL"</c>, SQL
///     Server emits <c>"bigint"</c>/<c>"nvarchar(max)"</c>/<c>"datetime2"</c>, PostgreSQL emits <c>"bigint"</c>/
///     <c>"text"</c>/<c>"timestamp with time zone"</c>. The snapshot is therefore inherently provider-flavoured.
///     </para>
///     <para>
///     The relevant question is what happens when the scaffolded migration source files (<c>Up</c>/<c>Down</c>
///     methods) are executed against a different provider at runtime. Migrations authored under SQLite tooling
///     liberally emit <c>AlterColumn</c> operations on primary-key columns, because SQLite implements
///     <c>ALTER COLUMN</c> as a transparent table-rebuild with foreign keys temporarily disabled. SQL Server
///     refuses to alter a primary-key column while foreign keys reference it, so the same migration aborts in CI
///     with errors such as <c>FK_Resources_Participants_CreatedByParticipantId</c> failing to apply.
///     </para>
///     <para>
///     Using SQL Server as the design-time provider inverts the asymmetry: the migration differ produces
///     migrations that satisfy the strictest supported provider, and the more permissive providers (SQLite,
///     PostgreSQL) execute them at runtime without complaint. The provider-specific snapshot strings are never
///     read at runtime — only the EF Core tooling and the in-process drift test consume the snapshot, and both
///     run under the design-time provider.
///     </para>
///     <para>
///         <b>Runtime vs. design-time</b>
///     </para>
///     <para>
///     At runtime, <see cref="LumaCoreDbContext"/> is configured through dependency injection in
///     <see cref="ServiceRegistration"/> based on <see cref="DatabaseOptions"/> (supporting SQLite, PostgreSQL,
///     and SQL Server). This factory is <b>never</b> used at runtime — it exists solely for EF Core tooling.
///     </para>
///     <para>
///         <b>Runtime drift detection</b>
///     </para>
///     <para>
///     Because the snapshot is provider-flavoured (SQL Server) but runtime providers may differ, EF Core's
///     built-in <c>RelationalEventId.PendingModelChangesWarning</c> would produce false positives on
///     <c>MigrateAsync()</c> for non-SQL-Server runtime providers. <see cref="ServiceRegistration"/>
///     therefore suppresses that warning at runtime, and the test harness mirrors the suppression so the
///     <c>MigrationIntegrationTests</c> can exercise <c>MigrateAsync()</c> against every supported provider
///     without spurious failures.
///     </para>
///     <para>
///     The trade-off: structural drift that is visible only on a non-SQL-Server provider (for example a
///     change to the SQLite branch of <see cref="LumaCoreDbContext.OnModelCreating"/>'s provider-specific
///     <c>Users.Email</c> filtered unique index) is <b>not</b> caught by any automated test. The
///     <c>NoDrift_LiveModelMatchesLatestSnapshot</c> test catches drift that is visible to the SQL Server
///     differ; the per-provider <c>MigrationIntegrationTests</c> validate that migrations execute against
///     real database engines but do not re-enable the drift check. Provider-specific model branches must
///     be reviewed manually when introduced.
///     </para>
/// </remarks>
sealed class LumaCoreDbContextDesignTimeFactory : IDesignTimeDbContextFactory<LumaCoreDbContext>
{
	/// <summary>
	/// Creates a configured <see cref="LumaCoreDbContext"/> instance for Entity Framework Core design-time operations.
	/// </summary>
	/// <param name="args">
	/// Command-line arguments supplied by the EF Core tooling.
	/// </param>
	/// <returns>
	/// A fully configured <see cref="LumaCoreDbContext"/> instance.
	/// </returns>
	public LumaCoreDbContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<LumaCoreDbContext>();

		// SQL Server is used here intentionally — see the class-level remarks for the full explanation.
		// The connection string is never opened; EF Core only needs the provider to build the model.
		optionsBuilder.UseSqlServer(
			"Server=(local);Database=lumacore-design-time;Trusted_Connection=True;",
			sqlServerOptions =>
			{
				sqlServerOptions.MigrationsAssembly(typeof(LumaCoreDbContext).Assembly.FullName);
			});

		return new LumaCoreDbContext(optionsBuilder.Options);
	}
}
