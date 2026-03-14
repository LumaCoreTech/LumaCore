// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Providers;

/// <summary>
/// Resolves the <see cref="IDatabaseProviderOperations"/> implementation for a given provider name.
/// </summary>
/// <remarks>
/// This is the single place where provider names are mapped to their implementations. To add a new
/// database provider, create a new <see cref="IDatabaseProviderOperations"/> implementation and add
/// a case to the <see cref="GetProvider"/> method.
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
	/// <exception cref="InvalidOperationException">The provider is not supported.</exception>
	public static IDatabaseProviderOperations GetProvider(string providerName)
	{
		ArgumentNullException.ThrowIfNull(providerName);

		return providerName.Trim().ToLowerInvariant() switch
		{
			DatabaseProviders.Sqlite     => new SqliteProviderOperations(),
			DatabaseProviders.PostgreSql => new PostgreSqlProviderOperations(),
			DatabaseProviders.SqlServer  => new SqlServerProviderOperations(),
			DatabaseProviders.MySql      => new MySqlProviderOperations(),
			var _ => throw new InvalidOperationException(
				         $"Unsupported database provider: '{providerName}'. " +
				         $"Supported providers: {DatabaseProviders.GetSupportedProviders()}.")
		};
	}
}
