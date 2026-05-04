// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

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
///             The database enforces uniqueness for <see cref="UsernameNormalized"/>, not for the display-cased
///             <see cref="Username"/> value itself.
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
	public UserId Id { get; set; }

	// --- 2. Public identifier (none) ---

	// --- 3. Foreign keys + Navigation properties ---

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
	/// Gets or sets the navigation property to the user's application preferences.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is a 1:1 optional relationship. The <see cref="UserPreferencesEntity"/> row is created lazily
	///     on the first preference write and deleted via <c>CASCADE</c> when the user is removed.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public UserPreferencesEntity? Preferences { get; set; }

	// --- 4. Timestamps ---

	/// <summary>
	/// Gets or sets the UTC timestamp when this user account was created.
	/// </summary>
	/// <remarks>
	/// Stamped once on insert by the data layer and never modified afterwards. The data layer treats this column as
	/// required so consumers can rely on a meaningful value without coalescing. This is distinct from
	/// <see cref="LastLoginAtUtc"/> (interactive sign-ins) and <see cref="LastTokenRefreshAtUtc"/> (token refreshes),
	/// and supports audit, cohort, and inactive-account cleanup queries.
	/// </remarks>
	public DateTime CreatedAtUtc { get; set; }

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

	// --- 5. Scalar domain fields ---

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
	///     Maximum length: <see cref="EntityLimits.UsernameMaxLength"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index for case-preserving lookups and administrative queries.
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
	///     Maximum length: <see cref="EntityLimits.UsernameMaxLength"/> (matches <see cref="Username"/>).
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public string UsernameNormalized { get; set; } = string.Empty;

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
	///     Maximum length: <see cref="EntityLimits.PasswordHashMaxLength"/>.
	///     Ensure the chosen hash format fits (e.g. common bcrypt formats do).
	///     </para>
	/// </remarks>
	public string PasswordHash { get; set; } = string.Empty;

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
	///     Maximum length: <see cref="EntityLimits.EmailMaxLength"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public string? Email { get; set; }

	// --- 6. Collection navigation properties ---

	/// <summary>
	/// Gets the collection of roles assigned to this user.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<UserRoleEntity> UserRoles { get; set; } = [];
}
