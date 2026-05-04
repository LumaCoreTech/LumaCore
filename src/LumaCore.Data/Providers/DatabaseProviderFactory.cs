// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Providers;

/// <summary>
/// Resolves the <see cref="IDatabaseProviderOperations"/> implementation for a given provider name.
/// </summary>
/// <remarks>
///     <para>
///     This is the single place where provider names are mapped to their implementations. To add a new
///     database provider, create a new <see cref="IDatabaseProviderOperations"/> implementation and add
///     a case to the <see cref="GetProvider"/> method.
///     </para>
///     <para>
///     Providers that are known but temporarily unavailable (e.g. <see cref="DatabaseProviders.MySql"/>)
///     are rejected explicitly with a dedicated error message that points at the underlying reason —
///     they do not fall through to the generic "unsupported provider" branch. This keeps the diagnostic
///     consistent with the corresponding configuration-time check in the service registration layer.
///     </para>
/// </remarks>
public static class DatabaseProviderFactory
{
	/// <summary>
	/// Returns the <see cref="IDatabaseProviderOperations"/> implementation for the specified provider.
	/// </summary>
	/// <param name="providerName">
	/// The provider identifier (case-insensitive). Must match one of the constants in <see cref="DatabaseProviders"/>.
	/// </param>
	/// <returns>The provider operations instance.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="providerName"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// The provider is not supported, or the provider is known but temporarily unavailable
	/// (currently <see cref="DatabaseProviders.MySql"/>).
	/// </exception>
	public static IDatabaseProviderOperations GetProvider(string providerName)
	{
		ArgumentNullException.ThrowIfNull(providerName);

		return providerName.Trim().ToLowerInvariant() switch
		{
			DatabaseProviders.Sqlite     => new SqliteProviderOperations(),
			DatabaseProviders.PostgreSql => new PostgreSqlProviderOperations(),
			DatabaseProviders.SqlServer  => new SqlServerProviderOperations(),

			// MySQL/MariaDB is intentionally rejected here even though MySqlProviderOperations exists:
			// Pomelo.EntityFrameworkCore.MySql has not yet released an EF Core 10 compatible version, so
			// the DataPort code paths in MySqlProviderOperations would throw at runtime anyway. Failing
			// fast at provider resolution gives a single, actionable error message instead of a late
			// NotSupportedException from deep inside an export/import pipeline.
			DatabaseProviders.MySql => throw new InvalidOperationException(
				                           "MySQL/MariaDB support is temporarily unavailable. " +
				                           "Pomelo.EntityFrameworkCore.MySql has not yet released an EF Core 10 compatible version. " +
				                           "Please use SQLite, PostgreSQL, or SQL Server instead, or track progress at: " +
				                           "https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues"),

			var _ => throw new InvalidOperationException(
				         $"Unsupported database provider: '{providerName}'. " +
				         $"Supported providers: {DatabaseProviders.GetSupportedProviders()}.")
		};
	}
}
