// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Models;

using Microsoft.Extensions.Logging;

// using LumaCore.Data.Services.DataPort.Models;

// TODO: Re-enable when Pomelo releases EF Core 10 compatible version.
// Track: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues
// using MySqlConnector;

namespace LumaCore.Data.DataPort.Import.Implementations;

/// <summary>
/// Implements the <see cref="IDataImportWriter"/> for MySQL.
/// </summary>
/// <remarks>
///     <para>
///     This importer uses high-performance batched INSERT statements, as
///     MySQL's native bulk loader (LOAD DATA) is file-based and not
///     suitable for stream-based imports.
///     It manages foreign key checks and auto-increment resetting.
///     </para>
///     <para>
///     Data is imported in chunks of <see cref="DataPortTuning.ImportChunkSizeRows"/> rows. Each chunk
///     is committed in its own transaction alongside a checkpoint update, enabling crash-safe resume.
///     </para>
///     <para>
///     <b>Note:</b> This implementation is not yet complete.
///     </para>
/// </remarks>
public sealed class MySqlImportWriter : IDataImportWriter
{
	private readonly string       mConnectionString;
	private readonly TimeProvider mTimeProvider;
	private readonly ILogger?     mLogger;

	private bool mDisposed;

	/// <summary>
	/// Message used by <see cref="NotSupportedException"/> until Pomelo releases an EF Core 10 compatible provider.
	/// </summary>
	private const string NotSupportedMessage =
		"MySQL data import is not yet available. Pomelo.EntityFrameworkCore.MySql has not released an " +
		"EF Core 10 compatible version. Track progress at: " +
		"https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues";

	//private                 MySqlConnection?  mConnection;
	//private                 MySqlTransaction? mTransaction;
	//private static readonly int               mBatchSize = DataPortTuning.MySqlInsertBatchSizeRows;

	/// <summary>
	/// Initializes a new instance of the <see cref="MySqlImportWriter"/> class.
	/// </summary>
	/// <param name="connectionString">The connection string for the target MySQL database.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <param name="logger">
	/// An optional logger for diagnostic messages (e.g., checkpoint mismatch warnings).
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="connectionString"/> or <paramref name="timeProvider"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="connectionString"/> is empty or consists only of white-space characters.
	/// </exception>
	public MySqlImportWriter(string connectionString, TimeProvider timeProvider, ILogger? logger = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(timeProvider);
		mConnectionString = connectionString;
		mTimeProvider = timeProvider;
		mLogger = logger;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		mDisposed = true;

		//// Commit only if CleanupAfterImportAsync was called successfully.
		//// Otherwise, the transaction will be rolled back on dispose.
		//if (mTransaction != null)
		//{
		//	try
		//	{
		//		if (mImportSuccessfullyCompleted)
		//			await mTransaction.CommitAsync().ConfigureAwait(false);
		//	}
		//	finally
		//	{
		//		await mTransaction.DisposeAsync().ConfigureAwait(false);
		//		mTransaction = null;
		//	}
		//}
		//if (mConnection != null)
		//{
		//	await mConnection.DisposeAsync().ConfigureAwait(false);
		//	mConnection = null;
		//}
	}

	/// <inheritdoc/>
	public Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException(NotSupportedMessage);

		//// Prevent re-initialization.
		//if (mConnection != null)
		//	throw new InvalidOperationException("Importer has already been initialized.");

