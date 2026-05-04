// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text;

using LumaCore.Core.IO;
using LumaCore.Data.Entities;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LumaCoreDataServiceTests
{
	/// <summary>
	/// Tests for <see cref="IPersonaDataService"/> methods.
	/// </summary>
	/// <remarks>
	///     <para>
	///     These tests cover the full persona lifecycle: creation (with ownership and visibility),
	///     user-scoped listing, detail lookup, updates (including system prompt versioning with SHA-256
	///     deduplication), soft-deletion (deactivation), and cloning.
	///     </para>
	///     <para>
	///     The suite exercises the 3-phase save pattern required by the circular FK between
	///     <see cref="PersonaEntity.ActiveSystemPromptId"/> and <see cref="SystemPromptEntity.PersonaId"/>,
	///     as well as the ownership/visibility model where <see cref="PersonaVisibility.Private"/> personas
	///     are only visible to their creator and <see cref="PersonaVisibility.Shared"/> personas are visible
	///     to all authenticated users.
	///     </para>
	/// </remarks>
	[Trait("Category", "Services")]
	public sealed class Personas : TestBase
	{
		#region GetPersonasForUserAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonasForUserAsync"/> returns shared personas and
		/// the requesting user's own private personas, but excludes another user's private personas.
		/// </summary>
		[Fact]
		public async Task GetPersonasForUserAsync_WhenSharedAndOwnPrivateExist_ReturnsBothExcludingOtherUsersPrivate()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity userA = await CreateUserParticipantAsync("UserA", "a@test.test", utcNow);
			ParticipantEntity userB = await CreateUserParticipantAsync("UserB", "b@test.test", utcNow);

			// Shared persona (visible to everyone)
			PersonaEntity shared = await service.CreatePersonaAsync(
				                       displayName: "SharedBot",
				                       descriptionTranslations: new Dictionary<string, string>
					                       { { "en", "A shared persona." } },
				                       defaultModel: null,
				                       systemPrompt: null,
				                       visibility: PersonaVisibility.Shared,
				                       creatorParticipantId: userA.Id,
				                       utcNow: utcNow);

			// User A's private persona
			PersonaEntity ownPrivate = await service.CreatePersonaAsync(
				                           displayName: "MyPrivateBot",
				                           descriptionTranslations: new Dictionary<string, string>
					                           { { "en", "User A's private persona." } },
				                           defaultModel: null,
				                           systemPrompt: null,
				                           visibility: PersonaVisibility.Private,
				                           creatorParticipantId: userA.Id,
				                           utcNow: utcNow);

			// User B's private persona (should NOT be visible to User A)
			await service.CreatePersonaAsync(
				displayName: "OtherPrivateBot",
				descriptionTranslations: new Dictionary<string, string> { { "en", "User B's private persona." } },
				defaultModel: null,
				systemPrompt: null,
				visibility: PersonaVisibility.Private,
				creatorParticipantId: userB.Id,
				utcNow: utcNow);

			// Act
			IReadOnlyList<PersonaEntity> result = await service.GetPersonasForUserAsync(userA.Id);

			// Assert
			Assert.Equal(2, result.Count);
			Assert.Contains(result, p => p.Id == shared.Id);
			Assert.Contains(result, p => p.Id == ownPrivate.Id);
			Assert.DoesNotContain(result, p => p.Participant!.DisplayName == "OtherPrivateBot");

			// Verify navigations are loaded
			Assert.All(result, p => Assert.NotNull(p.Participant));
			Assert.All(result, p => Assert.NotNull(p.CreatedByParticipant));
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonasForUserAsync"/> returns an empty list when only
		/// another user's private personas exist and no shared personas are available.
		/// </summary>
		[Fact]
		public async Task GetPersonasForUserAsync_WhenOnlyOtherUsersPrivateExist_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity userA = await CreateUserParticipantAsync("UserA", "a@test.test", utcNow);
			ParticipantEntity userB = await CreateUserParticipantAsync("UserB", "b@test.test", utcNow);

			await service.CreatePersonaAsync(
				displayName: "HiddenBot",
				descriptionTranslations: null,
				defaultModel: null,
				systemPrompt: null,
				visibility: PersonaVisibility.Private,
				creatorParticipantId: userB.Id,
				utcNow: utcNow);

			// Act
			IReadOnlyList<PersonaEntity> result = await service.GetPersonasForUserAsync(userA.Id);

			// Assert
			Assert.Empty(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonasForUserAsync"/> excludes inactive personas
		/// from the results even when they belong to the requesting user.
		/// </summary>
		[Fact]
		public async Task GetPersonasForUserAsync_WhenInactivePersonaExists_ExcludesInactive()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity user = await CreateUserParticipantAsync("Owner", "owner@test.test", utcNow);

			PersonaEntity active = await service.CreatePersonaAsync(
				                       displayName: "ActiveBot",
				                       descriptionTranslations: null,
				                       defaultModel: null,
				                       systemPrompt: null,
				                       visibility: PersonaVisibility.Private,
				                       creatorParticipantId: user.Id,
				                       utcNow: utcNow);

			PersonaEntity toDeactivate = await service.CreatePersonaAsync(
				                             displayName: "InactiveBot",
				                             descriptionTranslations: null,
				                             defaultModel: null,
				                             systemPrompt: null,
				                             visibility: PersonaVisibility.Private,
				                             creatorParticipantId: user.Id,
				                             utcNow: utcNow);

			await service.DeactivatePersonaAsync(toDeactivate.Participant!.PublicId, utcNow);

			// Act
			IReadOnlyList<PersonaEntity> result = await service.GetPersonasForUserAsync(user.Id);

			// Assert
			Assert.Single(result);
			Assert.Equal(active.Id, result[0].Id);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonasForUserAsync"/> returns personas ordered
		/// alphabetically by <see cref="ParticipantEntity.DisplayName"/>.
		/// </summary>
		[Fact]
		public async Task GetPersonasForUserAsync_WhenMultiplePersonasExist_ReturnsOrderedByDisplayName()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity user = await CreateUserParticipantAsync("Owner", "owner@test.test", utcNow);

			// Create in non-alphabetical order
			await service.CreatePersonaAsync(
				displayName: "Zara",
				descriptionTranslations: null,
				defaultModel: null,
				systemPrompt: null,
				visibility: PersonaVisibility.Shared,
				creatorParticipantId: user.Id,
				utcNow: utcNow);

			await service.CreatePersonaAsync(
				displayName: "Atlas",
				descriptionTranslations: null,
				defaultModel: null,
				systemPrompt: null,
				visibility: PersonaVisibility.Shared,
				creatorParticipantId: user.Id,
				utcNow: utcNow);

			await service.CreatePersonaAsync(
				displayName: "Nova",
				descriptionTranslations: null,
				defaultModel: null,
				systemPrompt: null,
				visibility: PersonaVisibility.Shared,
				creatorParticipantId: user.Id,
				utcNow: utcNow);

			// Act
			IReadOnlyList<PersonaEntity> result = await service.GetPersonasForUserAsync(user.Id);

			// Assert
			Assert.Equal(3, result.Count);
			Assert.Equal("Atlas", result[0].Participant!.DisplayName);
			Assert.Equal("Nova", result[1].Participant!.DisplayName);
			Assert.Equal("Zara", result[2].Participant!.DisplayName);
		}

		#endregion

		#region GetPersonaByPublicIdAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaByPublicIdAsync"/> returns the persona with
		/// <see cref="PersonaEntity.Participant"/> and <see cref="PersonaEntity.ActiveSystemPrompt"/> loaded.
		/// The toggle parameter asserts the contract — both the regular EF branch and the compiled hot-path
		/// branch (delegating to <c>PersonaQueries.GetByPublicId</c>) must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetPersonaByPublicIdAsync_WhenExists_ReturnsPersonaWithNavigations(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "Atlas",
				                        descriptionTranslations: new Dictionary<string, string>
					                        { { "en", "A knowledgeable assistant." } },
				                        defaultModel: "mistral:7b",
				                        systemPrompt: "You are Atlas.",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			Guid publicId = created.Participant!.PublicId;

			// Act
			PersonaEntity? result = await service.GetPersonaByPublicIdAsync(publicId);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("Atlas", result.Participant!.DisplayName);
			Assert.Single(result.DescriptionTranslations);
			Assert.Equal("A knowledgeable assistant.", result.DescriptionTranslations.First().Value);
			Assert.Equal("mistral:7b", result.DefaultModel);
			Assert.True(result.IsActive);
			Assert.NotNull(result.ActiveSystemPrompt);
			Assert.Equal("You are Atlas.", result.ActiveSystemPrompt!.Content);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaByPublicIdAsync"/> returns <see langword="null"/>
		/// for a non-existent public ID. The toggle parameter asserts the contract — both the regular EF branch
		/// and the compiled hot-path branch must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetPersonaByPublicIdAsync_WhenNotFound_ReturnsNull(bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			// Act
			PersonaEntity? result = await service.GetPersonaByPublicIdAsync(Guid.NewGuid());

			// Assert
			Assert.Null(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaByPublicIdAsync"/> throws
		/// <see cref="ArgumentException"/> when <see cref="Guid.Empty"/> is passed.
		/// </summary>
		[Fact]
		public async Task GetPersonaByPublicIdAsync_WhenEmptyGuid_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GetPersonaByPublicIdAsync(Guid.Empty));

			Assert.Equal("publicId", ex.ParamName);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaByPublicIdAsync"/> loads the
		/// <see cref="PersonaEntity.CreatedByParticipant"/> navigation when the persona was created by a user.
		/// The toggle parameter asserts the contract — both the regular EF branch and the compiled hot-path
		/// branch (delegating to <c>PersonaQueries.GetByPublicId</c>) must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetPersonaByPublicIdAsync_WhenCreatedByUser_LoadsCreatedByParticipant(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity creator = await CreateUserParticipantAsync("Creator", "creator@test.test", utcNow);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "OwnedBot",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: creator.Id,
				                        utcNow: utcNow);

			// Act
			PersonaEntity? result = await service.GetPersonaByPublicIdAsync(created.Participant!.PublicId);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.CreatedByParticipant);
			Assert.Equal(creator.Id, result.CreatedByParticipantId);
			Assert.Equal("Creator", result.CreatedByParticipant!.DisplayName);
		}

		#endregion

		#region GetAllActivePersonasAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetAllActivePersonasAsync"/> returns only active personas
		/// (regardless of visibility), ordered by display name. The toggle parameter asserts the contract — both
		/// the regular EF branch and the compiled hot-path branch (delegating to <c>PersonaQueries.GetAllActive</c>)
		/// must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetAllActivePersonasAsync_WhenMixedActiveAndInactive_ReturnsOnlyActiveOrderedByName(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			// Create three personas in non-alphabetical order. One is then deactivated to verify the IsActive
			// filter — a buggy implementation that returns all rows would fail the count and ordering assertion.
			PersonaEntity charlie = await service.CreatePersonaAsync(
				                        displayName: "Charlie",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Shared,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);
			PersonaEntity alice = await service.CreatePersonaAsync(
				                      displayName: "Alice",
				                      descriptionTranslations: null,
				                      defaultModel: null,
				                      systemPrompt: "Alice prompt",
				                      visibility: PersonaVisibility.Private,
				                      creatorParticipantId: null,
				                      utcNow: utcNow);
			PersonaEntity bob = await service.CreatePersonaAsync(
				                    displayName: "Bob",
				                    descriptionTranslations: null,
				                    defaultModel: null,
				                    systemPrompt: null,
				                    visibility: PersonaVisibility.Shared,
				                    creatorParticipantId: null,
				                    utcNow: utcNow);

			// Special: deactivate Charlie so we can verify the IsActive filter.
			await service.DeactivatePersonaAsync(charlie.Participant!.PublicId, utcNow);

			// Act
			IReadOnlyList<PersonaEntity> personas = await service.GetAllActivePersonasAsync();

			// Assert
			Assert.Equal(2, personas.Count);
			Assert.Equal("Alice", personas[0].Participant!.DisplayName);
			Assert.Equal("Bob", personas[1].Participant!.DisplayName);

			// Special: confirm the contract of the included navigations.
			Assert.NotNull(personas[0].ActiveSystemPrompt);
			Assert.Equal("Alice prompt", personas[0].ActiveSystemPrompt!.Content);
			Assert.Null(personas[1].ActiveSystemPrompt);

			_ = bob; // Suppress IDE unused-variable warning; bob is asserted via personas[1].
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetAllActivePersonasAsync"/> returns an empty list when no
		/// personas exist.
		/// </summary>
		[Fact]
		public async Task GetAllActivePersonasAsync_WhenNoPersonas_ReturnsEmptyList()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			IReadOnlyList<PersonaEntity> personas = await service.GetAllActivePersonasAsync();

			// Assert
			Assert.Empty(personas);
		}

		#endregion

		#region GetPersonaByParticipantIdAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaByParticipantIdAsync"/> returns the persona with
		/// <see cref="PersonaEntity.Participant"/>, <see cref="PersonaEntity.ActiveSystemPrompt"/>, and
		/// <see cref="PersonaEntity.CreatedByParticipant"/> loaded. The toggle parameter asserts the contract —
		/// both the regular EF branch and the compiled hot-path branch (delegating to
		/// <c>PersonaQueries.GetByParticipantId</c>) must yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetPersonaByParticipantIdAsync_WhenExists_ReturnsPersonaWithNavigations(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity creator = await CreateUserParticipantAsync("Creator", "creator@test.test", utcNow);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "Atlas",
				                        descriptionTranslations: new Dictionary<string, string>
					                        { { "en", "A knowledgeable assistant." } },
				                        defaultModel: "mistral:7b",
				                        systemPrompt: "You are Atlas.",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: creator.Id,
				                        utcNow: utcNow);

			// Act
			PersonaEntity? result = await service.GetPersonaByParticipantIdAsync(created.ParticipantId);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(created.Id, result.Id);
			Assert.Equal("Atlas", result.Participant!.DisplayName);
			Assert.Single(result.DescriptionTranslations);
			Assert.Equal("A knowledgeable assistant.", result.DescriptionTranslations.First().Value);
			Assert.NotNull(result.ActiveSystemPrompt);
			Assert.Equal("You are Atlas.", result.ActiveSystemPrompt!.Content);
			Assert.NotNull(result.CreatedByParticipant);
			Assert.Equal("Creator", result.CreatedByParticipant!.DisplayName);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaByParticipantIdAsync"/> returns
		/// <see langword="null"/> when no persona is linked to the given participant.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetPersonaByParticipantIdAsync_WhenNotFound_ReturnsNull(bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			// Act — use a participant id that cannot exist (very high value).
			PersonaEntity? result = await service.GetPersonaByParticipantIdAsync(new ParticipantId(999_999));

			// Assert
			Assert.Null(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaByParticipantIdAsync"/> validates
		/// <c>participantId</c> and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task GetPersonaByParticipantIdAsync_WhenParticipantIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.GetPersonaByParticipantIdAsync(participantId: new ParticipantId(0)));
			Assert.Equal("participantId.Value", ex.ParamName);
		}

		#endregion

		#region GetCurrentSystemPromptAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetCurrentSystemPromptAsync"/> returns the most recently
		/// created prompt for the persona. The toggle parameter asserts the contract — both the regular EF branch
		/// and the compiled hot-path branch (delegating to <c>PersonaQueries.GetCurrentSystemPrompt</c>) must
		/// yield the same result.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetCurrentSystemPromptAsync_WhenMultiplePromptsExist_ReturnsMostRecent(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			DateTime initial = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
			DateTime later = initial.AddHours(1);

			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "Atlas",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: "First prompt.",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: initial);

			// Special: a second prompt at a later timestamp must win over the initial one.
			await service.UpdatePersonaAsync(
				publicId: persona.Participant!.PublicId,
				displayName: "Atlas",
				descriptionTranslations: null,
				defaultModel: null,
				systemPrompt: "Second prompt.",
				visibility: PersonaVisibility.Private,
				isActive: true,
				utcNow: later);

			// Act
			SystemPromptEntity? result = await service.GetCurrentSystemPromptAsync(persona.Id);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("Second prompt.", result.Content);
			Assert.Equal(persona.Id, result.PersonaId);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetCurrentSystemPromptAsync"/> returns <see langword="null"/>
		/// when the persona has no system prompts.
		/// </summary>
		/// <param name="preferCompiledHotPathQueries">
		/// Whether to enable
		/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/> for this run.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task GetCurrentSystemPromptAsync_WhenPersonaHasNoPrompts_ReturnsNull(
			bool preferCompiledHotPathQueries)
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				o => o.PreferCompiledHotPathQueries = preferCompiledHotPathQueries);

			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			// Create a persona without any system prompt.
			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "Atlas",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			// Act
			SystemPromptEntity? result = await service.GetCurrentSystemPromptAsync(persona.Id);

			// Assert
			Assert.Null(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetCurrentSystemPromptAsync"/> validates <c>personaId</c>
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task GetCurrentSystemPromptAsync_WhenPersonaIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.GetCurrentSystemPromptAsync(personaId: new PersonaId(0)));
			Assert.Equal("personaId.Value", ex.ParamName);
		}

		#endregion

		#region CreatePersonaAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.CreatePersonaAsync"/> creates a fully populated persona
		/// with participant, description, default model, system prompt, and avatar URL.
		/// </summary>
		[Fact]
		public async Task CreatePersonaAsync_WhenValidWithSystemPrompt_CreatesFullPersonaChain()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			// Act
			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "Nova",
				                        descriptionTranslations: new Dictionary<string, string>
					                        { { "en", "A creative writing partner." } },
				                        defaultModel: "llama3.1:8b",
				                        systemPrompt: "You are Nova, a creative muse.",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			// Reload from DB to verify persistence
			PersonaEntity? reloaded = await Fixture.DbContext.Personas
				                          .AsNoTracking()
				                          .Include(p => p.Participant)
				                          .Include(p => p.ActiveSystemPrompt)
				                          .Include(p => p.DescriptionTranslations)
				                          .FirstOrDefaultAsync(p => p.Id == persona.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.NotNull(reloaded.Participant);
			Assert.NotEqual(Guid.Empty, reloaded.Participant!.PublicId);
			Assert.Equal("Nova", reloaded.Participant.DisplayName);
			Assert.Equal(utcNow, reloaded.Participant.CreatedAtUtc);

			Assert.Single(reloaded.DescriptionTranslations);
			Assert.Equal("A creative writing partner.", reloaded.DescriptionTranslations.First().Value);
			Assert.Equal("llama3.1:8b", reloaded.DefaultModel);
			Assert.True(reloaded.IsActive);

			Assert.NotNull(reloaded.ActiveSystemPrompt);
			Assert.NotEqual(Guid.Empty, reloaded.ActiveSystemPrompt!.PublicId);
			Assert.Equal("You are Nova, a creative muse.", reloaded.ActiveSystemPrompt.Content);
			Assert.Equal(utcNow, reloaded.ActiveSystemPrompt.CreatedAtUtc);
			Assert.NotEmpty(reloaded.ActiveSystemPrompt.Hash);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.CreatePersonaAsync"/> creates a persona without a system
		/// prompt when <see langword="null"/> is passed.
		/// </summary>
		[Fact]
		public async Task CreatePersonaAsync_WhenNoSystemPrompt_CreatesPersonaWithoutActivePrompt()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			// Act
			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "Echo",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			PersonaEntity? reloaded = await Fixture.DbContext.Personas
				                          .AsNoTracking()
				                          .Include(p => p.Participant)
				                          .FirstOrDefaultAsync(p => p.Id == persona.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal("Echo", reloaded.Participant!.DisplayName);
			Assert.Empty(reloaded.DescriptionTranslations);
			Assert.Null(reloaded.DefaultModel);
			Assert.Null(reloaded.ActiveSystemPromptId);
			Assert.True(reloaded.IsActive);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.CreatePersonaAsync"/> trims leading and trailing
		/// whitespace from the display name.
		/// </summary>
		[Fact]
		public async Task CreatePersonaAsync_WhenDisplayNameHasWhitespace_TrimsDisplayName()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			// Act
			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "  Spark  ",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			// Assert
			Assert.Equal("Spark", persona.Participant!.DisplayName);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.CreatePersonaAsync"/> persists
		/// <see cref="PersonaEntity.Visibility"/> and <see cref="PersonaEntity.CreatedByParticipantId"/> when
		/// creating a shared persona with an explicit creator.
		/// </summary>
		[Fact]
		public async Task CreatePersonaAsync_WhenSharedWithCreator_PersistsVisibilityAndCreator()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity creator = await CreateUserParticipantAsync("Creator", "creator@test.test", utcNow);

			// Act
			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "SharedOwned",
				                        descriptionTranslations: new Dictionary<string, string>
					                        { { "en", "A shared persona with a creator." } },
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Shared,
				                        creatorParticipantId: creator.Id,
				                        utcNow: utcNow);

			// Reload from DB to verify persistence
			PersonaEntity? reloaded = await Fixture.DbContext.Personas
				                          .AsNoTracking()
				                          .FirstOrDefaultAsync(p => p.Id == persona.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal(PersonaVisibility.Shared, reloaded.Visibility);
			Assert.Equal(creator.Id, reloaded.CreatedByParticipantId);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.CreatePersonaAsync"/> throws
		/// <see cref="ArgumentException"/> when the display name is empty.
		/// </summary>
		[Fact]
		public async Task CreatePersonaAsync_WhenDisplayNameEmpty_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			// Act + Assert
			await Assert.ThrowsAsync<ArgumentException>(() => service.CreatePersonaAsync(
				displayName: "",
				descriptionTranslations: null,
				defaultModel: null,
				systemPrompt: null,
				visibility: PersonaVisibility.Private,
				creatorParticipantId: null,
				utcNow: utcNow));
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.CreatePersonaAsync"/> throws
		/// <see cref="ArgumentNullException"/> when the display name is <see langword="null"/>.
		/// </summary>
		[Fact]
		public async Task CreatePersonaAsync_WhenDisplayNameNull_ThrowsArgumentNullException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			// Act + Assert
			await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreatePersonaAsync(
				displayName: null!,
				descriptionTranslations: null,
				defaultModel: null,
				systemPrompt: null,
				visibility: PersonaVisibility.Private,
				creatorParticipantId: null,
				utcNow: utcNow));
		}

		#endregion

		#region UpdatePersonaAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.UpdatePersonaAsync"/> updates all persona and participant
		/// fields when a persona exists.
		/// </summary>
		[Fact]
		public async Task UpdatePersonaAsync_WhenValid_UpdatesAllFields()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "Original",
				                        descriptionTranslations: new Dictionary<string, string>
					                        { { "en", "Original description" } },
				                        defaultModel: "model-v1",
				                        systemPrompt: "Original prompt",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			Guid publicId = created.Participant!.PublicId;
			DateTime updateTime = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

			// Act
			PersonaEntity? updated = await service.UpdatePersonaAsync(
				                         publicId: publicId,
				                         displayName: "Updated",
				                         descriptionTranslations: new Dictionary<string, string>
					                         { { "en", "Updated description" } },
				                         defaultModel: "model-v2",
				                         systemPrompt: "Updated prompt",
				                         visibility: PersonaVisibility.Shared,
				                         isActive: false,
				                         utcNow: updateTime);

			// Assert
			Assert.NotNull(updated);
			Assert.Equal("Updated", updated.Participant!.DisplayName);
			Assert.Single(updated.DescriptionTranslations);
			Assert.Equal("Updated description", updated.DescriptionTranslations.First().Value);
			Assert.Equal("model-v2", updated.DefaultModel);
			Assert.False(updated.IsActive);
			Assert.Equal(PersonaVisibility.Shared, updated.Visibility);
			Assert.NotNull(updated.ActiveSystemPrompt);
			Assert.Equal("Updated prompt", updated.ActiveSystemPrompt!.Content);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.UpdatePersonaAsync"/> does not create a new system prompt
		/// version when the prompt content has not changed.
		/// </summary>
		[Fact]
		public async Task UpdatePersonaAsync_WhenSystemPromptUnchanged_DoesNotCreateNewVersion()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "Stable",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: "Stable prompt",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			Guid publicId = created.Participant!.PublicId;
			SystemPromptId originalPromptId = created.ActiveSystemPromptId!.Value;

			// Act — same prompt text
			PersonaEntity? updated = await service.UpdatePersonaAsync(
				                         publicId: publicId,
				                         displayName: "Stable",
				                         descriptionTranslations: null,
				                         defaultModel: null,
				                         systemPrompt: "Stable prompt",
				                         visibility: PersonaVisibility.Private,
				                         isActive: true,
				                         utcNow: new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));

			// Assert — same prompt entity is still active
			Assert.NotNull(updated);
			Assert.Equal(originalPromptId, updated.ActiveSystemPromptId);

			// Verify no extra prompts were created
			int promptCount = await Fixture.DbContext.SystemPrompts
				                  .CountAsync(sp => sp.PersonaId == created.Id);
			Assert.Equal(1, promptCount);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.UpdatePersonaAsync"/> creates a new system prompt version
		/// when the prompt content changes, preserving the old version.
		/// </summary>
		[Fact]
		public async Task UpdatePersonaAsync_WhenSystemPromptChanges_CreatesNewVersion()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "Evolving",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: "Version 1",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			Guid publicId = created.Participant!.PublicId;
			SystemPromptId originalPromptId = created.ActiveSystemPromptId!.Value;
			DateTime updateTime = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

			// Act — different prompt text
			PersonaEntity? updated = await service.UpdatePersonaAsync(
				                         publicId: publicId,
				                         displayName: "Evolving",
				                         descriptionTranslations: null,
				                         defaultModel: null,
				                         systemPrompt: "Version 2",
				                         visibility: PersonaVisibility.Private,
				                         isActive: true,
				                         utcNow: updateTime);

			// Assert — new prompt version is active, old one still exists
			Assert.NotNull(updated);
			Assert.NotEqual(originalPromptId, updated.ActiveSystemPromptId);
			Assert.NotNull(updated.ActiveSystemPrompt);
			Assert.Equal("Version 2", updated.ActiveSystemPrompt!.Content);

			// Both versions exist
			int promptCount = await Fixture.DbContext.SystemPrompts
				                  .CountAsync(sp => sp.PersonaId == created.Id);
			Assert.Equal(2, promptCount);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.UpdatePersonaAsync"/> reuses an existing prompt when the
		/// hash matches (deduplication).
		/// </summary>
		[Fact]
		public async Task UpdatePersonaAsync_WhenRevertingToPreviousPrompt_ReusesExistingByHash()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "Revert",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: "Original prompt",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			Guid publicId = created.Participant!.PublicId;
			SystemPromptId originalPromptId = created.ActiveSystemPromptId!.Value;

			// Change to a different prompt
			await service.UpdatePersonaAsync(
				publicId: publicId,
				displayName: "Revert",
				descriptionTranslations: null,
				defaultModel: null,
				systemPrompt: "Intermediate prompt",
				visibility: PersonaVisibility.Private,
				isActive: true,
				utcNow: new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));

			// Act — revert back to the original prompt text
			PersonaEntity? reverted = await service.UpdatePersonaAsync(
				                          publicId: publicId,
				                          displayName: "Revert",
				                          descriptionTranslations: null,
				                          defaultModel: null,
				                          systemPrompt: "Original prompt",
				                          visibility: PersonaVisibility.Private,
				                          isActive: true,
				                          utcNow: new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));

			// Assert — original prompt entity is reused (same ID), no new row created
			Assert.NotNull(reverted);
			Assert.Equal(originalPromptId, reverted.ActiveSystemPromptId);

			int promptCount = await Fixture.DbContext.SystemPrompts
				                  .CountAsync(sp => sp.PersonaId == created.Id);
			Assert.Equal(2, promptCount); // Original + Intermediate, no third row
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.UpdatePersonaAsync"/> clears the active system prompt
		/// when <see langword="null"/> is passed.
		/// </summary>
		[Fact]
		public async Task UpdatePersonaAsync_WhenSystemPromptSetToNull_ClearsActivePrompt()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "Clear",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: "To be cleared",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			Guid publicId = created.Participant!.PublicId;

			// Act
			PersonaEntity? updated = await service.UpdatePersonaAsync(
				                         publicId: publicId,
				                         displayName: "Clear",
				                         descriptionTranslations: null,
				                         defaultModel: null,
				                         systemPrompt: null,
				                         visibility: PersonaVisibility.Private,
				                         isActive: true,
				                         utcNow: new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));

			// Assert
			Assert.NotNull(updated);
			Assert.Null(updated.ActiveSystemPromptId);
			Assert.Null(updated.ActiveSystemPrompt);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.UpdatePersonaAsync"/> returns <see langword="null"/>
		/// when the persona does not exist.
		/// </summary>
		[Fact]
		public async Task UpdatePersonaAsync_WhenNotFound_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			PersonaEntity? result = await service.UpdatePersonaAsync(
				                        publicId: Guid.NewGuid(),
				                        displayName: "Ghost",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        isActive: true,
				                        utcNow: DateTime.UtcNow);

			// Assert
			Assert.Null(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.UpdatePersonaAsync"/> throws
		/// <see cref="ArgumentException"/> when <see cref="Guid.Empty"/> is passed as the public ID.
		/// </summary>
		[Fact]
		public async Task UpdatePersonaAsync_WhenEmptyGuid_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePersonaAsync(
				         publicId: Guid.Empty,
				         displayName: "X",
				         descriptionTranslations: null,
				         defaultModel: null,
				         systemPrompt: null,
				         visibility: PersonaVisibility.Private,
				         isActive: true,
				         utcNow: DateTime.UtcNow));

			Assert.Equal("publicId", ex.ParamName);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.UpdatePersonaAsync"/> throws
		/// <see cref="ArgumentException"/> when the display name is empty.
		/// </summary>
		[Fact]
		public async Task UpdatePersonaAsync_WhenDisplayNameEmpty_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePersonaAsync(
				publicId: Guid.NewGuid(),
				displayName: "",
				descriptionTranslations: null,
				defaultModel: null,
				systemPrompt: null,
				visibility: PersonaVisibility.Private,
				isActive: true,
				utcNow: DateTime.UtcNow));
		}

		#endregion

		#region DeactivatePersonaAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.DeactivatePersonaAsync"/> sets
		/// <see cref="PersonaEntity.IsActive"/> to <see langword="false"/> and returns <see langword="true"/>.
		/// </summary>
		[Fact]
		public async Task DeactivatePersonaAsync_WhenActivePersonaExists_DeactivatesAndReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "ToDeactivate",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			Guid publicId = created.Participant!.PublicId;

			// Act
			bool result = await service.DeactivatePersonaAsync(publicId, utcNow);

			// Assert
			Assert.True(result);

			PersonaEntity? reloaded = await Fixture.DbContext.Personas
				                          .AsNoTracking()
				                          .FirstOrDefaultAsync(p => p.Id == created.Id);
			Assert.NotNull(reloaded);
			Assert.False(reloaded.IsActive);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.DeactivatePersonaAsync"/> returns
		/// <see langword="false"/> when the persona is already inactive.
		/// </summary>
		[Fact]
		public async Task DeactivatePersonaAsync_WhenAlreadyInactive_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "AlreadyOff",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			Guid publicId = created.Participant!.PublicId;
			await service.DeactivatePersonaAsync(publicId, utcNow); // First deactivation

			// Act — second deactivation
			bool result = await service.DeactivatePersonaAsync(publicId, utcNow);

			// Assert
			Assert.False(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.DeactivatePersonaAsync"/> returns
		/// <see langword="false"/> when the persona does not exist.
		/// </summary>
		[Fact]
		public async Task DeactivatePersonaAsync_WhenNotFound_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool result = await service.DeactivatePersonaAsync(Guid.NewGuid(), DateTime.UtcNow);

			// Assert
			Assert.False(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.DeactivatePersonaAsync"/> throws
		/// <see cref="ArgumentException"/> when <see cref="Guid.Empty"/> is passed.
		/// </summary>
		[Fact]
		public async Task DeactivatePersonaAsync_WhenEmptyGuid_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.DeactivatePersonaAsync(Guid.Empty, DateTime.UtcNow));

			Assert.Equal("publicId", ex.ParamName);
		}

		#endregion

		#region ClonePersonaAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.ClonePersonaAsync"/> creates an independent private copy of
		/// a shared source persona with a new participant identity and the correct creator.
		/// </summary>
		[Fact]
		public async Task ClonePersonaAsync_WhenSourceExists_CreatesPrivateCopyWithNewIdentity()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity ownerA = await CreateUserParticipantAsync("OwnerA", "a@test.test", utcNow);
			ParticipantEntity ownerB = await CreateUserParticipantAsync("OwnerB", "b@test.test", utcNow);

			PersonaEntity source = await service.CreatePersonaAsync(
				                       displayName: "SharedSource",
				                       descriptionTranslations: new Dictionary<string, string>
					                       { { "en", "Source description." } },
				                       defaultModel: "mistral:7b",
				                       systemPrompt: "You are SharedSource.",
				                       visibility: PersonaVisibility.Shared,
				                       creatorParticipantId: ownerA.Id,
				                       utcNow: utcNow);

			DateTime cloneTime = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

			// Act
			PersonaEntity? clone = await service.ClonePersonaAsync(source.Participant!.PublicId, ownerB.Id, cloneTime);

			// Assert
			Assert.NotNull(clone);

			// New identity
			Assert.NotEqual(source.Participant!.PublicId, clone.Participant!.PublicId);
			Assert.NotEqual(source.Id, clone.Id);

			// Same content
			Assert.Equal("SharedSource", clone.Participant.DisplayName);
			Assert.Single(clone.DescriptionTranslations);
			Assert.Equal("Source description.", clone.DescriptionTranslations.First().Value);
			Assert.Equal("mistral:7b", clone.DefaultModel);
			Assert.NotNull(clone.ActiveSystemPrompt);
			Assert.Equal("You are SharedSource.", clone.ActiveSystemPrompt!.Content);

			// Independent system prompt (different entity, same content)
			Assert.NotEqual(source.ActiveSystemPromptId, clone.ActiveSystemPromptId);

			// Ownership: private, owned by cloner
			Assert.Equal(PersonaVisibility.Private, clone.Visibility);
			Assert.Equal(ownerB.Id, clone.CreatedByParticipantId);

			// Creation timestamp
			Assert.Equal(cloneTime, clone.Participant.CreatedAtUtc);
			Assert.True(clone.IsActive);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.ClonePersonaAsync"/> creates a clone without an active
		/// system prompt when the source persona has no prompt.
		/// </summary>
		[Fact]
		public async Task ClonePersonaAsync_WhenSourceHasNoSystemPrompt_CreatesCloneWithoutPrompt()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity creator = await CreateUserParticipantAsync("Creator", "c@test.test", utcNow);

			PersonaEntity source = await service.CreatePersonaAsync(
				                       displayName: "NoPromptBot",
				                       descriptionTranslations: null,
				                       defaultModel: null,
				                       systemPrompt: null,
				                       visibility: PersonaVisibility.Shared,
				                       creatorParticipantId: null,
				                       utcNow: utcNow);

			// Act
			PersonaEntity? clone = await service.ClonePersonaAsync(source.Participant!.PublicId, creator.Id, utcNow);

			// Assert
			Assert.NotNull(clone);
			Assert.Null(clone.ActiveSystemPromptId);
			Assert.Equal("NoPromptBot", clone.Participant!.DisplayName);
			Assert.Equal(PersonaVisibility.Private, clone.Visibility);
			Assert.Equal(creator.Id, clone.CreatedByParticipantId);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.ClonePersonaAsync"/> returns <see langword="null"/>
		/// when the source persona does not exist.
		/// </summary>
		[Fact]
		public async Task ClonePersonaAsync_WhenSourceNotFound_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity creator = await CreateUserParticipantAsync("Creator", "c@test.test", utcNow);

			// Act
			PersonaEntity? result = await service.ClonePersonaAsync(Guid.NewGuid(), creator.Id, utcNow);

			// Assert
			Assert.Null(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.ClonePersonaAsync"/> throws
		/// <see cref="ArgumentException"/> when <see cref="Guid.Empty"/> is passed.
		/// </summary>
		[Fact]
		public async Task ClonePersonaAsync_WhenEmptyGuid_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity creator = await CreateUserParticipantAsync("Creator", "c@test.test", utcNow);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.ClonePersonaAsync(Guid.Empty, creator.Id, utcNow));

			Assert.Equal("sourcePublicId", ex.ParamName);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.ClonePersonaAsync"/> copies the source persona's avatar
		/// reference so the clone exposes the same image without writing a new file (deduplication via shared
		/// <see cref="ResourceReferenceEntity.ResourceId"/>).
		/// </summary>
		[Fact]
		public async Task ClonePersonaAsync_WhenSourceHasAvatar_ClonesAvatarReference()
		{
			// Arrange
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			ParticipantEntity ownerA = await CreateUserParticipantAsync("OwnerA", "a@test.test", utcNow);
			ParticipantEntity ownerB = await CreateUserParticipantAsync("OwnerB", "b@test.test", utcNow);

			PersonaEntity source = await service.CreatePersonaAsync(
				                       displayName: "WithAvatar",
				                       descriptionTranslations: null,
				                       defaultModel: null,
				                       systemPrompt: null,
				                       visibility: PersonaVisibility.Shared,
				                       creatorParticipantId: ownerA.Id,
				                       utcNow: utcNow);

			// Upload an avatar for the source persona so the clone has something to copy.
			using (MemoryStream avatar = MakeStream("source-avatar-bytes"))
			{
				bool saved = await service.SaveAvatarAsync(
					             source.Participant!.PublicId,
					             avatar,
					             contentType: "image/png",
					             createdByParticipantId: null,
					             utcNow: utcNow);
				Assert.True(saved);
			}

			int saveCountBeforeClone = store.SaveCount;

			// Act
			PersonaEntity? clone = await service.ClonePersonaAsync(
				                       source.Participant!.PublicId,
				                       ownerB.Id,
				                       utcNow);

			// Assert
			Assert.NotNull(clone);

			ResourceReferenceEntity? sourceRef = await Fixture.DbContext.ResourceReferences
				                                     .AsNoTracking()
				                                     .FirstOrDefaultAsync(r =>
					                                     r.OwnerKind == ResourceOwnerKind.Persona &&
					                                     r.OwnerId == new ResourceOwnerId(source.Id.Value));
			ResourceReferenceEntity? cloneRef = await Fixture.DbContext.ResourceReferences
				                                    .AsNoTracking()
				                                    .FirstOrDefaultAsync(r =>
					                                    r.OwnerKind == ResourceOwnerKind.Persona &&
					                                    r.OwnerId == new ResourceOwnerId(clone.Id.Value));

			Assert.NotNull(sourceRef);
			Assert.NotNull(cloneRef);

			// Same physical resource (no re-upload), but a fresh reference identity for the clone.
			Assert.Equal(sourceRef!.ResourceId, cloneRef!.ResourceId);
			Assert.NotEqual(sourceRef.PublicId, cloneRef.PublicId);
			Assert.Equal(sourceRef.ContentType, cloneRef.ContentType);
			Assert.Equal(sourceRef.OriginalFileName, cloneRef.OriginalFileName);

			// No additional file write — the clone reuses the source's storage path.
			Assert.Equal(saveCountBeforeClone, store.SaveCount);
		}

		#endregion

		#region SaveAvatarAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.SaveAvatarAsync"/> writes the file via the resource store,
		/// creates a single <see cref="ResourceReferenceEntity"/> for the persona and returns
		/// <see langword="true"/>.
		/// </summary>
		[Fact]
		public async Task SaveAvatarAsync_WhenPersonaExists_PersistsAvatarAndReturnsTrue()
		{
			// Arrange
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "AvatarBot",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			using MemoryStream content = MakeStream("avatar-bytes");

			// Act
			bool result = await service.SaveAvatarAsync(
				              persona.Participant!.PublicId,
				              content,
				              contentType: "image/png",
				              createdByParticipantId: null,
				              utcNow: utcNow);

			// Assert
			Assert.True(result);
			Assert.Equal(1, store.SaveCount);

			List<ResourceReferenceEntity> refs = await Fixture.DbContext.ResourceReferences
				                                     .AsNoTracking()
				                                     .Where(r => r.OwnerKind == ResourceOwnerKind.Persona &&
				                                                 r.OwnerId == new ResourceOwnerId(persona.Id.Value))
				                                     .ToListAsync();
			Assert.Single(refs);
			Assert.Equal("image/png", refs[0].ContentType);
			Assert.Equal("avatar", refs[0].OriginalFileName);
		}

		/// <summary>
		/// Verifies that calling <see cref="IPersonaDataService.SaveAvatarAsync"/> a second time replaces the
		/// existing avatar reference: the new reference points at the new resource and only one reference exists
		/// for the persona afterwards.
		/// </summary>
		[Fact]
		public async Task SaveAvatarAsync_WhenAvatarAlreadyExists_ReplacesExistingReference()
		{
			// Arrange
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "Replaceable",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			using (MemoryStream first = MakeStream("first-avatar"))
			{
				await service.SaveAvatarAsync(persona.Participant!.PublicId, first, "image/png", null, utcNow);
			}

			ResourceReferenceEntity firstRef = await Fixture.DbContext.ResourceReferences
				                                   .AsNoTracking()
				                                   .FirstAsync(r => r.OwnerKind == ResourceOwnerKind.Persona &&
				                                                    r.OwnerId == new ResourceOwnerId(persona.Id.Value));

			using MemoryStream second = MakeStream("second-avatar-different-content");

			// Act
			bool result = await service.SaveAvatarAsync(
				              persona.Participant!.PublicId,
				              second,
				              contentType: "image/jpeg",
				              createdByParticipantId: null,
				              utcNow: utcNow);

			// Assert
			Assert.True(result);

			List<ResourceReferenceEntity> refs = await Fixture.DbContext.ResourceReferences
				                                     .AsNoTracking()
				                                     .Where(r => r.OwnerKind == ResourceOwnerKind.Persona &&
				                                                 r.OwnerId == new ResourceOwnerId(persona.Id.Value))
				                                     .ToListAsync();
			Assert.Single(refs);
			Assert.NotEqual(firstRef.PublicId, refs[0].PublicId);
			Assert.NotEqual(firstRef.ResourceId, refs[0].ResourceId);
			Assert.Equal("image/jpeg", refs[0].ContentType);
		}

		/// <summary>
		/// Verifies that a failure inside <see cref="IResourceStore.SaveAsync"/> rolls the compensating transaction
		/// back: the previously existing avatar reference must remain intact and no new reference must leak into
		/// the database.
		/// </summary>
		/// <remarks>
		/// This exercises the <c>BeginCompensatingTransactionAsync</c> wrapping in
		/// <see cref="IPersonaDataService.SaveAvatarAsync"/>: <c>DeleteReferencesByOwnerAsync</c> already wiped the
		/// old reference inside the transaction by the time the upload throws — the rollback must restore it.
		/// </remarks>
		[Fact]
		public async Task SaveAvatarAsync_WhenUploadFails_RollsBackAndKeepsExistingAvatar()
		{
			// Arrange
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "ResilientBot",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			// Establish the existing avatar that must survive the failed replacement attempt.
			using (MemoryStream original = MakeStream("original-avatar"))
			{
				await service.SaveAvatarAsync(persona.Participant!.PublicId, original, "image/png", null, utcNow);
			}

			ResourceReferenceEntity originalRef = await Fixture.DbContext.ResourceReferences
				                                      .AsNoTracking()
				                                      .FirstAsync(r => r.OwnerKind == ResourceOwnerKind.Persona &&
				                                                       r.OwnerId ==
				                                                       new ResourceOwnerId(persona.Id.Value));

			// Inject the storage failure for the next SaveAsync call (the replacement upload).
			store.OnSave = _ => throw new IOException("Simulated storage failure");

			using MemoryStream replacement = MakeStream("replacement-avatar");

			// Act
			var ex = await Assert.ThrowsAsync<IOException>(() => service.SaveAvatarAsync(
				         persona.Participant!.PublicId,
				         replacement,
				         contentType: "image/jpeg",
				         createdByParticipantId: null,
				         utcNow: utcNow));

			// Assert
			Assert.Equal("Simulated storage failure", ex.Message);

			// The original reference must still be there — DeleteReferencesByOwnerAsync ran inside the transaction
			// and was rolled back when the upload threw.
			List<ResourceReferenceEntity> refs = await Fixture.DbContext.ResourceReferences
				                                     .AsNoTracking()
				                                     .Where(r => r.OwnerKind == ResourceOwnerKind.Persona &&
				                                                 r.OwnerId == new ResourceOwnerId(persona.Id.Value))
				                                     .ToListAsync();
			Assert.Single(refs);
			Assert.Equal(originalRef.PublicId, refs[0].PublicId);
			Assert.Equal(originalRef.ResourceId, refs[0].ResourceId);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.SaveAvatarAsync"/> returns <see langword="false"/> without
		/// touching the resource store when the persona does not exist.
		/// </summary>
		[Fact]
		public async Task SaveAvatarAsync_WhenPersonaNotFound_ReturnsFalseAndDoesNotTouchStore()
		{
			// Arrange
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store);
			using MemoryStream content = MakeStream("ignored");

			// Act
			bool result = await service.SaveAvatarAsync(
				              Guid.NewGuid(),
				              content,
				              contentType: "image/png",
				              createdByParticipantId: null,
				              utcNow: DateTime.UtcNow);

			// Assert
			Assert.False(result);
			Assert.Equal(0, store.SaveCount);
			Assert.Equal(0, store.DeleteCount);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.SaveAvatarAsync"/> rejects <see cref="Guid.Empty"/>.
		/// </summary>
		[Fact]
		public async Task SaveAvatarAsync_WhenEmptyGuid_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			using MemoryStream content = MakeStream("ignored");

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAvatarAsync(
				         Guid.Empty,
				         content,
				         contentType: "image/png",
				         createdByParticipantId: null,
				         utcNow: DateTime.UtcNow));

			Assert.Equal("publicId", ex.ParamName);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.SaveAvatarAsync"/> rejects a <see langword="null"/>
		/// content stream.
		/// </summary>
		[Fact]
		public async Task SaveAvatarAsync_WhenContentIsNull_ThrowsArgumentNullException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveAvatarAsync(
				         Guid.NewGuid(),
				         content: null!,
				         contentType: "image/png",
				         createdByParticipantId: null,
				         utcNow: DateTime.UtcNow));

			Assert.Equal("content", ex.ParamName);
		}

		#endregion

		#region GetAvatarAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetAvatarAsync"/> returns the storage path, content type,
		/// original file name and size for a persona that has an avatar.
		/// </summary>
		[Fact]
		public async Task GetAvatarAsync_WhenAvatarExists_ReturnsDownloadInfo()
		{
			// Arrange
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "ShownBot",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			byte[] payload = Encoding.UTF8.GetBytes("avatar-payload");
			using (var content = new MemoryStream(payload, writable: false))
			{
				await service.SaveAvatarAsync(persona.Participant!.PublicId, content, "image/png", null, utcNow);
			}

			// Act
			ResourceDownloadInfo? info = await service.GetAvatarAsync(persona.Participant!.PublicId);

			// Assert
			Assert.NotNull(info);
			Assert.Equal("image/png", info!.ContentType);
			Assert.Equal("avatar", info.OriginalFileName);
			Assert.Equal(payload.Length, info.SizeBytes);
			Assert.False(string.IsNullOrWhiteSpace(info.StoragePath));
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetAvatarAsync"/> returns <see langword="null"/> when the
		/// persona exists but has no avatar.
		/// </summary>
		[Fact]
		public async Task GetAvatarAsync_WhenPersonaHasNoAvatar_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "BareBot",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			// Act
			ResourceDownloadInfo? info = await service.GetAvatarAsync(persona.Participant!.PublicId);

			// Assert
			Assert.Null(info);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetAvatarAsync"/> returns <see langword="null"/> when the
		/// persona itself does not exist.
		/// </summary>
		[Fact]
		public async Task GetAvatarAsync_WhenPersonaNotFound_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			ResourceDownloadInfo? info = await service.GetAvatarAsync(Guid.NewGuid());

			// Assert
			Assert.Null(info);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetAvatarAsync"/> rejects <see cref="Guid.Empty"/>.
		/// </summary>
		[Fact]
		public async Task GetAvatarAsync_WhenEmptyGuid_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GetAvatarAsync(Guid.Empty));

			Assert.Equal("publicId", ex.ParamName);
		}

		#endregion

		#region DeleteAvatarAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.DeleteAvatarAsync"/> removes the avatar reference and
		/// returns <see langword="true"/> when one existed.
		/// </summary>
		[Fact]
		public async Task DeleteAvatarAsync_WhenAvatarExists_RemovesReferenceAndReturnsTrue()
		{
			// Arrange
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "ToBeStripped",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			using (MemoryStream content = MakeStream("avatar"))
			{
				await service.SaveAvatarAsync(persona.Participant!.PublicId, content, "image/png", null, utcNow);
			}

			// Act
			bool result = await service.DeleteAvatarAsync(persona.Participant!.PublicId);

			// Assert
			Assert.True(result);

			List<ResourceReferenceEntity> refs = await Fixture.DbContext.ResourceReferences
				                                     .AsNoTracking()
				                                     .Where(r => r.OwnerKind == ResourceOwnerKind.Persona &&
				                                                 r.OwnerId == new ResourceOwnerId(persona.Id.Value))
				                                     .ToListAsync();
			Assert.Empty(refs);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.DeleteAvatarAsync"/> returns <see langword="false"/> when
		/// the persona has no avatar to delete.
		/// </summary>
		[Fact]
		public async Task DeleteAvatarAsync_WhenPersonaHasNoAvatar_ReturnsFalse()
		{
			// Arrange
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "Bare",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: utcNow);

			// Act
			bool result = await service.DeleteAvatarAsync(persona.Participant!.PublicId);

			// Assert
			Assert.False(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.DeleteAvatarAsync"/> returns <see langword="false"/> when
		/// the persona itself does not exist.
		/// </summary>
		[Fact]
		public async Task DeleteAvatarAsync_WhenPersonaNotFound_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool result = await service.DeleteAvatarAsync(Guid.NewGuid());

			// Assert
			Assert.False(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.DeleteAvatarAsync"/> rejects <see cref="Guid.Empty"/>.
		/// </summary>
		[Fact]
		public async Task DeleteAvatarAsync_WhenEmptyGuid_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAvatarAsync(Guid.Empty));

			Assert.Equal("publicId", ex.ParamName);
		}

		#endregion

		#region GetPersonaIdsWithAvatarAsync

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaIdsWithAvatarAsync"/> returns exactly the subset
		/// of supplied persona ids that have an avatar reference.
		/// </summary>
		[Fact]
		public async Task GetPersonaIdsWithAvatarAsync_WhenMixed_ReturnsOnlyIdsWithAvatar()
		{
			// Arrange
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity withAvatar = await service.CreatePersonaAsync(
				                           displayName: "WithAvatar",
				                           descriptionTranslations: null,
				                           defaultModel: null,
				                           systemPrompt: null,
				                           visibility: PersonaVisibility.Private,
				                           creatorParticipantId: null,
				                           utcNow: utcNow);

			PersonaEntity withoutAvatar = await service.CreatePersonaAsync(
				                              displayName: "WithoutAvatar",
				                              descriptionTranslations: null,
				                              defaultModel: null,
				                              systemPrompt: null,
				                              visibility: PersonaVisibility.Private,
				                              creatorParticipantId: null,
				                              utcNow: utcNow);

			using (MemoryStream content = MakeStream("avatar"))
			{
				await service.SaveAvatarAsync(withAvatar.Participant!.PublicId, content, "image/png", null, utcNow);
			}

			long unknownId = withAvatar.Id.Value + withoutAvatar.Id.Value + 1000;

			// Act
			IReadOnlySet<PersonaId> result = await service.GetPersonaIdsWithAvatarAsync(
				                                 new[] { withAvatar.Id, withoutAvatar.Id, new PersonaId(unknownId) });

			// Assert
			Assert.Single(result);
			Assert.Contains(withAvatar.Id, result);
			Assert.DoesNotContain(withoutAvatar.Id, result);
			Assert.DoesNotContain(new PersonaId(unknownId), result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaIdsWithAvatarAsync"/> returns an empty set when
		/// no supplied id has an avatar.
		/// </summary>
		[Fact]
		public async Task GetPersonaIdsWithAvatarAsync_WhenNoneHaveAvatar_ReturnsEmptySet()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);
			DateTime utcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

			PersonaEntity p1 = await service.CreatePersonaAsync(
				                   displayName: "A",
				                   descriptionTranslations: null,
				                   defaultModel: null,
				                   systemPrompt: null,
				                   visibility: PersonaVisibility.Private,
				                   creatorParticipantId: null,
				                   utcNow: utcNow);
			PersonaEntity p2 = await service.CreatePersonaAsync(
				                   displayName: "B",
				                   descriptionTranslations: null,
				                   defaultModel: null,
				                   systemPrompt: null,
				                   visibility: PersonaVisibility.Private,
				                   creatorParticipantId: null,
				                   utcNow: utcNow);

			// Act
			IReadOnlySet<PersonaId> result = await service.GetPersonaIdsWithAvatarAsync(new[] { p1.Id, p2.Id });

			// Assert
			Assert.Empty(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaIdsWithAvatarAsync"/> returns an empty set when
		/// the supplied id collection is empty.
		/// </summary>
		[Fact]
		public async Task GetPersonaIdsWithAvatarAsync_WhenInputEmpty_ReturnsEmptySet()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			IReadOnlySet<PersonaId> result = await service.GetPersonaIdsWithAvatarAsync(Array.Empty<PersonaId>());

			// Assert
			Assert.Empty(result);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.GetPersonaIdsWithAvatarAsync"/> rejects a
		/// <see langword="null"/> id collection with an <see cref="ArgumentNullException"/> identifying the
		/// offending parameter.
		/// </summary>
		[Fact]
		public async Task GetPersonaIdsWithAvatarAsync_WhenPersonaIdsIsNull_ThrowsArgumentNullException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
				         service.GetPersonaIdsWithAvatarAsync(personaIds: null!));
			Assert.Equal("personaIds", ex.ParamName);
		}

		#endregion

		#region UTC clock fallback

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.CreatePersonaAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for the persona's <c>CreatedAtUtc</c> when the optional <c>utcNow</c>
		/// argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c> and silently
		/// captures wall-clock time instead.
		/// </remarks>
		[Fact]
		public async Task CreatePersonaAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			// Act
			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "Nova",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: "hi",
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null);

			PersonaEntity? reloaded = await Fixture.DbContext.Personas
				                          .AsNoTracking()
				                          .Include(p => p.Participant)
				                          .Include(p => p.ActiveSystemPrompt)
				                          .FirstOrDefaultAsync(p => p.Id == persona.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal(fixedNow, reloaded.CreatedAtUtc);
			Assert.Equal(fixedNow, reloaded.Participant!.CreatedAtUtc);
			Assert.Equal(fixedNow, reloaded.ActiveSystemPrompt!.CreatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.UpdatePersonaAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for <c>UpdatedAtUtc</c> when the optional <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c>.
		/// </remarks>
		[Fact]
		public async Task UpdatePersonaAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime seedNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "Nova",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: seedNow);

			// Act
			PersonaEntity? updated = await service.UpdatePersonaAsync(
				                         publicId: created.Participant!.PublicId,
				                         displayName: "Nova2",
				                         descriptionTranslations: null,
				                         defaultModel: null,
				                         systemPrompt: null,
				                         visibility: PersonaVisibility.Private,
				                         isActive: true);

			// Assert
			Assert.NotNull(updated);
			Assert.Equal(fixedNow, updated.UpdatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.DeactivatePersonaAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for <c>UpdatedAtUtc</c> when the optional <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c>.
		/// </remarks>
		[Fact]
		public async Task DeactivatePersonaAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime seedNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			PersonaEntity created = await service.CreatePersonaAsync(
				                        displayName: "Nova",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: seedNow);

			// Act
			bool deactivated = await service.DeactivatePersonaAsync(publicId: created.Participant!.PublicId);

			// Assert
			Assert.True(deactivated);
			PersonaEntity? reloaded = await Fixture.DbContext.Personas
				                          .AsNoTracking()
				                          .FirstOrDefaultAsync(p => p.Id == created.Id);
			Assert.NotNull(reloaded);
			Assert.False(reloaded.IsActive);
			Assert.Equal(fixedNow, reloaded.UpdatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.ClonePersonaAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for the cloned persona's <c>CreatedAtUtc</c> when the optional <c>utcNow</c>
		/// argument is omitted.
		/// </summary>
		/// <remarks>
		/// Guards against a regression where the production code path bypasses <c>ResolveUtcNow()</c>.
		/// </remarks>
		[Fact]
		public async Task ClonePersonaAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime seedNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext, timeProvider: clock);

			ParticipantEntity creator = await CreateUserParticipantAsync("alice", "alice@example.test", seedNow);

			PersonaEntity source = await service.CreatePersonaAsync(
				                       displayName: "Nova",
				                       descriptionTranslations: null,
				                       defaultModel: null,
				                       systemPrompt: null,
				                       visibility: PersonaVisibility.Private,
				                       creatorParticipantId: creator.Id,
				                       utcNow: seedNow);

			// Act
			PersonaEntity? clone = await service.ClonePersonaAsync(
				                       sourcePublicId: source.Participant!.PublicId,
				                       creatorParticipantId: creator.Id);

			// Assert
			Assert.NotNull(clone);
			PersonaEntity? reloaded = await Fixture.DbContext.Personas
				                          .AsNoTracking()
				                          .Include(p => p.Participant)
				                          .FirstOrDefaultAsync(p => p.Id == clone.Id);
			Assert.NotNull(reloaded);
			Assert.Equal(fixedNow, reloaded.CreatedAtUtc);
			Assert.Equal(fixedNow, reloaded.Participant!.CreatedAtUtc);
		}

		/// <summary>
		/// Verifies that <see cref="IPersonaDataService.SaveAvatarAsync"/> falls back to the injected
		/// <see cref="TimeProvider"/> for the resource reference's <c>CreatedAtUtc</c> when the optional
		/// <c>utcNow</c> argument is omitted.
		/// </summary>
		/// <remarks>
		/// Uses a real <see cref="ResourceService"/> wired through the same <see cref="FakeTimeProvider"/> so the
		/// upload pipeline observes the deterministic clock end-to-end.
		/// </remarks>
		[Fact]
		public async Task SaveAvatarAsync_WhenUtcNowIsNull_UsesInjectedTimeProvider()
		{
			// Arrange
			DateTime seedNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime fixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			FakeTimeProvider clock = CreateClock(fixedNow);
			var store = new ResourceServiceTests.FakeResourceStore();
			LumaCoreDataService service = CreateServiceWithRealResources(store, timeProvider: clock);

			PersonaEntity persona = await service.CreatePersonaAsync(
				                        displayName: "AvatarBot",
				                        descriptionTranslations: null,
				                        defaultModel: null,
				                        systemPrompt: null,
				                        visibility: PersonaVisibility.Private,
				                        creatorParticipantId: null,
				                        utcNow: seedNow);

			using MemoryStream content = MakeStream("avatar-bytes");

			// Act
			bool result = await service.SaveAvatarAsync(
				              persona.Participant!.PublicId,
				              content,
				              contentType: "image/png",
				              createdByParticipantId: null);

			// Assert
			Assert.True(result);
			ResourceReferenceEntity? reference = await Fixture.DbContext.ResourceReferences
				                                     .AsNoTracking()
				                                     .FirstOrDefaultAsync(r =>
					                                     r.OwnerKind == ResourceOwnerKind.Persona &&
					                                     r.OwnerId == new ResourceOwnerId(persona.Id.Value));
			Assert.NotNull(reference);
			Assert.Equal(fixedNow, reference.CreatedAtUtc);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Creates a <see cref="LumaCoreDataService"/> wired with a real <see cref="ResourceService"/> so the
		/// avatar API exercises the actual upload/dedup/compensation pipeline against the supplied
		/// <paramref name="store"/>.
		/// </summary>
		/// <param name="store">The fake resource store used as filesystem backend.</param>
		/// <param name="timeProvider">
		/// Optional <see cref="TimeProvider"/> override forwarded to the data service. Defaults to
		/// <see cref="TimeProvider.System"/>.
		/// </param>
		/// <returns>A configured <see cref="LumaCoreDataService"/> for avatar tests.</returns>
		private LumaCoreDataService CreateServiceWithRealResources(
			ResourceServiceTests.FakeResourceStore store,
			TimeProvider?                          timeProvider = null)
		{
			var resourceService = new ResourceService(
				Fixture.DbContext,
				store,
				new StreamBufferPool(new StreamBufferPoolOptions()),
				Options.Create(new DatabaseOptions()),
				timeProvider ?? TimeProvider.System,
				NullLogger<ResourceService>.Instance);
			return LumaCoreDataServiceFactory.Create(
				Fixture.DbContext,
				resourceService: resourceService,
				timeProvider: timeProvider);
		}

		#endregion
	}
}
