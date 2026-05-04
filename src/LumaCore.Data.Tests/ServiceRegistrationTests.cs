// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;
using LumaCore.Data.Security;
using LumaCore.Data.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests;

/// <summary>
/// Tests for dependency injection registration and provider selection in <see cref="ServiceRegistration"/>.
/// </summary>
/// <remarks>
/// These tests validate wiring only (registration and option parsing/provider branching).
/// They do not validate connectivity to external database servers.
/// </remarks>
[Trait("Category", "DbContext")]
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
		services.AddOptions<StreamBufferPoolOptions>();
		services.AddSingleton<IStreamBufferPool, StreamBufferPool>();
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

	/// <summary>
	/// Verifies that the interface-segregation factory lambdas all resolve to the same scoped
	/// <see cref="ILumaCoreDataService"/> instance. Each focused interface (<see cref="IUserDataService"/>,
	/// <see cref="IRoleDataService"/>, etc.) is a forwarding registration.
	/// </summary>
	[Fact]
	public void AddLumaCoreData_WhenResolved_ProvidesInterfaceSegregatedServices()
	{
		// Arrange
		ServiceCollection services = CreateServiceCollection();

		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Database:Provider"] = "sqlite",
					["Database:ConnectionString"] = "Data Source=:memory:",
					["Database:EncryptionKey"] = TestEncryptionKey
				})
			.Build();

		services.AddLumaCoreData(configuration);
		using ServiceProvider sp = services.BuildServiceProvider();

		// Act — resolve all interface-segregated services within the same scope
		using IServiceScope scope = sp.CreateScope();
		IServiceProvider scoped = scope.ServiceProvider;

		var dataService = scoped.GetRequiredService<ILumaCoreDataService>();
		var userDataService = scoped.GetRequiredService<IUserDataService>();
		var roleDataService = scoped.GetRequiredService<IRoleDataService>();
		var conversationDataService = scoped.GetRequiredService<IConversationDataService>();
		var messageDataService = scoped.GetRequiredService<IMessageDataService>();
		var modelEndpointDataService = scoped.GetRequiredService<IModelEndpointDataService>();
		var dataIntegrityService = scoped.GetRequiredService<IDataIntegrityService>();

		// Assert — all forwarding registrations resolve to the same scoped instance
		Assert.Same(dataService, userDataService);
		Assert.Same(dataService, roleDataService);
		Assert.Same(dataService, conversationDataService);
		Assert.Same(dataService, messageDataService);
		Assert.Same(dataService, modelEndpointDataService);
		Assert.Same(dataService, dataIntegrityService);
	}

	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations"/> and <see cref="IShuttleReaderFactory"/> are
	/// resolvable after calling <see cref="ServiceRegistration.AddLumaCoreData"/>. Exercises the singleton
	/// factory lambda that delegates to <see cref="DatabaseProviderFactory.GetProvider"/>.
	/// </summary>
	[Fact]
	public void AddLumaCoreData_WhenResolved_ProvidesSingletonInfrastructureServices()
	{
		// Arrange
		ServiceCollection services = CreateServiceCollection();

		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Database:Provider"] = "sqlite",
					["Database:ConnectionString"] = "Data Source=:memory:",
					["Database:EncryptionKey"] = TestEncryptionKey
				})
			.Build();

		services.AddLumaCoreData(configuration);
		using ServiceProvider sp = services.BuildServiceProvider();

		// Act
		var providerOps = sp.GetRequiredService<IDatabaseProviderOperations>();
		var shuttleReaderFactory = sp.GetRequiredService<IShuttleReaderFactory>();

		// Assert
		Assert.NotNull(providerOps);
		Assert.Equal(DatabaseProviders.Sqlite, providerOps.ProviderName);
		Assert.NotNull(shuttleReaderFactory);
	}

	/// <summary>
	/// Verifies that the <see cref="DatabaseInitializer"/> singleton and the <see cref="IHostedService"/>
	/// registration resolve to the same instance. This exercises the forwarding factory lambda
	/// <c>sp =&gt; sp.GetRequiredService&lt;DatabaseInitializer&gt;()</c> and ensures
	/// <see cref="DatabaseConnectionMonitorService"/> can reach the initializer through DI.
	/// </summary>
	[Fact]
	public void AddLumaCoreData_WhenResolved_ForwardsDatabaseInitializerToHostedService()
	{
		// Arrange
		ServiceCollection services = CreateServiceCollection();

		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Database:Provider"] = "sqlite",
					["Database:ConnectionString"] = "Data Source=:memory:",
					["Database:EncryptionKey"] = TestEncryptionKey
				})
			.Build();

		services.AddLumaCoreData(configuration);
		using ServiceProvider sp = services.BuildServiceProvider();

		// Act
		var singleton = sp.GetRequiredService<DatabaseInitializer>();
		DatabaseInitializer hostedInitializer = sp.GetServices<IHostedService>().OfType<DatabaseInitializer>().Single();

		// Assert — AddHostedService() forwards to the singleton registration.
		Assert.Same(singleton, hostedInitializer);
	}

	/// <summary>
	/// Verifies that keyed <see cref="ISecretProtector"/> registrations are resolvable
	/// separation produces distinct instances, while the <see cref="SecretProtectorDomains.Default"/> key
	/// forwards to the non-keyed singleton.
	/// </summary>
	[Fact]
	public void AddLumaCoreData_WhenResolved_ProvidesKeyedSecretProtectors()
	{
		// Arrange
		ServiceCollection services = CreateServiceCollection();

		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Database:Provider"] = "sqlite",
					["Database:ConnectionString"] = "Data Source=:memory:",
					["Database:EncryptionKey"] = TestEncryptionKey
				})
			.Build();

		services.AddLumaCoreData(configuration);
		using ServiceProvider sp = services.BuildServiceProvider();

		// Act
		var nonKeyed = sp.GetRequiredService<ISecretProtector>();
		var defaultKeyed = sp.GetRequiredKeyedService<ISecretProtector>(SecretProtectorDomains.Default);
		var endpointKeyed =
			sp.GetRequiredKeyedService<ISecretProtector>(SecretProtectorDomains.ModelEndpointCredentials);
		var apiTokenKeyed =
			sp.GetRequiredKeyedService<ISecretProtector>(SecretProtectorDomains.UserApiTokens);

		// Assert — Default keyed entry forwards to the non-keyed singleton
		Assert.Same(nonKeyed, defaultKeyed);

		// Assert — domain-specific protectors are distinct from the default and from each other
		Assert.NotSame(nonKeyed, endpointKeyed);
		Assert.NotSame(nonKeyed, apiTokenKeyed);
		Assert.NotSame(endpointKeyed, apiTokenKeyed);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddLumaCoreData"/> configures the EF Core provider
	/// correctly even when no <see cref="ILoggerFactory"/> is registered — exercises the
	/// <c>loggerFactory is null</c> branch inside <c>ConfigureProvider()</c>.
	/// </summary>
	/// <remarks>
	/// <see cref="ILoggerFactory"/> is intentionally not registered, but <c>ILogger&lt;T&gt;</c> is
	/// satisfied via <see cref="NullLogger{T}"/> so that other singletons (e.g.,
	/// <see cref="DatabaseConnectionInterceptor"/>) can still be activated by the DI container.
	/// </remarks>
	[Fact]
	public void AddLumaCoreData_WhenNoLoggerFactory_ConfiguresProviderWithoutLogging()
	{
		// Arrange — register ILogger<T> via NullLogger<T> but do NOT register ILoggerFactory,
		// so GetService<ILoggerFactory>() inside ConfigureProvider() returns null.
		var services = new ServiceCollection();
		services.AddSingleton(TimeProvider.System);
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Database:Provider"] = "sqlite",
					["Database:ConnectionString"] = "Data Source=:memory:",
					["Database:EncryptionKey"] = TestEncryptionKey
				})
			.Build();

		services.AddLumaCoreData(configuration);
		using ServiceProvider sp = services.BuildServiceProvider();

		// Act + Assert — resolving the DbContext must not throw when logging is absent
		Assert.NotNull(sp.GetRequiredService<LumaCoreDbContext>());
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
		var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<LumaCoreDbContext>());
		Assert.StartsWith($"Unsupported database provider: '{provider}'.", ex.Message);
	}
}
