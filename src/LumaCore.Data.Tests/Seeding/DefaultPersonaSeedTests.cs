// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Seeding;
using LumaCore.Data.Tests.Infrastructure;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.Seeding;

// DefaultPersonaSeed: idempotent three-phase insert that resolves the circular FK between
// PersonaEntity.ActiveSystemPromptId and SystemPromptEntity.PersonaId.
//
//   1. Metadata: stable SeedId / Version / Description (contract for SeedExecutor).
//   2. ExecuteAsync first time on an empty DB: creates Participant + Persona + SystemPrompt,
//      links them via two intermediate SaveChangesAsync calls, and resolves the circular FK
//      so PersonaEntity.ActiveSystemPromptId points at the freshly-created SystemPrompt.
//   3. ExecuteAsync second time (same DB): idempotency — no duplicate rows are inserted.

/// <summary>
/// Tests for <see cref="DefaultPersonaSeed"/>: verifies the seed metadata contract, the
/// three-phase insert that resolves the circular foreign key between
/// <see cref="PersonaEntity.ActiveSystemPromptId"/> and <see cref="SystemPromptEntity.PersonaId"/>,
/// and idempotency on repeat execution.
/// </summary>
[Trait("Category", "Seeding")]
public sealed class DefaultPersonaSeedTests : IAsyncLifetime
{
	// Must follow the configured provider (not hardcoded SQLite) to stay consistent with the rest of the
	// test suite and to avoid binding any incidentally-triggered EF.CompileAsyncQuery delegate to a
	// SQLite model. See CompiledQueryRegressionTests.cs for the full rationale on compiled-query model
	// affinity and why provider hardcoding poisons the static query cache.
	private readonly DbFixture mFixture = DbFixture.Create();

	/// <summary>
	/// Initializes the database schema for the test instance.
	/// </summary>
	/// <returns>A task that represents the asynchronous initialization operation.</returns>
	public ValueTask InitializeAsync() => mFixture.InitializeAsync();

	/// <summary>
	/// Disposes the underlying database resources.
	/// </summary>
	/// <returns>A task that represents the asynchronous dispose operation.</returns>
	public ValueTask DisposeAsync() => mFixture.DisposeAsync();

	/// <summary>
	/// Creates a <see cref="DefaultPersonaSeed"/> backed by a <see cref="FakeTimeProvider"/>
	/// pinned to a deterministic UTC instant.
	/// </summary>
	/// <param name="utcNow">The fixed UTC instant the seed should observe via <see cref="TimeProvider"/>.</param>
	/// <returns>A new <see cref="DefaultPersonaSeed"/> ready for tests.</returns>
	private static DefaultPersonaSeed CreateSut(DateTime utcNow)
	{
		var time = new FakeTimeProvider(new DateTimeOffset(utcNow, TimeSpan.Zero));
		return new DefaultPersonaSeed(NullLogger<DefaultPersonaSeed>.Instance, time);
	}

	#region Metadata

	/// <summary>
	/// Verifies that <see cref="DefaultPersonaSeed.SeedId"/> is the stable identifier consumed by
	/// <c>SeedExecutor</c> to deduplicate seed runs across application restarts.
	/// </summary>
	[Fact]
	public void SeedId_ReturnsStableIdentifier()
	{
		// Arrange
		DefaultPersonaSeed sut = CreateSut(new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc));

