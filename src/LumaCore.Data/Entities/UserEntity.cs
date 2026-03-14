// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a human user account in the system.
/// </summary>
/// <remarks>
///     <para>
///     Users are participants who can authenticate and interact with the system. Each user has a linked
///     <see cref="ParticipantEntity"/> that provides the unified identity for conversations.
///     </para>
///     <para>
///     Authentication is handled via username/password with secure password hashing. The <see cref="PasswordHash"/>
///     field stores the hashed password using a secure algorithm (e.g., bcrypt, Argon2).
///     </para>
///     <para>
///         <b>Identifiers:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             This entity uses an internal numeric <see cref="Id"/> as primary key.
///             </description>
///         </item>
///         <item>
///             <description>
///             External references should use the linked participant's <see cref="ParticipantEntity.PublicId"/>.
///             </description>
///         </item>
///     </list>
///     <para>
///         <b>Constraints:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             The database enforces uniqueness for <see cref="Username"/>.
///             </description>
///         </item>
///         <item>
///             <description>
///             The database enforces uniqueness for non-<see langword="null"/> <see cref="Email"/> values.
///             </description>
///         </item>
///     </list>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class UserEntity
{
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
	public UserId Id { get; set; }

	/// <summary>
	/// Gets or sets the foreign key to the associated participant.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Points to <see cref="ParticipantEntity.Id"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public ParticipantId ParticipantId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the associated participant.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     This relationship is required at the database level via <see cref="ParticipantId"/>,
	///     but the navigation may be <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Deleting a user account does not automatically delete the linked participant.
	///     This preserves conversation participant lists and message history in multi-user conversations.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ParticipantEntity? Participant { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp of the user's last successful login.
	/// </summary>
	/// <remarks>
	/// Updated each time credentials are validated successfully (i.e. an interactive/login authentication).
	/// This is not a general "last activity" timestamp and is not automatically updated by token refresh or API usage.
	/// <see langword="null"/> if never logged in.
	/// </remarks>
	public DateTime? LastLoginAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp of the user's last successful token refresh.
	/// </summary>
	/// <remarks>
	/// Updated each time the user successfully uses a refresh flow to obtain new credentials.
	/// This is not a general "last activity" timestamp and is not updated by normal API usage.
	/// <see langword="null"/> if never refreshed.
	/// </remarks>
	public DateTime? LastTokenRefreshAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the user's email address.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Optional.
	///     Used for password recovery and notifications. The database enforces uniqueness for non-<see langword="null"/>
	///     values.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public string? Email { get; set; }

	/// <summary>
	/// Gets or sets the securely hashed password.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Never store plain-text passwords.
	///     </para>
	///     <para>
	///     This value is expected to contain the full output of a password hashing scheme (e.g. bcrypt, Argon2),
	///     including salt and work/variant parameters.
	///     </para>
	///     <para>
	///     The database column length is limited; ensure the chosen hash format fits (e.g. common bcrypt formats do).
	///     </para>
	/// </remarks>
	public string PasswordHash { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the unique username for authentication.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Used for login.
	///     </para>
	///     <para>
	///     Preserve the user-entered casing for display, but treat username uniqueness and authentication comparisons as
	///     case-insensitive by applying consistent normalization at the application boundary.
	///     </para>
	///     <para>
	///     Uniqueness is enforced via <see cref="UsernameNormalized"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index.
	///     </para>
	/// </remarks>
	public string Username { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the normalized username used for case-insensitive uniqueness and lookups.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This value is derived from <see cref="Username"/> by applying a stable, culture-invariant normalization
	///     (typically <c>Trim()</c> + <c>ToUpperInvariant()</c>).
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public string UsernameNormalized { get; set; } = string.Empty;

	/// <summary>
	/// Gets the collection of roles assigned to this user.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<UserRoleEntity> UserRoles { get; set; } = [];
}
