// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;

using Microsoft.Extensions.Logging;

namespace LumaCore.Data.DataPort.Shuttle;

/// <summary>
/// Default <see cref="IShuttleReaderFactory"/> implementation that creates <see cref="SqliteShuttleReader"/> instances.
/// </summary>
/// <remarks>
/// Each reader gets the shared <see cref="ILogger{SqliteShuttleReader}"/> from DI. The reader's SQLite connection is
/// opened lazily during <see cref="IShuttleReader.InitializeAsync"/> and closed on disposal, so the factory itself
/// holds no database resources.
/// </remarks>
sealed class SqliteShuttleReaderFactory : IShuttleReaderFactory
{
	private readonly ILogger<SqliteShuttleReader> mLogger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteShuttleReaderFactory"/> class.
	/// </summary>
	/// <param name="logger">The logger forwarded to each created <see cref="SqliteShuttleReader"/>.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="logger"/> is <see langword="null"/>.
	/// </exception>
	public SqliteShuttleReaderFactory(ILogger<SqliteShuttleReader> logger)
	{
		ArgumentNullException.ThrowIfNull(logger);
		mLogger = logger;
	}

	/// <inheritdoc/>
	public IShuttleReader Create(string filePath)
	{
		FilePathValidator.Validate(filePath);
		return new SqliteShuttleReader(filePath, mLogger);
	}
}
