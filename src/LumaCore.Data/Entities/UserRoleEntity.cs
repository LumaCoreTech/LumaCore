// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents the assignment of a role to a user.
/// </summary>
/// <remarks>
///     <para>
///     This is a join entity for the many-to-many relationship between users and roles.
///     It includes audit information about when the role was assigned.
///     </para>
///     <para>
///     <b>Keys:</b> The primary key is composite: <see cref="UserId"/> + <see cref="RoleId"/>.
///     </para>
///     <para>
///     Database relationships, keys, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class UserRoleEntity
{
	/// <summary>
	/// Gets or sets the foreign key to the user.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Part of the composite primary key.
	///     </para>
	///     <para>
	///     <b>Index:</b> Composite primary key <c>(UserId, RoleId)</c>.
	///     </para>
	///     <para>
	///     Points to <see cref="UserEntity.Id"/>.
	///     </para>
	/// </remarks>
	public UserId UserId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the user.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     The relationship is required at the database level via <see cref="UserId"/>,
	///     but the navigation may be <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public UserEntity? User { get; set; }

	/// <summary>
	/// Gets or sets the foreign key to the role.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Part of the composite primary key.
	///     </para>
	///     <para>
	///     <b>Index:</b> Composite primary key <c>(UserId, RoleId)</c>.
	///     </para>
	///     <para>
	///     Points to <see cref="RoleEntity.Id"/>.
	///     </para>
	/// </remarks>
	public RoleId RoleId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the role.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     The relationship is required at the database level via <see cref="RoleId"/>,
	///     but the navigation may be <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public RoleEntity? Role { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this role was assigned to the user.
	/// </summary>
	/// <remarks>
	/// Provides an audit trail for role assignments.
	/// This value is set by the application at assignment time and is required by the database schema.
	/// </remarks>
	public DateTime AssignedAtUtc { get; set; }
}
