// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Reflection;

using Xunit;

namespace LumaCore.Data.Tests;

/// <summary>
/// Tests for the EF Core design-time DbContext factory to ensure tooling scenarios remain functional.
/// </summary>
public sealed class DesignTimeFactoryTests
{
	/// <summary>
	/// Verifies that the EF Core design-time factory exists and produces a <see cref="LumaCoreDbContext"/> configured
	/// for SQLite.
	/// </summary>
	/// <remarks>
	/// The factory is typically used only by EF tooling, so the test locates and invokes it via reflection to keep the
	/// usage decoupled from runtime code paths.
	/// </remarks>
	[Fact]
	public void CreateDbContext_ReturnsConfiguredSqliteDbContext()
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
			// Assert — the factory defaults to SQLite so EF tooling can work without external infrastructure.
			Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
		}
		finally
		{
			context.Dispose();
		}
	}
}
