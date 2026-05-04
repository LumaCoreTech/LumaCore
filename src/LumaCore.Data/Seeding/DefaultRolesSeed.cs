// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;

using LumaCore.Data.Entities;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Seeding;

/// <summary>
/// Seeds default roles into the database.
/// </summary>
/// <remarks>
/// This seed is idempotent and can be safely re-run. It will only add roles that don't already exist.
/// </remarks>
public sealed class DefaultRolesSeed : ISeedDefinition
{
	private readonly ILogger<DefaultRolesSeed> mLogger;
	private readonly TimeProvider              mTimeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultRolesSeed"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	public DefaultRolesSeed(ILogger<DefaultRolesSeed> logger, TimeProvider timeProvider)
	{
		mLogger = logger;
		mTimeProvider = timeProvider;
	}

	/// <inheritdoc/>
	public string SeedId => "DefaultRoles";

	/// <inheritdoc/>
	public int Version => 1;

	/// <inheritdoc/>
	public string Description => "Seeds the default user roles (admin, user, moderator)";

	/// <inheritdoc/>
	public async Task ExecuteAsync(LumaCoreDbContext dbContext, CancellationToken cancellationToken)
	{
		// Get existing roles (case-insensitive check)
		List<string> existingRoleNames = await dbContext.Roles
			                                 .Select(r => r.Name.ToLower(CultureInfo.InvariantCulture))
			                                 .ToListAsync(cancellationToken)
			                                 .ConfigureAwait(false);

		// Determine which roles need to be added (idempotent check)
		var rolesToAdd = new List<RoleEntity>();
		DateTime now = mTimeProvider.GetUtcNow().UtcDateTime;

		foreach ((string name, string description) in RoleDefinitions.Defaults)
		{
			if (existingRoleNames.Contains(name.ToLower(CultureInfo.InvariantCulture)))
			{
				mLogger.LogDebug("Role '{RoleName}' already exists, skipping", name);
				continue;
			}

			rolesToAdd.Add(
				new RoleEntity
				{
					PublicId = Guid.NewGuid(),
					Name = name,
					Description = description,
					CreatedAtUtc = now
				});
		}

		// Add new roles to the database
		if (rolesToAdd.Count > 0)
		{
			dbContext.Roles.AddRange(rolesToAdd);

			// Note: SaveChanges is called by SeedExecutor to include SeedHistory in same transaction

			mLogger.LogInformation(
				"Seeded {SeededRoleCount} default role(s): {SeededRoleNames}",
				rolesToAdd.Count,
				string.Join(", ", rolesToAdd.Select(r => r.Name)));
		}
		else
		{
			mLogger.LogDebug("All default roles already exist");
		}
	}
}
