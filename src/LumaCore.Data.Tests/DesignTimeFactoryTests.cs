// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Reflection;

using Xunit;

namespace LumaCore.Data.Tests;

/// <summary>
/// Tests for the EF Core design-time DbContext factory to ensure tooling scenarios remain functional.
/// </summary>
[Trait("Category", "DbContext")]
public sealed class DesignTimeFactoryTests
{
	/// <summary>
	/// Verifies that the EF Core design-time factory exists and produces a <see cref="LumaCoreDbContext"/> configured
	/// for SQL Server.
	/// </summary>
	/// <remarks>
	/// The factory is typically used only by EF tooling, so the test locates and invokes it via reflection to keep the
	/// usage decoupled from runtime code paths. SQL Server is used as the design-time provider because it is the
	/// strictest of the supported providers — migrations scaffolded against it apply cleanly under the more permissive
	/// providers (SQLite, PostgreSQL) at runtime, while the reverse is not true.
	/// </remarks>
	[Fact]
	public void CreateDbContext_ReturnsConfiguredSqlServerDbContext()
	{
		// Arrange — locate the design-time factory via reflection to avoid a compile-time dependency on an
		// implementation detail that is never referenced by runtime code.
		Type? factoryType = typeof(LumaCoreDbContext)
			.Assembly
			.GetType("LumaCore.Data.LumaCoreDbContextDesignTimeFactory", throwOnError: true);

		object factory = Activator.CreateInstance(factoryType!)!;
		MethodInfo createMethod = factoryType!.GetMethod("CreateDbContext")!;

		// Act
		var context = (LumaCoreDbContext)createMethod.Invoke(factory, [Array.Empty<string>()])!;

		try
		{
			// Assert — the factory defaults to SQL Server so EF tooling scaffolds migrations and the model snapshot
			// against the strictest supported provider.
			Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
		}
		finally
		{
			context.Dispose();
		}
	}
}
