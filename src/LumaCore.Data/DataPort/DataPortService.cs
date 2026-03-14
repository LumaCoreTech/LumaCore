// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.DataPort.Models;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging;

namespace LumaCore.Data.DataPort;

/// <summary>
/// Orchestrates the entire data porting (export/import) process.<br/>
/// It uses a reader (for the source DB), a writer (for the shuttle file), and an importer (for the target DB)
/// to perform safe, schema-verified, high-speed data transfers.
/// </summary>
public class DataPortService
{
	/// <summary>
	/// Metadata key that identifies which database provider was used as the export source.
	/// </summary>
	internal const string SourceProviderKey = "SourceProvider";

	private readonly ILogger<DataPortService> mLogger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataPortService"/> class.
	/// </summary>
	/// <param name="logger">
	/// The logger instance used to log diagnostic messages and errors.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
	public DataPortService(ILogger<DataPortService> logger)
	{
		ArgumentNullException.ThrowIfNull(logger);
		mLogger = logger;
	}

	/// <summary>
	/// Runs the complete export process from a source database to a <c>.shuttle.sqlite</c> file.
	/// </summary>
	/// <param name="sourceReader">The reader implementation configured with the source database connection.</param>
	/// <param name="shuttleWriter">The writer implementation configured with the target <c>.shuttle.sqlite</c> file path.</param>
	/// <param name="progress">An optional progress reporter to receive progress updates.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="sourceReader"/> or <paramref name="shuttleWriter"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	/// This method does not take ownership of <paramref name="sourceReader"/> or <paramref name="shuttleWriter"/>.
	/// The caller is responsible for disposing them (via <c>try/finally</c> with explicit
	/// <see cref="IAsyncDisposable.DisposeAsync"/>) even when this method throws.
	/// </remarks>
	public async Task RunExportAsync(
		IDataExportReader                  sourceReader,
		IShuttleWriter                     shuttleWriter,
		IProgress<DataPortProgressReport>? progress          = null,
		CancellationToken                  cancellationToken = default)
	{
		// Validate parameters.
		ArgumentNullException.ThrowIfNull(sourceReader);
		ArgumentNullException.ThrowIfNull(shuttleWriter);

		try
		{
			// Log start of export.
			mLogger.LogInformation(
				"Starting export from {SourceType} to {ShuttleType}...",
				sourceReader.GetType().Name,
				shuttleWriter.GetType().Name);

			// Send initial progress report.
			progress?.Report(
				new DataPortProgressReport
				{
					OverallMessage = "Initializing export..."
				});

			// Initialize both reader and writer
			await sourceReader.InitializeAsync(cancellationToken).ConfigureAwait(false);
			await shuttleWriter.InitializeAsync(cancellationToken).ConfigureAwait(false);
			mLogger.LogInformation("Export reader and shuttle writer initialized");

			// Write Metadata (e.g., source provider).
			// The creation timestamp (CreatedUtc) is written automatically by the writer during
			// FinalizeAsync(), so it is not included here.
			var metadata = new Dictionary<string, string>
			{
				{ SourceProviderKey, sourceReader.GetType().Name }
			};
			await shuttleWriter.WriteMetadataAsync(metadata, cancellationToken).ConfigureAwait(false);

			// Write Migration History (for schema check on import)
			mLogger.LogDebug("Exporting migration history...");
			List<MigrationInfo> migrations = await sourceReader
				                                 .GetMigrationHistoryAsync(cancellationToken)
				                                 .ConfigureAwait(false);
			await shuttleWriter.WriteMigrationHistoryAsync(migrations, cancellationToken).ConfigureAwait(false);
			mLogger.LogInformation("Exported {ExportedMigrationCount} migration entries", migrations.Count);

			// Get list of tables to export.
			List<string> tablesToExport = await sourceReader
				                              .GetTableNamesAsync(cancellationToken)
				                              .ConfigureAwait(false);
			mLogger.LogInformation("Found {ExportTableCount} tables to export", tablesToExport.Count);
			int totalTables = tablesToExport.Count;
			int overallTotalSteps = totalTables + 2;

			// Start timing.
			var overallStopwatch = Stopwatch.StartNew();
			var stopWatch = Stopwatch.StartNew();
			int currentStep = 0;

			// Stream-export each table.
			foreach (string tableName in tablesToExport)
			{
				cancellationToken.ThrowIfCancellationRequested();
				currentStep++;

				// Log table export start.
				mLogger.LogInformation("Exporting table {TableName}...", tableName);

				// Restart timing for this table.
				stopWatch.Restart();

				// Report overall progress (starting table X)
				progress?.Report(
					new DataPortProgressReport
					{
						OverallMessage = $"Exporting table {tableName} ({currentStep}/{totalTables})...",
						OverallTotalSteps = overallTotalSteps,
						OverallCurrentStep = currentStep
					});

				// The reader creates the stream.
				TableSnapshot snapshot = await sourceReader
					                         .ReadTableAsync(tableName, cancellationToken)
					                         .ConfigureAwait(false);

				// The writer consumes the stream and reports detailed row progress.
				await shuttleWriter.WriteTableAsync(
						snapshot,
						mLogger,
						progress,          // Pass the handler down
						currentStep,       // Pass the current step
						overallTotalSteps, // Pass the total
						cancellationToken)
					.ConfigureAwait(false);

				// Log timing.
				stopWatch.Stop();
				mLogger.LogInformation(
					"Exporting table {TableName}... DONE in {ElapsedMilliseconds}ms",
					tableName,
					stopWatch.ElapsedMilliseconds);
			}

			// Report that finalization is starting (not yet 100%).
			progress?.Report(
				new DataPortProgressReport
				{
					OverallMessage = "Finalizing export...",
					OverallTotalSteps = overallTotalSteps,
					OverallCurrentStep = totalTables + 1
				});

			// Finalize the shuttle file (e.g., run integrity check)
			mLogger.LogInformation("Finalizing shuttle file...");
			await shuttleWriter.FinalizeAsync(cancellationToken).ConfigureAwait(false);

			overallStopwatch.Stop();
			mLogger.LogInformation(
				"Export completed successfully in {ElapsedMs}ms",
				overallStopwatch.ElapsedMilliseconds);

			// Signal completion at 100%.
			progress?.Report(
				new DataPortProgressReport
				{
					OverallMessage = "Export completed",
					OverallTotalSteps = overallTotalSteps,
					OverallCurrentStep = overallTotalSteps
				});
		}
		catch (OperationCanceledException)
		{
			mLogger.LogWarning("Export was cancelled");
			throw; // Re-throw for the caller to handle
		}
		catch (Exception ex)
		{
			mLogger.LogError(
				ex,
				"Fatal export error — the caller must dispose the reader/writer to release resources");

			throw;
		}
	}