		//mConnection = new MySqlConnection(mConnectionString);
		//await mConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
		//mTransaction = await mConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<List<MigrationInfo>> GetMigrationHistoryAsync(
		CancellationToken cancellationToken
			= default)
	{
		throw new NotSupportedException(NotSupportedMessage);

		//if (mConnection == null || mTransaction == null)
		//	throw new InvalidOperationException("Importer is not initialized.");

		//var migrations = new List<(string, string)>();
		//// Use backticks for MySQL identifiers
		//await using var cmd = new MySqlCommand(
		//	"SELECT `MigrationId`, `ProductVersion` FROM `__EFMigrationsHistory` ORDER BY `MigrationId`",
		//	mConnection,
		//	mTransaction);

		//await using MySqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		//while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		//{
		//	migrations.Add((reader.GetString(0), reader.GetString(1)));
		//}
		//return migrations;
	}

	/// <inheritdoc/>
	public async Task PrepareForImportAsync(string shuttleId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(shuttleId);

		throw new NotSupportedException(NotSupportedMessage);

		//if (mConnection == null || mTransaction == null)
		//	throw new InvalidOperationException("Importer is not initialized.");

		//// MySQL's command to disable foreign key checks for the session.
		//await using var cmd = new MySqlCommand("SET FOREIGN_KEY_CHECKS=0;", mConnection, mTransaction);
		//await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task ImportTableAsync(
		TableSnapshot                      table,
		ILogger?                           logger,
		IProgress<DataPortProgressReport>? progress,
		int                                currentTable,
		int                                totalTables,
		CancellationToken                  cancellationToken = default)
	{
		throw new NotSupportedException(NotSupportedMessage);
		//if (mConnection == null || mTransaction == null)
		//	throw new InvalidOperationException("Importer is not initialized.");

		//// Truncate the table before importing.
		//// TRUNCATE TABLE is fast and also resets the AUTO_INCREMENT counter.
		//try
		//{
		//	string truncateSql = $"TRUNCATE TABLE {QuoteMySql(table.Name)}";
		//	await using (var cmd = new MySqlCommand(truncateSql, mConnection, mTransaction))
		//	{
		//		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		//	}
		//}
		//catch (Exception ex)
		//{
		//	logger?.LogError(ex, "Failed to truncate table {TableName}", table.Name);
		//	throw;
		//}

		//// 1. Build the command template
		//// `INSERT INTO `Users` (`Id`, `Name`) VALUES `
		//var sqlBuilder = new StringBuilder();
		//var columnNames = string.Join(", ", table.Columns.Select(c => QuoteMySql(c.Name)));
		//sqlBuilder.Append($"INSERT INTO {QuoteMySql(table.Name)} ({columnNames}) VALUES ");

		//// `(@p0_0, @p0_1), (@p1_0, @p1_1), ...`
		//var parameterNames = new List<string>();
		//for (int i = 0; i < table.Columns.Count; i++)
		//{
		//	parameterNames.Add($"@p{i}");
		//}
		//var rowParameterTemplate = $"({string.Join(", ", parameterNames)})";

		//// This is the full template for a *single* row insert.
		//// We will build the batch manually.
		//string singleInsertSql = sqlBuilder.ToString() + rowParameterTemplate;

		//// 2. Stream rows and build batches
		//await using var cmd = new MySqlCommand { Connection = mConnection, Transaction = mTransaction };

		//var batchRowValues = new StringBuilder();
		//var batchParameters = new List<MySqlParameter>();
		//int rowInBatch = 0;
		//long totalRowCount = 0;

		//// Get estimated total and set up messages.
		//long estimatedRows = table.EstimatedRowCount;
		//string overallMsg = $"Importing '{table.Name}' ({currentTable}/{totalTables})...";

		//await foreach (object?[] row in table.Rows.WithCancellation(cancellationToken).ConfigureAwait(false))
		//{
		//	// Build the parameter names for this row, e.g., (@p0_0, @p0_1)
		//	var rowParamNames = new List<string>();
		//	for (int i = 0; i < row.Length; i++)
		//	{
		//		string paramName = $"@p{rowInBatch}_{i}";
		//		rowParamNames.Add(paramName);
		//		batchParameters.Add(new MySqlParameter(paramName, row[i] ?? DBNull.Value));
		//	}

		//	// Add this row's values to the batch: `(@p0_0, @p0_1),`
		//	if (rowInBatch > 0)
		//	{
		//		batchRowValues.Append(", ");
		//	}
		//	batchRowValues.Append($"({string.Join(", ", rowParamNames)})");

		//	rowInBatch++;
		//	totalRowCount++;

		//	// If batch is full, execute it
		//	if (rowInBatch >= mBatchSize)
		//	{
		//		await ExecuteBatchAsync();

		//		// Feed detailed steps to the report.
		//		progress?.Report(
		//			new DataPortProgressReport
		//			{
		//				OverallMessage = overallMsg,
		//				OverallCurrentStep = currentTable,
		//				OverallTotalSteps = totalTables,

		//				DetailedMessage = $"{totalRowCount:N0} rows processed",
		//				DetailedCurrentStep = totalRowCount,
		//				DetailedTotalSteps = estimatedRows > 0 ? estimatedRows : null
		//			});
		//	}
		//}

		//// Execute any remaining rows in the last batch
		//if (rowInBatch > 0)
		//{
		//	await ExecuteBatchAsync();

		//	// Report final count.
		//	progress?.Report(
		//		new DataPortProgressReport
		//		{
		//			OverallMessage = overallMsg,
		//			OverallCurrentStep = currentTable,
		//			OverallTotalSteps = totalTables,

		//			DetailedMessage = $"{totalRowCount:N0} rows processed",
		//			DetailedCurrentStep = totalRowCount,
		//			DetailedTotalSteps = estimatedRows > 0 ? estimatedRows : null
		//		});
		//}

		//logger?.LogDebug("Imported {ImportedRowCount} rows into {TableName}.", totalRowCount, table.Name);

		//// --- Local helper function to execute a batch ---
		//async Task ExecuteBatchAsync()
		//{
		//	// Finalize the SQL query for the batch
		//	cmd.CommandText = sqlBuilder.ToString() + batchRowValues.ToString();
		//	cmd.Parameters.Clear();
		//	cmd.Parameters.AddRange(batchParameters.ToArray());

		//	// Execute
		//	await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

		//	// Reset for next batch
		//	rowInBatch = 0;
		//	batchRowValues.Clear();
		//	batchParameters.Clear();
		//}
	}

	/// <inheritdoc/>
	public async Task CleanupAfterImportAsync(CancellationToken cancellationToken = default)
	{
		//if (mConnection == null || mTransaction == null || mConnection.Database == null)
		//	throw new InvalidOperationException("Importer is not initialized.");

		//// Re-enable foreign key checks.
		//await using (var cmd = new MySqlCommand("SET FOREIGN_KEY_CHECKS=1;", mConnection, mTransaction))
		//{
		//	await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		//}

		//// Note: Unlike Postgres and SQL Server, MySQL does not require manually resetting
		//// auto-increment seeds after a TRUNCATE + INSERT. The auto-increment counter is
		//// automatically set to MAX(id) + 1 after the import.

		//// Mark import as successfully completed so DisposeAsync will commit.
		//mImportSuccessfullyCompleted = true;
	}
}