		// Act + Assert
		Assert.Equal("DefaultPersonas", sut.SeedId);
	}

	/// <summary>
	/// Verifies that <see cref="DefaultPersonaSeed.Version"/> is the schema version observed by
	/// <c>SeedExecutor</c> when deciding whether the seed must run again after upgrades.
	/// </summary>
	[Fact]
	public void Version_ReturnsExpectedSchemaVersion()
	{
		// Arrange
		DefaultPersonaSeed sut = CreateSut(new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc));

		// Act + Assert
		Assert.Equal(1, sut.Version);
	}

	/// <summary>
	/// Verifies that <see cref="DefaultPersonaSeed.Description"/> exposes a non-empty human-readable
	/// description used by diagnostic logging in <c>SeedExecutor</c>.
	/// </summary>
	[Fact]
	public void Description_ReturnsNonEmptyText()
	{
		// Arrange
		DefaultPersonaSeed sut = CreateSut(new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc));

		// Act + Assert
		Assert.Equal("Seeds the default AI persona (Mila)", sut.Description);
	}

	#endregion

	#region ExecuteAsync

	/// <summary>
	/// Verifies the happy path of the three-phase insert: starting from an empty database,
	/// <see cref="DefaultPersonaSeed.ExecuteAsync"/> creates exactly one Participant, one
	/// <see cref="PersonaEntity"/> and one <see cref="SystemPromptEntity"/>, and resolves the
	/// circular foreign key so <see cref="PersonaEntity.ActiveSystemPromptId"/> points at the
	/// newly created <see cref="SystemPromptEntity"/>.
	/// </summary>
	/// <returns>A task that represents the asynchronous test operation.</returns>
	[Fact]
	public async Task ExecuteAsync_WhenDatabaseIsEmpty_InsertsParticipantPersonaAndSystemPromptAndResolvesCircularFk()
	{
		// Arrange
		DateTime utcNow = new(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);
		DefaultPersonaSeed sut = CreateSut(utcNow);

		// Act — production seed flow ends with a final SaveChanges by SeedExecutor; emulate it
		// here so the Phase-3 ActiveSystemPromptId update lands on disk for assertions.
		await sut.ExecuteAsync(mFixture.DbContext, CancellationToken.None);
		await mFixture.DbContext.SaveChangesAsync();

		// Assert: exactly one row of each entity exists, with the expected wiring.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ParticipantEntity participant = await verify.Participants.AsNoTracking().SingleAsync();
		PersonaEntity persona = await verify.Personas.AsNoTracking().SingleAsync();
		SystemPromptEntity prompt = await verify.SystemPrompts.AsNoTracking().SingleAsync();
		List<PersonaDescriptionTranslationEntity> translations = await verify.PersonaDescriptionTranslations
			.AsNoTracking()
			.OrderBy(t => t.CultureCode)
			.ToListAsync();

		Assert.Equal("Mila", participant.DisplayName);
		Assert.Equal(utcNow, participant.CreatedAtUtc);
		Assert.NotEqual(Guid.Empty, participant.PublicId);

		Assert.Equal(participant.Id, persona.ParticipantId);
		Assert.True(persona.IsActive);
		Assert.Equal(PersonaVisibility.Shared, persona.Visibility);
		Assert.Null(persona.CreatedByParticipantId);
		Assert.Null(persona.DefaultModel);

		// Phase 3 wiring: ActiveSystemPromptId must point at the freshly-created prompt.
		Assert.Equal(prompt.Id, persona.ActiveSystemPromptId);
		Assert.Equal(persona.Id, prompt.PersonaId);
		Assert.Equal(utcNow, prompt.CreatedAtUtc);
		Assert.NotEqual(Guid.Empty, prompt.PublicId);
		Assert.Equal(64, prompt.Hash.Length); // SHA-256 hex
		Assert.False(string.IsNullOrWhiteSpace(prompt.Content));

		// Description translations: two entries (en + de), manually written by the team.
		Assert.Equal(2, translations.Count);
		Assert.All(translations, t => Assert.Equal(persona.Id, t.PersonaId));
		Assert.All(translations, t => Assert.Equal(TranslationSource.Manual, t.Source));
		Assert.Contains(translations, t => t.CultureCode == "en" && t.Value.StartsWith("Mila is an optimistic", StringComparison.Ordinal));
		Assert.Contains(translations, t => t.CultureCode == "de" && t.Value.StartsWith("Mila ist eine optimistische", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies that <see cref="DefaultPersonaSeed.ExecuteAsync"/> is idempotent: a second
	/// invocation against a database that already contains the Mila persona is a no-op — no
	/// duplicate Participant, Persona or SystemPrompt rows are inserted.
	/// </summary>
	/// <returns>A task that represents the asynchronous test operation.</returns>
	[Fact]
	public async Task ExecuteAsync_WhenAlreadySeeded_IsIdempotentAndDoesNotInsertDuplicates()
	{
		// Arrange — first run primes the DB.
		DateTime utcNow = new(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);
		DefaultPersonaSeed sut = CreateSut(utcNow);
		await sut.ExecuteAsync(mFixture.DbContext, CancellationToken.None);
		await mFixture.DbContext.SaveChangesAsync();

		// Act — second invocation must short-circuit on the idempotency check.
		await sut.ExecuteAsync(mFixture.DbContext, CancellationToken.None);
		await mFixture.DbContext.SaveChangesAsync();

		// Assert: still exactly one of each entity.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.Equal(1, await verify.Participants.AsNoTracking().CountAsync(p => p.DisplayName == "Mila"));
		Assert.Equal(1, await verify.Personas.AsNoTracking().CountAsync());
		Assert.Equal(1, await verify.SystemPrompts.AsNoTracking().CountAsync());
		Assert.Equal(2, await verify.PersonaDescriptionTranslations.AsNoTracking().CountAsync());
	}

	#endregion
}
