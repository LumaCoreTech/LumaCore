// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Configuration;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;
using LumaCore.Data.Security;
using LumaCore.Data.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Data;

/// <summary>
/// Provides extension methods for registering database services.
/// </summary>
/// <remarks>
/// Registers <see cref="LumaCoreDbContext"/>, binds <see cref="DatabaseOptions"/>, and wires up
/// <see cref="DatabaseInitializer"/> to handle migrations/seeding during application startup.
/// </remarks>
public static class ServiceRegistration
{
	/// <summary>
	/// Adds LumaCore database services to the dependency injection container.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configuration">The application configuration containing database settings.</param>
	/// <returns>
	/// The service collection for chaining.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// The database provider specified in configuration is not supported.
	/// </exception>
	/// <remarks>
	///     <para>
	///     Reads the <c>Database</c> section from configuration and configures the appropriate EF Core provider.
	///     Supported providers: <c>sqlite</c>, <c>postgresql</c>, <c>sqlserver</c>.
	///     </para>
	///     <para>
	///     Also registers <see cref="DatabaseInitializer"/> as a hosted service, which automatically applies
	///     pending migrations on startup when <c>Database:AutoMigration:Enabled</c> is <see langword="true"/>.
	///     </para>
	/// </remarks>
	public static IServiceCollection AddLumaCoreData(this IServiceCollection services, IConfiguration configuration)
	{
		// Register DatabaseOptions with section tracking for diagnostic endpoints
		services.AddFeatureOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);

		// Shared status flag: set by DatabaseInitializer during startup, read by health checks and the
		// database-not-ready middleware to gate incoming requests until initialization succeeds.
		services.AddSingleton<DatabaseInitializationStatus>();

		// Provider-specific operations (SQL dialect, error code detection, DDL, etc.).
		// Resolved eagerly from configuration so that the singleton is available before the first
		// DbContext is created. Used by DatabaseConnectionInterceptor for service-unavailable detection
		// and by DatabaseInitializer / DataPortService for schema operations and data export/import.
		services.AddSingleton<IDatabaseProviderOperations>(sp =>
		{
			DatabaseOptions options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
			return DatabaseProviderFactory.GetProvider(options.Provider);
		});

		// EF Core interceptor that fires on connection state changes. Singleton because a single instance
		// tracks connectivity across all scoped DbContext instances and signals the monitor service.
		services.AddSingleton<DatabaseConnectionInterceptor>();

		// Register the DbContext with provider-specific configuration. The interceptor is added here to ensure
		// it is active for all DbContext instances, allowing it to detect runtime disconnects and trigger
		// re-initialization.
		services.AddDbContext<LumaCoreDbContext>((serviceProvider, dbContextOptions) =>
		{
			DatabaseOptions options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
			ConfigureProvider(dbContextOptions, options, serviceProvider);

			// Add the connection interceptor to detect runtime disconnections
			dbContextOptions.AddInterceptors(serviceProvider.GetRequiredService<DatabaseConnectionInterceptor>());
		});

		// Factory for creating shuttle readers during backup validation and restore operations.
		// Singleton because the factory itself holds no state — each Create() call returns a new,
		// independent reader with its own SQLite connection.
		services.AddSingleton<IShuttleReaderFactory, SqliteShuttleReaderFactory>();

		// DatabaseInitializer runs migrations/seeding on startup. It must be a singleton (not just a hosted
		// service) because DatabaseConnectionMonitorService needs the same instance to trigger re-initialization
		// after a runtime disconnect. The AddHostedService() overload forwards to the singleton.
		services.AddSingleton<DatabaseInitializer>();
		services.AddHostedService<DatabaseInitializer>(sp => sp.GetRequiredService<DatabaseInitializer>());

		// Background self-healing loop: periodically checks connectivity and re-runs initialization if the
		// database went away (runtime disconnect) or if the initial startup migration failed.
		services.AddHostedService<DatabaseConnectionMonitorService>();

		// High-level database API for common use-cases (privacy-first policies, deletion/redaction, etc.)
		services.AddScoped<ILumaCoreDataService, LumaCoreDataService>();
		services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();
		services.AddScoped<DataPortService>();

		// Interface segregation: each focused interface resolves to the same scoped LumaCoreDataService instance.
		// Consumers depend only on the slice they need (e.g., IUserDataService for user CRUD).
		services.AddScoped<IUserDataService>(sp => sp.GetRequiredService<ILumaCoreDataService>());
		services.AddScoped<IRoleDataService>(sp => sp.GetRequiredService<ILumaCoreDataService>());
		services.AddScoped<IConversationDataService>(sp => sp.GetRequiredService<ILumaCoreDataService>());
		services.AddScoped<IMessageDataService>(sp => sp.GetRequiredService<ILumaCoreDataService>());
		services.AddScoped<IModelEndpointDataService>(sp => sp.GetRequiredService<ILumaCoreDataService>());
		services.AddScoped<IDataIntegrityService>(sp => sp.GetRequiredService<ILumaCoreDataService>());

		// Resource file storage: options + local filesystem implementation.
		// Singleton because the store holds only the resolved root path and a logger — no scoped state.
		services.AddFeatureOptions<ResourceStoreOptions>(configuration, ResourceStoreOptions.SectionName);
		services.AddSingleton<IResourceStore, LocalFileResourceStore>();

