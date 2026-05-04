// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text;

using LumaCore.Data.Entities;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.Extensions.Time.Testing;

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
	///     database schema and <see cref="IAsyncDisposable.DisposeAsync"/> tears it down to keep tests isolated.
	///     </para>
	/// </remarks>
	public abstract class TestBase : IAsyncLifetime
	{
		/// <summary>
		/// Provides a dedicated database fixture for the test instance, resolved via <see cref="DbFixture.Create()"/>.
		/// </summary>
		protected DbFixture Fixture { get; } = DbFixture.Create();

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize

		/// <summary>
		/// Disposes the underlying database resources for the test instance.
		/// </summary>
		/// <returns>A task that represents the asynchronous dispose operation.</returns>
		public ValueTask DisposeAsync() => Fixture.DisposeAsync();

#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

		/// <summary>
		/// Initializes the database schema for the test instance.
		/// </summary>
		/// <returns>A task that represents the asynchronous initialization operation.</returns>
		public ValueTask InitializeAsync() => Fixture.InitializeAsync();

		/// <summary>
		/// Helper: Creates a deterministic <see cref="FakeTimeProvider"/> seeded with the supplied UTC instant.
		/// </summary>
		/// <param name="utcNow">The UTC instant the clock should report from <see cref="TimeProvider.GetUtcNow"/>.</param>
		/// <returns>
		/// A fresh <see cref="FakeTimeProvider"/> whose current time equals <paramref name="utcNow"/>.
		/// </returns>
		/// <remarks>
		/// Used by the "<c>WhenUtcNowIsNull_UsesInjectedTimeProvider</c>" test family to verify that every public
		/// mutation method falls back to the injected <see cref="TimeProvider"/> when the optional <c>utcNow</c>
		/// argument is omitted. <see cref="DateTime.Kind"/> is normalized to <see cref="DateTimeKind.Utc"/> so
		/// callers do not have to remember the kind themselves.
		/// </remarks>
		protected static FakeTimeProvider CreateClock(DateTime utcNow)
		{
			return new FakeTimeProvider(new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)));
		}

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
				CreatedAtUtc = utcNow,
				Username = username,
				UsernameNormalized = username.Trim().ToUpperInvariant(),
				Email = email,
				PasswordHash = "hash"
			};
			Fixture.DbContext.Users.Add(user);
			await Fixture.DbContext.SaveChangesAsync().ConfigureAwait(false);

			return (participant, user);
		}

		/// <summary>
		/// Helper: Creates a persona participant (Participant + Persona row) for tests that require a persona member
		/// in a conversation, an avatar owner, or any other persona-anchored scenario.
		/// </summary>
		/// <param name="displayName">The display name for the participant.</param>
		/// <param name="utcNow">
		/// The UTC timestamp used for both <see cref="ParticipantEntity.CreatedAtUtc"/> and
		/// <see cref="PersonaEntity.UpdatedAtUtc"/>.
		/// </param>
		/// <param name="createdByParticipantId">
		/// Optional creator participant id. <see langword="null"/> represents a system-created persona.
		/// </param>
		/// <returns>
		/// The created <see cref="ParticipantEntity"/> backed by a <see cref="PersonaEntity"/> row.
		/// </returns>
		protected async Task<ParticipantEntity> CreatePersonaParticipantAsync(
			string         displayName,
			DateTime       utcNow,
			ParticipantId? createdByParticipantId = null)
		{
			var participant = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = displayName,
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.Participants.Add(participant);
			await Fixture.DbContext.SaveChangesAsync().ConfigureAwait(false);

			var persona = new PersonaEntity
			{
				ParticipantId = participant.Id,
				CreatedByParticipantId = createdByParticipantId,
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};
			Fixture.DbContext.Personas.Add(persona);
			await Fixture.DbContext.SaveChangesAsync().ConfigureAwait(false);

			return participant;
		}

		/// <summary>
		/// Helper: Buffers the supplied UTF-8 string into a non-writable <see cref="MemoryStream"/> for upload.
		/// </summary>
		/// <param name="content">The string content to wrap.</param>
		/// <returns>A <see cref="MemoryStream"/> positioned at zero.</returns>
		protected static MemoryStream MakeStream(string content) =>
			new(Encoding.UTF8.GetBytes(content), writable: false);
	}
}
