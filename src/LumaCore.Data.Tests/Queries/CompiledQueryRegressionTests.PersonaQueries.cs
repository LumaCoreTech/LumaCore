// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Queries;

using Xunit;

namespace LumaCore.Data.Tests.Queries;

public sealed partial class CompiledQueryRegressionTests
{
	/// <summary>
	/// Verifies that <see cref="PersonaQueries.GetAllActive"/> returns active personas with the
	/// <see cref="PersonaEntity.Participant"/> navigation loaded.
	/// </summary>
	[Fact]
	public async Task PersonaQueries_GetAllActive_ReturnsActivePersonas()
	{
		// Act
		List<PersonaEntity> personas = await ToListAsync(PersonaQueries.GetAllActive(mFixture.DbContext));

		// Assert
		Assert.Single(personas);
		Assert.Equal(mPersonaId, personas[0].Id);
		Assert.Equal(mBotParticipantId, personas[0].ParticipantId);
		Assert.Single(personas[0].DescriptionTranslations);
		Assert.Equal("Test bot persona", personas[0].DescriptionTranslations.First().Value);
		Assert.NotNull(personas[0].Participant);
		Assert.Equal(mBotPublicId, personas[0].Participant!.PublicId);
		Assert.Equal("Bot", personas[0].Participant!.DisplayName);
	}

	/// <summary>
	/// Verifies that <see cref="PersonaQueries.GetByParticipantId"/> returns the persona linked to the
	/// given participant.
	/// </summary>
	[Fact]
	public async Task PersonaQueries_GetByParticipantId_ReturnsPersona()
	{
		// Act
		PersonaEntity? result = await PersonaQueries.GetByParticipantId(mFixture.DbContext, mBotParticipantId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(mPersonaId, result.Id);
		Assert.Equal(mBotParticipantId, result.ParticipantId);
		Assert.Single(result.DescriptionTranslations);
		Assert.Equal("Test bot persona", result.DescriptionTranslations.First().Value);
		Assert.NotNull(result.Participant);
	}

	/// <summary>
	/// Verifies that <see cref="PersonaQueries.GetByPublicId"/> returns the persona whose linked
	/// participant matches the given public ID.
	/// </summary>
	[Fact]
	public async Task PersonaQueries_GetByPublicId_ReturnsPersona()
	{
		// Act
		PersonaEntity? result = await PersonaQueries.GetByPublicId(mFixture.DbContext, mBotPublicId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(mPersonaId, result.Id);
		Assert.Equal(mBotParticipantId, result.ParticipantId);
		Assert.Single(result.DescriptionTranslations);
		Assert.Equal("Test bot persona", result.DescriptionTranslations.First().Value);
		Assert.NotNull(result.Participant);
		Assert.Equal(mBotPublicId, result.Participant.PublicId);
	}

	/// <summary>
	/// Verifies that <see cref="PersonaQueries.GetCurrentSystemPrompt"/> returns the most recent system
	/// prompt for the seeded persona.
	/// </summary>
	[Fact]
	public async Task PersonaQueries_GetCurrentSystemPrompt_ReturnsPrompt()
	{
		// Act
		SystemPromptEntity? result =
			await PersonaQueries.GetCurrentSystemPrompt(mFixture.DbContext, mPersonaId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(mPersonaId, result.PersonaId);
		Assert.Equal("You are a test bot.", result.Content);
	}
}
