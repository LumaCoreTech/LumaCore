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
///     deterministic <see cref="LumaCoreDbContext"/> backed by a local SQLite file — no external database server,
///     no secrets, no configuration required.
///     </para>
///     <para>
///         <b>Runtime vs. design-time</b>
///     </para>
///     <para>
///     At runtime, <see cref="LumaCoreDbContext"/> is configured through dependency injection in
///     <see cref="ServiceRegistration"/> based on <see cref="DatabaseOptions"/> (supporting SQLite, PostgreSQL,
///     SQL Server, and MySQL). This factory is <b>never</b> used at runtime — it exists solely for EF Core tooling.
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

		optionsBuilder.UseSqlite(
			"Data Source=lumacore.design-time.db",
			sqliteOptions =>
			{
				sqliteOptions.MigrationsAssembly(typeof(LumaCoreDbContext).Assembly.FullName);
			});

		return new LumaCoreDbContext(optionsBuilder.Options);
	}
}
