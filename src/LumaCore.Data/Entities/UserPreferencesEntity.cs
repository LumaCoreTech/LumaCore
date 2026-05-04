// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

namespace LumaCore.Data.Entities;

/// <summary>
/// Stores user-specific application preferences as a JSON blob.
/// </summary>
/// <remarks>
///     <para>
///     This is a 1:1 extension table for <see cref="UserEntity"/>. It keeps the frequently queried
///     <c>Users</c> table lean by separating infrequently accessed, schema-flexible preference data
///     into its own row.
///     </para>
///     <para>
///     The <see cref="PreferencesJson"/> column contains a JSON object whose schema is defined by the
///     application-layer <c>UserPreferences</c> model. Adding new preference fields only requires a
///     model change — no database migration is needed.
///     </para>
///     <para>
///     <b>Lifecycle:</b> Created lazily on first preference write. Deleted automatically via
///     <c>CASCADE</c> when the parent <see cref="UserEntity"/> is removed.
///     </para>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class UserPreferencesEntity
{
	// --- 1. Primary key (also foreign key to UserEntity) ---

	/// <summary>
	/// Gets or sets the user identifier. This is both the primary key and the foreign key
	/// to <see cref="UserEntity.Id"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Index:</b> Primary key.
	///     </para>
	/// </remarks>
	public UserId UserId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the owning user.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This relationship is required at the database level via <see cref="UserId"/>,
	///     but the navigation may be <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public UserEntity? User { get; set; }

	// --- 2. Public identifier (none) ---

	// --- 3. Foreign keys + Navigation properties (none) ---

	// --- 4. Timestamps (none) ---

	// --- 5. Scalar domain fields ---

	/// <summary>
	/// Gets or sets the serialized JSON preferences blob.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Contains a JSON object conforming to the <c>UserPreferences</c> application model.
	///     <see langword="null"/> indicates that no preferences have been persisted yet
	///     (the application should fall back to defaults).
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.UserPreferencesJsonMaxLength"/>.
	///     The application is responsible for serialization and deserialization.
	///     </para>
	/// </remarks>
	public string? PreferencesJson { get; set; }

	// --- 6. Collection navigation properties (none) ---
}
