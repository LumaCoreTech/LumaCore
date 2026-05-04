// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Claims;
using System.Text.Json;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Data.Entities;
using LumaCore.Data.Services;

using V1 = LumaCore.Api.Contracts.V1.User;

namespace LumaCore.Api.Features.User;

/// <summary>
/// Provides extension methods for mapping authenticated-user self-service endpoints to the routing pipeline.
/// </summary>
/// <remarks>
///     <para>
///     This feature groups all endpoints that let the <b>authenticated user</b> manage their own account
///     (preferences, profile, sessions, etc.) under <c>/api/v1/user</c>.
///     </para>
///     <para>Currently exposed sub-resources:</para>
///     <list type="bullet">
///         <item><c>GET  /api/v1/user/preferences</c> — Retrieve preferences.</item>
///         <item><c>PUT  /api/v1/user/preferences</c> — Update preferences.</item>
///     </list>
///     <para>
///     All endpoints require authentication. The current user is resolved from the JWT <c>name</c> claim
///     via <see cref="ResolveUserIdAsync"/> — a shared helper available to all sub-resources.
///     </para>
///     <para>
///     This feature is distinct from <c>Features/UserManagement</c>, which provides <b>admin-level</b>
///     operations on user accounts (CRUD, role assignment, etc.).
///     </para>
/// </remarks>
static class EndpointMapping
{
	private static readonly JsonSerializerOptions sJsonOptions = new(JsonSerializerDefaults.Web);

	/// <summary>
	/// Maps authenticated-user self-service endpoints to the versioned API group.
	/// </summary>
	/// <param name="endpoints">The <see cref="RouteGroupBuilder"/> for the versioned API.</param>
	/// <returns>The <paramref name="endpoints"/> for method chaining.</returns>
	public static RouteGroupBuilder MapUserFeature(this RouteGroupBuilder endpoints)
	{
		RouteGroupBuilder user = endpoints
			.MapGroup("/user")
			.WithTags("User")
			.RequireAuthorization();

		MapPreferencesEndpoints(user);

		return user;
	}

	/// <summary>
	/// Maps the <c>/preferences</c> sub-resource for reading and writing the user's application preferences.
	/// </summary>
	/// <param name="user">The <c>/user</c> route group.</param>
	private static void MapPreferencesEndpoints(RouteGroupBuilder user)
	{
		// ────────────────────────────────────────────────────────────────────────
		// GET /api/v1/user/preferences
		// Returns the authenticated user's application preferences.
		// ────────────────────────────────────────────────────────────────────────
		user.MapGet(
				"/preferences",
				async (
					ClaimsPrincipal   claimsPrincipal,
					IUserDataService  userDataService,
					CancellationToken cancellationToken) =>
				{
					UserId userId = await ResolveUserIdAsync(claimsPrincipal, userDataService, cancellationToken)
						                .ConfigureAwait(false);

					string? json = await userDataService
						               .GetPreferencesJsonAsync(userId, cancellationToken)
						               .ConfigureAwait(false);

					V1.UserPreferencesResponse response = json is not null
						                                      ? JsonSerializer.Deserialize<V1.UserPreferencesResponse>(
							                                        json,
							                                        sJsonOptions)
						                                        ?? new V1.UserPreferencesResponse(null)
						                                      : new V1.UserPreferencesResponse(null);

					return Results.Ok(response);
				})
			.MapToApiVersion(ApiVersions.V1)
			.Produces<V1.UserPreferencesResponse>(StatusCodes.Status200OK)
			.WithSummary("Gets the authenticated user's preferences.")
			.WithDescription(
				"Returns the application preferences for the currently authenticated user. " +
				"Returns default (empty) preferences if none have been saved yet.")
			.WithName("GetUserPreferences");

		// ────────────────────────────────────────────────────────────────────────
		// PUT /api/v1/user/preferences
		// Updates the authenticated user's application preferences.
		// ────────────────────────────────────────────────────────────────────────
		user.MapPut(
				"/preferences",
				async (
					V1.UpdateUserPreferencesRequest request,
					ClaimsPrincipal                 claimsPrincipal,
					IUserDataService                userDataService,
					CancellationToken               cancellationToken) =>
				{
					UserId userId = await ResolveUserIdAsync(claimsPrincipal, userDataService, cancellationToken)
						                .ConfigureAwait(false);

					// Serialize the typed request into a JSON blob for storage.
					var preferences = new V1.UserPreferencesResponse(request.RecentEmojis);
					string json = JsonSerializer.Serialize(preferences, sJsonOptions);

					await userDataService
						.UpdatePreferencesJsonAsync(userId, json, cancellationToken)
						.ConfigureAwait(false);

					return Results.NoContent();
				})
			.MapToApiVersion(ApiVersions.V1)
			.Produces(StatusCodes.Status204NoContent)
			.WithSummary("Updates the authenticated user's preferences.")
			.WithDescription(
				"Replaces the application preferences for the currently authenticated user. " +
				"Send the full preferences object (read-modify-write). " +
				"Properties set to null are treated as 'use default'.")
			.WithName("UpdateUserPreferences");
	}

	/// <summary>
	/// Resolves the authenticated user's JWT identity to a database <see cref="UserId"/>.
	/// </summary>
	/// <remarks>
	/// Shared helper for all sub-resource endpoints in this feature.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// The authenticated user has no name claim or does not exist in the database.
	/// </exception>
	private static async Task<UserId> ResolveUserIdAsync(
		ClaimsPrincipal   user,
		IUserDataService  userDataService,
		CancellationToken cancellationToken)
	{
		string username = user.Identity?.Name ??
		                  throw new InvalidOperationException("Authenticated user has no name claim.");

		UserEntity? dbUser = await userDataService
			                     .GetUserByUsernameAsync(username, cancellationToken)
			                     .ConfigureAwait(false);

		return dbUser?.Id ??
		       throw new InvalidOperationException($"User '{username}' not found in database.");
	}
}
