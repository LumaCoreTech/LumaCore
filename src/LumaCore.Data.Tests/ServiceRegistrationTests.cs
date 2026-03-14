// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace LumaCore.Data.Tests;

/// <summary>
/// Tests for dependency injection registration and provider selection in <see cref="ServiceRegistration"/>.
/// </summary>
/// <remarks>
/// These tests validate wiring only (registration and option parsing/provider branching).
/// They do not validate connectivity to external database servers.
/// </remarks>
public sealed class ServiceRegistrationTests
{
	/// <summary>
	/// Dummy encryption key satisfying the <c>[Required]</c> / <c>[MinLength(32)]</c> validation
	/// on <see cref="DatabaseOptions.EncryptionKey"/>. Content is irrelevant for DI-wiring tests.
	/// </summary>
	private const string TestEncryptionKey = "01234567890123456789012345678901";

	/// <summary>
	/// Creates a <see cref="ServiceCollection"/> with host-level services that the generic host normally provides
	/// (<see cref="TimeProvider"/>, logging). Raw <see cref="ServiceCollection"/> instances in tests lack these,
	/// so this helper bridges the gap.
	/// </summary>
	private static ServiceCollection CreateServiceCollection()
	{
		var services = new ServiceCollection();
		services.AddSingleton(TimeProvider.System);
		services.AddLogging();
		return services;
	}

	/// <summary>
	/// Test data for <see cref="AddLumaCoreData_WhenValidProvider_RegistersServices"/>.
	/// </summary>
	public static TheoryData<string, string, string> ValidProvider_TestData() => new()
	{
		// SQLite provider-selection branch
		{ "Sqlite", "sqlite", "Data Source=:memory:" },

		// PostgreSQL provider-selection branch
		{ "PostgreSql", "postgresql", "Host=localhost;Database=ignored" },

		// SQL Server provider-selection branch
		{ "SqlServer", "sqlserver", "Server=localhost;Database=ignored;TrustServerCertificate=True" }

		// TODO: Uncomment when Pomelo.EntityFrameworkCore.MySql ships an EF Core 10 compatible version
		// // MySQL/MariaDB provider-selection branch (Pomelo)
		// { "MySql", "mysql", "Server=localhost;Database=ignored" }
	};

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddLumaCoreData"/> registers both the
	/// <see cref="LumaCoreDbContext"/> and <see cref="ILumaCoreDataService"/> for the specified provider.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="provider">The provider identifier to configure.</param>
	/// <param name="connectionString">A syntactically valid connection string for the provider.</param>
	/// <remarks>
	/// These are DI wiring tests: they exercise the provider-selection branch only and do not validate connectivity
	/// to external database servers.
	/// </remarks>
	[Theory]
	[MemberData(nameof(ValidProvider_TestData))]
	public void AddLumaCoreData_WhenValidProvider_RegistersServices(
		string scenario,
		string provider,
		string connectionString)
	{
		_ = scenario;

		// Arrange
		ServiceCollection services = CreateServiceCollection();

		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Database:Provider"] = provider,
					["Database:ConnectionString"] = connectionString,
					["Database:EncryptionKey"] = TestEncryptionKey
				})
			.Build();

		// Act
		services.AddLumaCoreData(configuration);

		// Assert
		using ServiceProvider sp = services.BuildServiceProvider();
		Assert.NotNull(sp.GetRequiredService<LumaCoreDbContext>());
		Assert.NotNull(sp.GetRequiredService<ILumaCoreDataService>());
	}

	// TODO: When Pomelo.EntityFrameworkCore.MySql ships an EF Core 10 compatible version:
	//       1. Add MySQL row to ValidProvider_TestData
	//       2. Delete this Fact entirely
	//       See: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues

	/// <summary>
	/// Verifies that MySQL is recognized but temporarily unavailable until
	/// <c>Pomelo.EntityFrameworkCore.MySql</c> ships an EF Core 10 compatible version.
	/// </summary>
	[Fact]
	public void AddLumaCoreData_WhenMySql_ThrowsUntilPomeloSupportsEfCore10()
	{
		// Arrange
		ServiceCollection services = CreateServiceCollection();

		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Database:Provider"] = "mysql",
					["Database:ConnectionString"] = "Server=localhost;Database=ignored",
					["Database:EncryptionKey"] = TestEncryptionKey
				})
			.Build();

		services.AddLumaCoreData(configuration);
		using ServiceProvider sp = services.BuildServiceProvider();

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<LumaCoreDbContext>());
		Assert.Contains("temporarily unavailable", ex.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Test data for <see cref="AddLumaCoreData_WhenInvalidProvider_ThrowsInvalidOperationException"/>.
	/// </summary>
	public static TheoryData<string, string, string> InvalidProvider_TestData() => new()
	{
		// Completely unknown provider identifier
		{ "Unknown", "nope", "Data Source=:memory:" }
	};

	/// <summary>
	/// Verifies that resolving <see cref="LumaCoreDbContext"/> throws <see cref="InvalidOperationException"/> when
	/// <see cref="DatabaseOptions.Provider"/> is set to an unsupported or unimplemented provider identifier.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="provider">The unsupported provider identifier.</param>
	/// <param name="connectionString">A syntactically valid connection string for the provider.</param>
	[Theory]
	[MemberData(nameof(InvalidProvider_TestData))]
	public void AddLumaCoreData_WhenInvalidProvider_ThrowsInvalidOperationException(
		string scenario,
		string provider,
		string connectionString)
	{
		_ = scenario;

		// Arrange
		ServiceCollection services = CreateServiceCollection();

		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Database:Provider"] = provider,
					["Database:ConnectionString"] = connectionString,
					["Database:EncryptionKey"] = TestEncryptionKey
				})
			.Build();

		services.AddLumaCoreData(configuration);
		using ServiceProvider sp = services.BuildServiceProvider();

		// Act + Assert
		Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<LumaCoreDbContext>());
	}
}
