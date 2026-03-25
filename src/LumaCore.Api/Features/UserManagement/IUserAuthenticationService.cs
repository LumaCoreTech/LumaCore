// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.UserManagement;

/// <summary>
/// Defines a service that validates user credentials and returns the authenticated user's identity.
/// </summary>
/// <remarks>
///     <para>
///     This interface decouples the authentication endpoints from any specific credential store. The login
///     endpoint delegates credential validation to this service and builds JWT claims from the returned
///     <see cref="AuthenticatedUser"/> — it never inspects raw credentials itself.
///     </para>
///     <para>
///     The current production implementation (<see cref="InMemoryUserAuthenticationService"/>) is seeded with
///     a single bootstrap account at startup. Once persistent user management is available, a database-backed
///     implementation will replace it without requiring changes to the authentication endpoints.
///     </para>
/// </remarks>
interface IUserAuthenticationService
{
	/// <summary>
	/// Validates the supplied credentials and returns the authenticated user's identity on success.
	/// </summary>
	/// <param name="username">The username to authenticate.</param>
	/// <param name="password">The password to validate.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// An <see cref="AuthenticatedUser"/> with the canonical username and assigned roles if the credentials are
	/// valid; otherwise, <see langword="null"/>.
	/// </returns>
	Task<AuthenticatedUser?> AuthenticateAsync(
		string            username,
		string            password,
		CancellationToken cancellationToken = default);
}
