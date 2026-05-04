// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a system-wide role that grants permissions to users.
/// </summary>
/// <remarks>
///     <para>
///     Roles define what actions a user can perform in the system, such as accessing admin endpoints,
///     managing personas, or moderating content. Users can have multiple roles assigned.
///     </para>
///     <para>
///     Standard roles include <c>admin</c> (full system access), <c>user</c> (standard access), and
///     <c>moderator</c> (content moderation). Additional roles can be added as needed.
///     </para>
///     <para>
///         <b>Identifiers:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="Id"/> is the internal identifier used for database relationships.
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="PublicId"/> is intended for stable external references.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Constraints:</b> <see cref="Name"/> is unique and length-limited by the database schema.
///     </para>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class RoleEntity
{
	// --- 1. Primary key ---

	/// <summary>
	/// Gets or sets the internal unique identifier for database relationships.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Auto-incremented by the database. Never exposed via APIs.
	///     </para>
	///     <para>
	///     <b>Index:</b> Primary key.
	///     </para>
	/// </remarks>
	public RoleId Id { get; set; }

	// --- 2. Public identifier ---

	/// <summary>
	/// Gets or sets the public unique identifier for external references.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Exposed via APIs and remains stable across database migrations.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public Guid PublicId { get; set; }

	// --- 3. Foreign keys + Navigation properties (none) ---

	// --- 4. Timestamps ---

	/// <summary>
	/// Gets or sets the UTC timestamp when this role was created.
	/// </summary>
	public DateTime CreatedAtUtc { get; set; }

	// --- 5. Scalar domain fields ---

	/// <summary>
	/// Gets or sets the unique name of the role.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Used in authorization checks and JWT claims. Should be lowercase and URL-safe.
	///     Examples: <c>admin</c>, <c>user</c>, <c>moderator</c>
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.RoleNameMaxLength"/>.
	///     The database enforces uniqueness.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a human-readable description of the role's purpose and permissions.
	/// </summary>
	/// <remarks>
	/// Maximum length: <see cref="EntityLimits.RoleDescriptionMaxLength"/>.
	/// </remarks>
	public string? Description { get; set; }

	// --- 6. Collection navigation properties ---

	/// <summary>
	/// Gets the collection of user-role assignments.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<UserRoleEntity> UserRoles { get; set; } = [];
}
