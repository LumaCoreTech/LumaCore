// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Security;

using Microsoft.Extensions.Options;

namespace LumaCore.Data.Services;

/// <summary>
/// Default implementation of <see cref="ILumaCoreDataService"/>.
/// </summary>
public sealed partial class LumaCoreDataService : ILumaCoreDataService
{
	private readonly DatabaseOptions   mDatabaseOptions;
	private readonly LumaCoreDbContext mDbContext;
	private readonly ISecretProtector  mSecretProtector;
	private readonly TimeProvider      mTimeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="LumaCoreDataService"/> class.
	/// </summary>
	/// <param name="dbContext">The EF Core database context.</param>
	/// <param name="databaseOptions">The database configuration options.</param>
	/// <param name="secretProtector">
	/// The secret protector for encrypting/decrypting sensitive data before storing it in the database.
	/// </param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="dbContext"/> or <paramref name="databaseOptions"/> is <see langword="null"/>.
	/// </exception>
	public LumaCoreDataService(
		LumaCoreDbContext         dbContext,
		IOptions<DatabaseOptions> databaseOptions,
		ISecretProtector          secretProtector,
		TimeProvider              timeProvider)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		ArgumentNullException.ThrowIfNull(databaseOptions);
		ArgumentNullException.ThrowIfNull(secretProtector);
		mDbContext = dbContext;
		mDatabaseOptions = databaseOptions.Value;
		mSecretProtector = secretProtector;
		mTimeProvider = timeProvider;
	}

	private bool PreferCompiledHotPathQueries => mDatabaseOptions.PreferCompiledHotPathQueries;
}