	/// <summary>
	/// Runs the complete import process from a shuttle file to a target database.
	/// </summary>
	/// <param name="shuttleReader">The reader implementation configured with the <c>.shuttle.sqlite</c> file.</param>
	/// <param name="targetImporter">The importer implementation configured with the target database connection.</param>
	/// <param name="progress">An optional progress reporter to receive progress updates.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="shuttleReader"/> or <paramref name="targetImporter"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	/// This method does not take ownership of <paramref name="shuttleReader"/> or <paramref name="targetImporter"/>.
	/// The caller is responsible for disposing them (via <c>try/finally</c> with explicit
	/// <see cref="IAsyncDisposable.DisposeAsync"/>) even when this method throws.
	/// </remarks>
	public async Task RunImportAsync(
		IShuttleReader                     shuttleReader,
		IDataImportWriter                  targetImporter,
		IProgress<DataPortProgressReport>? progress          = null,
		CancellationToken                  cancellationToken = default)
	{
		// Validate parameters.
		ArgumentNullException.ThrowIfNull(shuttleReader);
		ArgumentNullException.ThrowIfNull(targetImporter);

		try
		{
			// Log start of import.
			mLogger.LogInformation(
				"Starting import from {ShuttleType} to {TargetType}...",
				shuttleReader.GetType().Name,
				targetImporter.GetType().Name);

			// Send initial progress report.
			progress?.Report(
				new DataPortProgressReport
				{
					OverallMessage = "Initializing import..."
				});

			// Initialization
			await shuttleReader.InitializeAsync(cancellationToken).ConfigureAwait(false);
			await targetImporter.InitializeAsync(cancellationToken).ConfigureAwait(false);
			mLogger.LogInformation("Shuttle reader and target importer initialized");

			// The Critical Schema-Mismatch Check.
			mLogger.LogDebug("Performing schema compatibility check...");
			List<MigrationInfo> backupHistory = await shuttleReader
				                                    .GetMigrationHistoryAsync(cancellationToken)
				                                    .ConfigureAwait(false);
			List<MigrationInfo> targetHistory = await targetImporter
				                                    .GetMigrationHistoryAsync(cancellationToken)
				                                    .ConfigureAwait(false);

			// Validate that migration history is not empty.
			// An empty migration history indicates an uninitialized or invalid database/shuttle.
			if (backupHistory.Count == 0 || targetHistory.Count == 0)
			{
				mLogger.LogError(
					"Invalid migration history: shuttle has {ShuttleCount} migrations, target has {TargetCount} — both must have at least one entry",
					backupHistory.Count,
					targetHistory.Count);

				throw new InvalidOperationException(
					$"Invalid migration history. Shuttle has {backupHistory.Count} migrations, " +
					$"target has {targetHistory.Count}. Both must have at least one migration entry.");
			}

			List<string> backupMigrationIds = backupHistory.Select(m => m.MigrationId).ToList();
			List<string> targetMigrationIds = targetHistory.Select(m => m.MigrationId).ToList();

			if (!backupMigrationIds.SequenceEqual(targetMigrationIds))
			{
				var schemaMismatchEx = new DataPortSchemaMismatchException(backupHistory, targetHistory);

				mLogger.LogError(
					"Schema mismatch{MismatchDetail} — shuttle has {ShuttleCount} migrations, target has {TargetCount}, aborting import",
					schemaMismatchEx.FirstMismatchIndex is { } idx
						? $" at index {idx}"
						: " (different history length)",
					backupMigrationIds.Count,
					targetMigrationIds.Count);

				throw schemaMismatchEx;
			}

			mLogger.LogInformation("Schema check passed");

			// Read ShuttleId from shuttle metadata for checkpoint-based resume.
			Dictionary<string, string> shuttleMetadata = await shuttleReader
				                                             .GetMetadataAsync(cancellationToken)
				                                             .ConfigureAwait(false);

			if (!shuttleMetadata.TryGetValue(SqliteShuttleSchema.ShuttleIdKey, out string? shuttleId))
			{
				throw new InvalidOperationException(
					$"Shuttle file does not contain the required '{SqliteShuttleSchema.ShuttleIdKey}' metadata entry. " +
					"The file may have been created by an older version or is corrupted.");
			}

			mLogger.LogDebug("Shuttle identity: {ShuttleId}", shuttleId);

			// Prepare Target Database (e.g., disable FKs, validate/create checkpoint).
			await targetImporter.PrepareForImportAsync(shuttleId, cancellationToken).ConfigureAwait(false);
			mLogger.LogDebug("Target database prepared for bulk import");

			// Get list of tables to import.
			List<string> tablesToImport = await shuttleReader
				                              .GetTableNamesAsync(cancellationToken)
				                              .ConfigureAwait(false);
			// We don't need to filter __EFMigrationsHistory, as the shuttleReader's GetTableNamesAsync() should already exclude it.
			mLogger.LogInformation("Found {ImportTableCount} tables to import", tablesToImport.Count);
			int totalTables = tablesToImport.Count;
			int overallTotalSteps = totalTables + 2;

			// Start timing.
			var overallStopwatch = Stopwatch.StartNew();
			var stopWatch = Stopwatch.StartNew();
			int currentStep = 0;

			// Data Import Loop.
			foreach (string tableName in tablesToImport)
			{
				cancellationToken.ThrowIfCancellationRequested();
				currentStep++;

				// Log table import start.
				mLogger.LogInformation("Importing table {TableName}...", tableName);

				// Restart timing for this table.
				stopWatch.Restart();

				// Report progress.
				progress?.Report(
					new DataPortProgressReport
					{
						OverallMessage = $"Importing table {tableName} ({currentStep}/{totalTables})...",
						OverallTotalSteps = overallTotalSteps,
						OverallCurrentStep = currentStep
					});

				// The reader creates the stream.
				TableSnapshot snapshot = await shuttleReader
					                         .ReadTableAsync(tableName, cancellationToken)
					                         .ConfigureAwait(false);

				// The importer truncates, bulk-inserts, and reports detailed row progress.
				await targetImporter.ImportTableAsync(
						snapshot,
						mLogger,
						progress,          // Pass the handler down
						currentStep,       // Pass the current step
						overallTotalSteps, // Pass the total
						cancellationToken)
					.ConfigureAwait(false);

				// Log timing.
				stopWatch.Stop();
				mLogger.LogInformation(
					"Importing table {TableName}... DONE in {ElapsedMilliseconds}ms",
					tableName,
					stopWatch.ElapsedMilliseconds);
			}

			// Report that finalization is starting (not yet 100%).
			progress?.Report(
				new DataPortProgressReport
				{
					OverallMessage = "Finalizing import...",
					OverallTotalSteps = overallTotalSteps,
					OverallCurrentStep = totalTables + 1
				});

			// Finalize and Cleanup (e.g., re-enable FKs, reset sequences).
			mLogger.LogInformation("Finalizing import: re-enabling constraints and resetting sequences...");
			await targetImporter.CleanupAfterImportAsync(cancellationToken).ConfigureAwait(false);

			overallStopwatch.Stop();
			mLogger.LogInformation(
				"Import completed successfully in {ElapsedMs}ms",
				overallStopwatch.ElapsedMilliseconds);

			// Signal completion at 100%.
			progress?.Report(
				new DataPortProgressReport
				{
					OverallMessage = "Import completed",
					OverallTotalSteps = overallTotalSteps,
					OverallCurrentStep = overallTotalSteps
				});
		}
		catch (OperationCanceledException)
		{
			mLogger.LogWarning("Import was cancelled");
			throw; // Re-throw for the caller to handle.
		}
		catch (Exception ex)
		{
			mLogger.LogError(
				ex,
				"Fatal import error — the caller must dispose the reader/importer to release resources");

			throw;
		}
	}
}
