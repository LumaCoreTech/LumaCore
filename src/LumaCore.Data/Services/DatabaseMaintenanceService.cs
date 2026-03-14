// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Data.Services;

/// <summary>
/// Default implementation of <see cref="IDatabaseMaintenanceService"/>.
/// </summary>
public sealed class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
	private readonly ILogger<DatabaseMaintenanceService> mLogger;
	private readonly DatabaseOptions                     mOptions;
	private readonly DataPortService                     mDataPortService;
	private readonly TimeProvider                        mTimeProvider;
	private readonly IDatabaseProviderOperations         mProviderOperations;

	/// <summary>
	/// Initializes a new instance of the <see cref="DatabaseMaintenanceService"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">The database configuration options.</param>
	/// <param name="dataPortService">The data port service for export/import operations.</param>
	/// <param name="providerOperations">The database provider operations for creating export readers.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	public DatabaseMaintenanceService(
		ILogger<DatabaseMaintenanceService> logger,
		IOptions<DatabaseOptions>           options,
		DataPortService                     dataPortService,
		IDatabaseProviderOperations         providerOperations,
		TimeProvider                        timeProvider)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(dataPortService);
		ArgumentNullException.ThrowIfNull(providerOperations);
		ArgumentNullException.ThrowIfNull(timeProvider);

		mLogger = logger;
		mOptions = options.Value;
		mDataPortService = dataPortService;
		mProviderOperations = providerOperations;
		mTimeProvider = timeProvider;
	}

	/// <inheritdoc/>
	public async Task<string> CreateShuttleBackupAsync(CancellationToken cancellationToken = default)
	{
		string backupDirectory = GetBackupDirectory(mOptions);
		Directory.CreateDirectory(backupDirectory);

		// Include milliseconds and a short random suffix to prevent filename collisions in
		// fast restart loops or scale-out scenarios where multiple instances start simultaneously.
		string timestamp = mTimeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss-fff");
		string suffix = Guid.NewGuid().ToString("N")[..8];
		string backupPath = Path.Combine(
			backupDirectory,
			$"lumacore-{timestamp}-{suffix}{SqliteShuttleSchema.FileExtension}");

		mLogger.LogInformation("Creating shuttle backup: {BackupPath}", backupPath);

		IDataExportReader exportReader = mProviderOperations.CreateExportReader(mOptions, mLogger);
		try
		{
			var shuttleWriter = new SqliteShuttleWriter(backupPath, mLogger, mTimeProvider);
			try
			{
				await mDataPortService
					.RunExportAsync(exportReader, shuttleWriter, progress: null, cancellationToken)
					.ConfigureAwait(false);
			}
			finally
			{
				await shuttleWriter.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await exportReader.DisposeAsync().ConfigureAwait(false);
		}

		mLogger.LogInformation("Shuttle backup created successfully");
		return backupPath;
	}

	/// <summary>
	/// Resolves the absolute backup directory path from the configured options.
	/// </summary>
	/// <param name="options">The database options containing the backup directory setting.</param>
	/// <returns>The absolute path to the backup directory.</returns>
	/// <remarks>
	///     <para>
	///     This is the single source of truth for backup directory resolution, used by both
	///     <see cref="CreateShuttleBackupAsync"/> (backup creation) and
	///     <see cref="DatabaseInitializer"/> (backup cleanup). It handles three scenarios:
	///     </para>
	///     <list type="number">
	///         <item>Configured absolute path — used as-is</item>
	///         <item>Configured relative path — resolved against <see cref="AppContext.BaseDirectory"/></item>
	///         <item>No path configured — defaults to <c>{TempPath}/LumaCore/backups</c></item>
	///     </list>
	/// </remarks>
	internal static string GetBackupDirectory(DatabaseOptions options)
	{
		if (!string.IsNullOrWhiteSpace(options.AutoMigration.BackupDirectory))
		{
			string path = options.AutoMigration.BackupDirectory;

			// If already absolute, use as-is.
			if (Path.IsPathRooted(path))
				return path;

			// Otherwise resolve relative to application base directory.
			return Path.Combine(AppContext.BaseDirectory, path);
		}

		return Path.Combine(Path.GetTempPath(), "LumaCore", "backups");
	}
}
