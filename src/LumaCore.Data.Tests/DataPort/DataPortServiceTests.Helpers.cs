// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.DataPort.Models;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging;

// ReSharper disable PropertyCanBeMadeInitOnly.Local
// ReSharper disable AsyncMethodWithoutAwait

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class DataPortServiceTests
{
	/// <summary>
	/// Configurable stub for <see cref="IDataExportReader"/>. All members default to no-op; override via
	/// properties to inject specific behavior for error-path tests.
	/// </summary>
	private sealed class StubExportReader : IDataExportReader
	{
		/// <summary>Gets or sets the delegate invoked by <see cref="InitializeAsync"/>.</summary>
		public Func<CancellationToken, Task> OnInitialize { get; set; } = _ => Task.CompletedTask;

		/// <summary>Gets or sets the delegate invoked by <see cref="GetMigrationHistoryAsync"/>.</summary>
		public Func<CancellationToken, Task<List<MigrationInfo>>> OnGetMigrationHistory { get; set; } =
			_ => Task.FromResult(new List<MigrationInfo>());

		/// <summary>Gets or sets the delegate invoked by <see cref="GetTableNamesAsync"/>.</summary>
		public Func<CancellationToken, Task<List<string>>> OnGetTableNames { get; set; } =
			_ => Task.FromResult(new List<string>());

		/// <summary>Gets or sets the delegate invoked by <see cref="ReadTableAsync"/>.</summary>
		public Func<string, CancellationToken, Task<TableSnapshot>> OnReadTable { get; set; } =
			(_, _) => throw new NotImplementedException();

		/// <inheritdoc/>
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		/// <inheritdoc/>
		public Task InitializeAsync(CancellationToken cancellationToken = default) => OnInitialize(cancellationToken);

		/// <inheritdoc/>
		public Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default) =>
			OnGetMigrationHistory(cancellationToken);

		/// <inheritdoc/>
		public Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default) =>
			OnGetTableNames(cancellationToken);

		/// <inheritdoc/>
		public Task<TableSnapshot> ReadTableAsync(string tableName, CancellationToken cancellationToken = default) =>
			OnReadTable(tableName, cancellationToken);
	}

	/// <summary>
	/// Configurable stub for <see cref="IShuttleWriter"/>. All members default to no-op; override via
	/// properties to inject specific behavior for error-path tests.
	/// </summary>
	private sealed class StubShuttleWriter : IShuttleWriter
	{
		/// <inheritdoc/>
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		/// <inheritdoc/>
		public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		/// <inheritdoc/>
		public Task WriteTableAsync(
			TableSnapshot                      table,
			ILogger?                           logger,
			IProgress<DataPortProgressReport>? progress,
			int                                currentStep,
			int                                overallTotalSteps,
			CancellationToken                  cancellationToken = default) => Task.CompletedTask;

		/// <inheritdoc/>
		public Task WriteMigrationHistoryAsync(
			List<MigrationInfo> migrations,
			CancellationToken   cancellationToken = default) => Task.CompletedTask;

		/// <inheritdoc/>
		public Task WriteMetadataAsync(
			Dictionary<string, string> metadata,
			CancellationToken          cancellationToken = default) => Task.CompletedTask;

		/// <inheritdoc/>
		public Task FinalizeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	/// <summary>
	/// Configurable stub for <see cref="IShuttleReader"/>. All members default to no-op; override via
	/// properties to inject specific behavior for error-path tests.
	/// </summary>
	private sealed class StubShuttleReader : IShuttleReader
	{
		/// <summary>Gets or sets the delegate invoked by <see cref="GetMigrationHistoryAsync"/>.</summary>
		public Func<CancellationToken, Task<List<MigrationInfo>>> OnGetMigrationHistory { get; set; } =
			_ => Task.FromResult(new List<MigrationInfo>());

		/// <summary>Gets or sets the delegate invoked by <see cref="GetMetadataAsync"/>.</summary>
		public Func<CancellationToken, Task<Dictionary<string, string>>> OnGetMetadata { get; set; } =
			_ => Task.FromResult(new Dictionary<string, string>());

		/// <summary>Gets or sets the delegate invoked by <see cref="GetTableNamesAsync"/>.</summary>
		public Func<CancellationToken, Task<List<string>>> OnGetTableNames { get; set; } =
			_ => Task.FromResult(new List<string>());

		/// <summary>Gets or sets the delegate invoked by <see cref="ReadTableAsync"/>.</summary>
		public Func<string, CancellationToken, Task<TableSnapshot>> OnReadTable { get; set; } =
			(_, _) => throw new NotImplementedException();

		/// <inheritdoc/>
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		/// <inheritdoc/>
		public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		/// <inheritdoc/>
		public Task ValidateIntegrityAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		/// <inheritdoc/>
		public Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default) =>
			OnGetMigrationHistory(cancellationToken);

		/// <inheritdoc/>
		public Task<Dictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken = default) =>
			OnGetMetadata(cancellationToken);

		/// <inheritdoc/>
		public Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default) =>
			OnGetTableNames(cancellationToken);

		/// <inheritdoc/>
		public Task<TableSnapshot> ReadTableAsync(string tableName, CancellationToken cancellationToken = default) =>
			OnReadTable(tableName, cancellationToken);

		/// <inheritdoc/>
		public Task<DateTimeOffset?> GetCreatedUtcAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<DateTimeOffset?>(null);
	}

	/// <summary>
	/// Configurable stub for <see cref="IDataImportWriter"/>. All members default to no-op; override via
	/// properties to inject specific behavior for error-path tests.
	/// </summary>
	private sealed class StubImportWriter : IDataImportWriter
	{
		/// <summary>Gets or sets the delegate invoked by <see cref="GetMigrationHistoryAsync"/>.</summary>
		public Func<CancellationToken, Task<List<MigrationInfo>>> OnGetMigrationHistory { get; set; } =
			_ => Task.FromResult(new List<MigrationInfo>());

		/// <inheritdoc/>
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		/// <inheritdoc/>
		public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		/// <inheritdoc/>
		public Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default) =>
			OnGetMigrationHistory(cancellationToken);

		/// <inheritdoc/>
		public Task PrepareForImportAsync(string shuttleId, CancellationToken cancellationToken = default) =>
			Task.CompletedTask;

		/// <inheritdoc/>
		public Task ImportTableAsync(
			TableSnapshot                      table,
			ILogger?                           logger,
			IProgress<DataPortProgressReport>? progress,
			int                                currentStep,
			int                                overallTotalSteps,
			CancellationToken                  cancellationToken = default) => Task.CompletedTask;

		/// <inheritdoc/>
		public Task CleanupAfterImportAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	/// <summary>
	/// Synchronous <see cref="IProgress{T}"/> implementation that captures all reports into a list.
	/// Unlike <see cref="Progress{T}"/>, this does not post to a synchronization context, avoiding
	/// race conditions in test assertions.
	/// </summary>
	private sealed class CapturingProgress : IProgress<DataPortProgressReport>
	{
		/// <summary>Gets the captured progress reports in the order they were received.</summary>
		public List<DataPortProgressReport> Reports { get; } = [];

		/// <inheritdoc/>
		public void Report(DataPortProgressReport value) => Reports.Add(value);
	}

	/// <summary>
	/// Returns an empty <see cref="IAsyncEnumerable{T}"/> for constructing <see cref="TableSnapshot"/>
	/// instances without row data.
	/// </summary>
	private static async IAsyncEnumerable<object?[]> EmptyRowStream()
	{
		yield break;
	}
}
