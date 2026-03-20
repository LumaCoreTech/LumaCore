// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Tests.Infrastructure;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LumaCoreDataServiceTests
{
	/// <summary>
	/// Base class providing shared fixture setup and helper methods for nested test classes.
	/// Each nested class inherits from this to get a fresh database per test.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The fixture is created via <see cref="DbFixture.Create()"/>, which resolves the database provider from
	///     <see cref="DbTestSettingsLoader"/> (defaults to SQLite in-memory). Tests exercise real EF Core behavior
	///     (constraints, transactions, set-based operations) regardless of the configured provider.
	///     </para>
	///     <para>
	///     Each nested test class gets its own fixture instance; <see cref="IAsyncLifetime.InitializeAsync"/> creates the
	///     database schema and <see cref="IAsyncLifetime.DisposeAsync"/> tears it down to keep tests isolated.
	///     </para>
	/// </remarks>
	public abstract class TestBase : IAsyncLifetime
	{
		/// <summary>
		/// Provides a dedicated database fixture for the test instance, resolved via <see cref="DbFixture.Create()"/>.
		/// </summary>
		protected readonly DbFixture Fixture = DbFixture.Create();

		/// <summary>
		/// Disposes the underlying database resources for the test instance.
		/// </summary>
		/// <returns>A task that represents the asynchronous dispose operation.</returns>
		public ValueTask DisposeAsync() => Fixture.DisposeAsync();

		/// <summary>
		/// Initializes the database schema for the test instance.
		/// </summary>
		/// <returns>A task that represents the asynchronous initialization operation.</returns>
		public ValueTask InitializeAsync() => Fixture.InitializeAsync();

		/// <summary>
		/// Helper: Creates a user participant (Participant + User row) for tests that require a valid user creator.
		/// </summary>
		/// <param name="username">The username for both the display name and user account.</param>
		/// <param name="email">The email address for the user account.</param>
		/// <param name="utcNow">The UTC timestamp used for <see cref="ParticipantEntity.CreatedAtUtc"/>.</param>
		/// <returns>The created <see cref="ParticipantEntity"/> that is backed by a <see cref="UserEntity"/> row.</returns>
		/// <remarks>
		/// Convenience overload that delegates to <see cref="CreateUserParticipantWithUserAsync"/> and discards the
		/// <see cref="UserEntity"/>. Many service methods require a "user participant" but do not need the user
		/// identity directly.
		/// </remarks>
		protected async Task<ParticipantEntity> CreateUserParticipantAsync(
			string   username,
			string   email,
			DateTime utcNow)
		{
			(ParticipantEntity participant, UserEntity _) = await CreateUserParticipantWithUserAsync(
					                                                username,
					                                                email,
					                                                utcNow)
				                                                .ConfigureAwait(false);

			return participant;
		}

		/// <summary>
		/// Helper: Creates a user participant and returns both the participant and user entities.
		/// </summary>
		/// <param name="username">The username for both the display name and user account.</param>
		/// <param name="email">The email address for the user account.</param>
		/// <param name="utcNow">The UTC timestamp used for <see cref="ParticipantEntity.CreatedAtUtc"/>.</param>
		/// <returns>
		/// A tuple containing the created <see cref="ParticipantEntity"/> and <see cref="UserEntity"/>.
		/// </returns>
		/// <remarks>
		/// Use this helper when a test needs both identities (participant id for conversation membership and user id for
		/// role assignment/user deletion scenarios).
		/// </remarks>
		protected async Task<(ParticipantEntity Participant, UserEntity User)> CreateUserParticipantWithUserAsync(
			string   username,
			string   email,
			DateTime utcNow)
		{
			var participant = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = username,
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(participant);
			await Fixture.DbContext.SaveChangesAsync().ConfigureAwait(false);

			var user = new UserEntity
			{
				ParticipantId = participant.Id,
				Username = username,
				UsernameNormalized = username.Trim().ToUpperInvariant(),
				Email = email,
				PasswordHash = "hash"
			};
			Fixture.DbContext.Users.Add(user);
			await Fixture.DbContext.SaveChangesAsync().ConfigureAwait(false);

			return (participant, user);
		}
	}
}
