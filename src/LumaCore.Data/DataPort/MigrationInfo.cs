// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.DataPort;

/// <summary>
/// Represents a single Entity Framework Core migration entry used throughout the data porting pipeline.
/// </summary>
/// <param name="MigrationId">
/// The unique identifier of the migration as stored in the <c>__EFMigrationsHistory</c> table.
/// </param>
/// <param name="ProductVersion">
/// The version of the Entity Framework Core NuGet package that was used to generate this migration
/// (e.g., <c>10.0.0</c>). This value is written automatically by EF Core and does _not_ represent the application version.
/// </param>
/// <exception cref="ArgumentNullException">
/// <paramref name="MigrationId"/> or <paramref name="ProductVersion"/> is <see langword="null"/>.
/// </exception>
public sealed record MigrationInfo(string MigrationId, string ProductVersion)
{
	/// <summary>
	/// Gets the unique identifier of the migration as stored in the <c>__EFMigrationsHistory</c> table.
	/// </summary>
	public string MigrationId { get; init; } = MigrationId ?? throw new ArgumentNullException(nameof(MigrationId));

	/// <summary>
	/// Gets the version of the Entity Framework Core NuGet package that was used to generate this migration
	/// (e.g., <c>10.0.0</c>). This value is written automatically by EF Core and does not represent the application version.
	/// </summary>
	public string ProductVersion { get; init; } =
		ProductVersion ?? throw new ArgumentNullException(nameof(ProductVersion));
}
