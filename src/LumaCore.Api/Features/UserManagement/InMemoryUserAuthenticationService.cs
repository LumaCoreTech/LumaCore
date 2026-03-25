// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.UserManagement;

/// <summary>
/// A dictionary-backed <see cref="IUserAuthenticationService"/> that stores credentials in memory.
/// </summary>
/// <remarks>
///     <para>
///     This implementation is intended as a bootstrap mechanism so that the authentication endpoints can
///     function before a persistent user store (e.g., database-backed credentials with password hashing)
///     is available. It must be replaced before the system is exposed to untrusted networks.
///     </para>
///     <para>
///     Username lookup is case-insensitive; password comparison is case-sensitive and uses ordinal matching
///     (no culture-dependent collation). Usernames are trimmed before lookup.
///     </para>
/// </remarks>
sealed class InMemoryUserAuthenticationService : IUserAuthenticationService
{
	private readonly Dictionary<string, (string Password, string[] Roles)> mUsers = new(
		StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Registers a user with the given credentials and roles. If the username already exists, the
	/// previous entry is overwritten.
	/// </summary>
	/// <param name="username">The username.</param>
	/// <param name="password">The password (stored as-is — no hashing).</param>
	/// <param name="roles">The roles assigned to the user.</param>
	public void AddUser(string username, string password, params string[] roles)
	{
		mUsers[username] = (password, roles);
	}

	/// <inheritdoc/>
	public Task<AuthenticatedUser?> AuthenticateAsync(
		string            username,
		string            password,
		CancellationToken cancellationToken = default)
	{
		string trimmed = username.Trim();

		if (!mUsers.TryGetValue(trimmed, out (string Password, string[] Roles) user) ||
		    !string.Equals(password, user.Password, StringComparison.Ordinal))
		{
			return Task.FromResult<AuthenticatedUser?>(null);
		}

		return Task.FromResult<AuthenticatedUser?>(new AuthenticatedUser(trimmed, user.Roles));
	}
}