		// Resource service: orchestrates upload (hashing, dedup, storage), download info resolution,
		// and pre-CASCADE reference cleanup. Scoped because it depends on LumaCoreDbContext.
		services.AddScoped<IResourceService, ResourceService>();

		// Resource garbage collector: MARK orphaned resources → SWEEP (file delete + DB row delete).
		// Runs as a background service with configurable interval, grace period, and batch size.
		services.AddFeatureOptions<ResourceCleanupOptions>(configuration, ResourceCleanupOptions.SectionName);
		services.AddHostedService<ResourceCleanupService>();

		// Secret protection uses AES-GCM with HKDF domain separation. All protectors share the same
		// EncryptionKey from configuration but derive cryptographically independent AES keys per domain.
		// Compromising one domain does not affect secrets protected under another.
		services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();

		// Keyed registrations allow consumers to inject a domain-specific protector via [FromKeyedServices].
		// The Default keyed entry delegates to the non-keyed singleton above for backward compatibility.
		services.AddKeyedSingleton<ISecretProtector>(
			SecretProtectorDomains.Default,
			(sp, _) => sp.GetRequiredService<ISecretProtector>());

		services.AddKeyedSingleton<ISecretProtector>(
			SecretProtectorDomains.ModelEndpointCredentials,
			(sp, _) => new AesGcmSecretProtector(
				sp.GetRequiredService<IOptions<DatabaseOptions>>(),
				SecretProtectorDomains.ModelEndpointCredentials));

		services.AddKeyedSingleton<ISecretProtector>(
			SecretProtectorDomains.UserApiTokens,
			(sp, _) => new AesGcmSecretProtector(
				sp.GetRequiredService<IOptions<DatabaseOptions>>(),
				SecretProtectorDomains.UserApiTokens));

		return services;
	}

	/// <summary>
	/// Configures the EF Core provider based on the database options.
	/// </summary>
	/// <param name="dbContextOptions">The DbContext options builder.</param>
	/// <param name="options">The database configuration options.</param>
	/// <param name="serviceProvider">The service provider for resolving dependencies.</param>
	/// <exception cref="InvalidOperationException">The provider is not supported.</exception>
	private static void ConfigureProvider(
		DbContextOptionsBuilder dbContextOptions,
		DatabaseOptions         options,
		IServiceProvider        serviceProvider)
	{
		var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

		if (loggerFactory is not null)
		{
			dbContextOptions.UseLoggerFactory(loggerFactory);
		}

		// Suppress PendingModelChangesWarning at runtime. The migration snapshot is intentionally
		// scaffolded under SQL Server (see LumaCoreDbContextDesignTimeFactory) because it is the
		// strictest supported provider for ALTER COLUMN semantics. Provider-specific annotations
		// (Sqlite:Autoincrement, SqlServer:Identity, Npgsql:ValueGenerationStrategy, HasColumnType
		// strings) are baked into the snapshot at scaffold time, so the runtime differ inevitably
		// reports false-positive drift whenever the runtime provider differs from the design-time
		// provider — which is the normal case for SQLite and PostgreSQL deployments. Letting the
		// warning escalate to an exception would block MigrateAsync() on every non-SQL-Server
		// runtime, even when the model is structurally identical to the snapshot.
		//
		// Drift detection is delegated to MigrationIntegrationTests.NoDrift_LiveModelMatchesLatest-
		// Snapshot, which runs the differ in-memory under SQL Server (matching the snapshot's
		// provider). Trade-off: drift visible only on non-SQL-Server providers (e.g. inside a
		// provider-specific branch of OnModelCreating) is NOT caught automatically and must be
		// reviewed manually — see LumaCoreDbContextDesignTimeFactory's "Runtime drift detection"
		// remarks for the full rationale.
		dbContextOptions.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

		string provider = options.Provider.ToLowerInvariant();

		switch (provider)
		{
			case DatabaseProviders.Sqlite:
				dbContextOptions.UseSqlite(
					options.ConnectionString,
					sqliteOptions =>
					{
						sqliteOptions.MigrationsAssembly(typeof(LumaCoreDbContext).Assembly.FullName);
					});
				break;

			case DatabaseProviders.PostgreSql:
				dbContextOptions.UseNpgsql(
					options.ConnectionString,
					npgsqlOptions =>
					{
						npgsqlOptions.MigrationsAssembly(typeof(LumaCoreDbContext).Assembly.FullName);
					});
				break;

			// TODO: Re-enable MySQL/MariaDB support once an EF Core 10 compatible provider is available (e.g. Pomelo.EntityFrameworkCore.MySql)
			case DatabaseProviders.MySql:
				throw new InvalidOperationException(
					"MySQL/MariaDB support is temporarily unavailable. " +
					"Pomelo.EntityFrameworkCore.MySql has not yet released an EF Core 10 compatible version. " +
					"Please use SQLite, PostgreSQL, or SQL Server instead, or track progress at: " +
					"https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues");

			case DatabaseProviders.SqlServer:
				dbContextOptions.UseSqlServer(
					options.ConnectionString,
					sqlServerOptions =>
					{
						sqlServerOptions.MigrationsAssembly(typeof(LumaCoreDbContext).Assembly.FullName);
					});
				break;

			default:
				throw new InvalidOperationException(
					$"Unsupported database provider: '{options.Provider}'. " +
					$"Supported providers: {DatabaseProviders.GetSupportedProviders()}.");
		}
	}
}
