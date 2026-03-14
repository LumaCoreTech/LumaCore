// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Seeding;

/// <summary>
/// Defines a reusable, versioned seed operation that can be tracked and applied idempotently.
/// </summary>
/// <remarks>
///     <para>
///     Seed definitions are executed during database initialization and can be tracked in the seed history
///     to prevent duplicate execution. Each seed has a unique identifier and version number.
///     </para>
///     <para>
///     Use this for complex seeding scenarios where simple idempotent checks (e.g., "insert if not exists")
///     are insufficient or when seed logic needs to evolve across application versions.
///     </para>
/// </remarks>
public interface ISeedDefinition
{
	/// <summary>
	/// Gets the unique identifier for this seed.
	/// </summary>
	/// <remarks>
	/// This should be a stable, descriptive name that uniquely identifies the seed purpose.
	/// Examples: "DefaultRoles", "InitialSystemPrompts", "AdminUserSetup"
	/// </remarks>
	string SeedId { get; }

	/// <summary>
	/// Gets the version of this seed definition.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This version acts as a <b>state marker</b> that indicates which code-level has been applied to the database.
	///     When the seed code version is higher than the database version, the seed is re-executed and the database
	///     version is updated to match the code version.
	///     </para>
	///     <para>
	///     <b>Important:</b> Intermediate versions are skipped! If the database has version 1 and the code has version 5,
	///     only version 5 executes (versions 2-4 are skipped). This requires seeds to be <b>idempotent</b> and define
	///     the complete target state, not incremental changes.
	///     </para>
	///     <para>
	///     Simply increment this integer (1 → 2 → 3 → ...) when seed logic changes to ensure re-execution on deployments.
	///     </para>
	///     <para>
	///         <b>Example:</b>
	///     </para>
	///     <list type="bullet">
	///         <item>Version 1: Seeds admin, user, moderator roles</item>
	///         <item>Version 2: Adds "guest" role</item>
	///         <item>Version 3: Adds "api-user" role</item>
	///     </list>
	///     <para>
	///     If the database has version 1 and code deploys version 3, the seed checks the current state and adds
	///     both "guest" and "api-user" (version 2 is skipped, but its changes are included in version 3).
	///     </para>
	/// </remarks>
	int Version { get; }

	/// <summary>
	/// Gets the display description of what this seed does.
	/// </summary>
	string Description { get; }

	/// <summary>
	/// Executes the seed operation.
	/// </summary>
	/// <param name="dbContext">The database context to use for seeding.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This method should be idempotent when possible. If the seed has already been partially applied,
	/// re-running should complete successfully without duplicating data.
	/// </remarks>
	Task ExecuteAsync(LumaCoreDbContext dbContext, CancellationToken cancellationToken);
}
