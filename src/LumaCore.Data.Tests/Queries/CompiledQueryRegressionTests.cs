// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Tests.Infrastructure;
using LumaCore.Definitions;

using Xunit;

namespace LumaCore.Data.Tests.Queries;

/// <summary>
/// Regression tests for all EF Core compiled query helpers.
/// </summary>
/// <remarks>
///     <para>
///     EF compiled queries are compiled on first execution, not at build time. These tests exercise every
///     compiled query against a real database to catch regressions early.
///     </para>
///     <para>
///     <b>What these tests protect against:</b> The queries use strongly-typed LINQ expressions, so the
///     compiler already catches most incompatible model changes (renamed/removed properties, type changes).
///     The remaining runtime risk — and the reason these tests exist — is <see cref="LumaCoreDbContext"/>
///     configuration drift: a property marked <c>.Ignore()</c>, a relationship rewired in
///     <c>OnModelCreating</c>, or a value converter change can leave the C# entity intact while breaking
///     the EF model underneath. In those cases the LINQ compiles but <c>EF.CompileAsyncQuery</c> throws
///     at first execution.
///     </para>
///     <para>
///     These tests use <see cref="DbFixture.Create()"/> and respect the configured provider from
///     <see cref="DbTestSettingsLoader"/>. Although the LINQ expressions themselves are provider-independent,
///     EF Core compiled queries cache their plan against a specific model instance. Using a different provider
///     than the rest of the test suite would "poison" the static query cache and cause
///     <em>"compiled query was executed with a different model"</em> errors in subsequent tests.
///     </para>
///     <para>
///     Each test instance gets its own isolated in-memory database seeded with a minimal but complete dataset
///     covering all entity types referenced by the queries.
///     </para>
///     <para>
///     <b>Scope:</b> These are smoke tests for model-compatibility. Functional behaviour of the queries
///     (filtering predicates, business rules, edge cases) is verified by the corresponding service-level
///     tests in <c>LumaCoreDataServiceTests</c>. The seed is intentionally minimal — just enough for each
///     compiled query to execute against the EF model without throwing.
///     </para>
///     <para>
///     Partial files: <c>UserQueries</c>, <c>RoleQueries</c>, <c>ConversationQueries</c>,
///     <c>MessageQueries</c>, <c>PersonaQueries</c>, <c>ResourceQueries</c>.
///     </para>
/// </remarks>
[Trait("Category", "Queries")]
public sealed partial class CompiledQueryRegressionTests : IAsyncLifetime
{
	private static readonly DateTime sSeedUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	private readonly DbFixture mFixture = DbFixture.Create();

	// --- Seed entity IDs, stored after SaveChangesAsync so tests can reference them. ---

	private ParticipantId mAliceParticipantId;
	private Guid          mAlicePublicId;
	private UserId        mAliceUserId;

	private ParticipantId mBotParticipantId;
	private Guid          mBotPublicId;
	private PersonaId     mPersonaId;

	private ConversationId mConversationId;
	private Guid           mConversationPublicId;

	private RoleId mRoleId;

	private Guid mMessagePublicId;

	private ResourceId mResourceId;
	private string     mResourceContentHash = string.Empty;
	private Guid       mResourceReferencePublicId;

	/// <summary>
	/// Disposes the database fixture.
	/// </summary>
	public ValueTask DisposeAsync() => mFixture.DisposeAsync();

	/// <summary>
	/// Creates the database schema and seeds a minimal but complete dataset that satisfies all compiled queries.
	/// </summary>
	public async ValueTask InitializeAsync()
	{
		await mFixture.InitializeAsync();

		LumaCoreDbContext db = mFixture.DbContext;

		// --- 1. Participants (must be saved first — other entities reference their IDs) ---

		mAlicePublicId = Guid.NewGuid();
		var aliceParticipant = new ParticipantEntity
		{
			PublicId = mAlicePublicId,
			DisplayName = "Alice",
			CreatedAtUtc = sSeedUtc
		};

		mBotPublicId = Guid.NewGuid();
		var botParticipant = new ParticipantEntity
		{
			PublicId = mBotPublicId,
			DisplayName = "Bot",
			CreatedAtUtc = sSeedUtc
		};

		db.Participants.AddRange(aliceParticipant, botParticipant);
		await db.SaveChangesAsync();
		mAliceParticipantId = aliceParticipant.Id;
		mBotParticipantId = botParticipant.Id;

		// --- 2. User, Role, Conversation, Persona (depend on participant IDs) ---

		var user = new UserEntity
		{
			ParticipantId = mAliceParticipantId,
			CreatedAtUtc = sSeedUtc,
			Username = "alice",
			UsernameNormalized = "ALICE",
			Email = "alice@example.test",
			PasswordHash = "hash"
		};
		db.Users.Add(user);

		var role = new RoleEntity { Name = "admin", CreatedAtUtc = sSeedUtc };
		db.Roles.Add(role);

		mConversationPublicId = Guid.NewGuid();
		var conversation = new ConversationEntity
		{
			PublicId = mConversationPublicId,
			Title = "Test conversation",
			CreatedAtUtc = sSeedUtc,
			UpdatedAtUtc = sSeedUtc
		};
		db.Conversations.Add(conversation);

		var persona = new PersonaEntity
		{
			ParticipantId = mBotParticipantId,
			DefaultModel = "gpt-test",
			IsActive = true,
			CreatedAtUtc = sSeedUtc,
			UpdatedAtUtc = sSeedUtc
		};
		db.Personas.Add(persona);

		await db.SaveChangesAsync();

		// Seed description translations for the persona
		db.PersonaDescriptionTranslations.Add(
			new PersonaDescriptionTranslationEntity
			{
				PersonaId = persona.Id,
				CultureCode = "en",
				Value = "Test bot persona",
				Source = TranslationSource.Manual
			});

		await db.SaveChangesAsync();
		mAliceUserId = user.Id;
		mConversationId = conversation.Id;
		mPersonaId = persona.Id;
		mRoleId = role.Id;

		// --- 3. Join entities, message, system prompt (depend on IDs from step 2) ---

		db.UserRoles.Add(
			new UserRoleEntity
			{
				UserId = mAliceUserId,
				RoleId = role.Id,
				AssignedAtUtc = sSeedUtc
			});

		db.ConversationParticipants.Add(
			new ConversationParticipantEntity
			{
				ConversationId = mConversationId,
				ParticipantId = mAliceParticipantId,
				JoinedAtUtc = sSeedUtc,
				Role = ConversationParticipantRole.Owner
			});

		var systemPrompt = new SystemPromptEntity
		{
			PublicId = Guid.NewGuid(),
			PersonaId = mPersonaId,
			Content = "You are a test bot.",
			Hash = "0000000000000000000000000000000000000000000000000000000000000000",
			CreatedAtUtc = sSeedUtc
		};
		db.SystemPrompts.Add(systemPrompt);

		// Save so the SystemPrompt gets its DB-generated PK, then link it back as the persona's
		// active prompt. PersonaQueries.GetCurrentSystemPrompt resolves via Persona.ActiveSystemPromptId
		// (not "latest by CreatedAtUtc"), so the link must be explicit for the regression test to pass.
		await db.SaveChangesAsync();
		persona.ActiveSystemPromptId = systemPrompt.Id;

		// Three messages with distinct timestamps so MessageQueries.GetRecentByConversationId can verify
		// both the ordering (newest first) and the limit semantics. mMessagePublicId points to the
		// middle message so GetByPublicId asserts can distinguish it from first/last.
		mMessagePublicId = Guid.NewGuid();
		var middleMessage = new MessageEntity
		{
			PublicId = mMessagePublicId,
			ConversationId = mConversationId,
			SenderId = mAliceParticipantId,
			Content = "Middle message",
			CreatedAtUtc = sSeedUtc.AddMinutes(1)
		};
		db.Messages.AddRange(
			new MessageEntity
			{
				PublicId = Guid.NewGuid(),
				ConversationId = mConversationId,
				SenderId = mAliceParticipantId,
				Content = "First message",
				CreatedAtUtc = sSeedUtc
			},
			middleMessage,
			new MessageEntity
			{
				PublicId = Guid.NewGuid(),
				ConversationId = mConversationId,
				SenderId = mAliceParticipantId,
				Content = "Last message",
				CreatedAtUtc = sSeedUtc.AddMinutes(2)
			});

		await db.SaveChangesAsync();

		// --- 4. Resource + reference (depend on the middle message's database-generated PK) ---

		// Deterministic 64-character lowercase hex hash so ResourceQueries.GetActiveByContentHash
		// can probe both a hit and a miss without coupling to a real SHA-256 computation.
		mResourceContentHash = new string('a', 64);
		var resource = new ResourceEntity
		{
			ContentHash = mResourceContentHash,
			StoragePath = "ab/abcdef01-2345-6789-abcd-ef0123456789",
			SizeBytes = 1024,
			DeletionState = ResourceDeletionState.Active,
			CreatedAtUtc = sSeedUtc
		};
		db.Resources.Add(resource);
		await db.SaveChangesAsync();
		mResourceId = resource.Id;

		mResourceReferencePublicId = Guid.NewGuid();
		db.ResourceReferences.Add(
			new ResourceReferenceEntity
			{
				PublicId = mResourceReferencePublicId,
				ResourceId = mResourceId,
				OwnerKind = ResourceOwnerKind.Message,
				OwnerId = new ResourceOwnerId(middleMessage.Id.Value),
				ContentType = "image/png",
				OriginalFileName = "test.png",
				CreatedAtUtc = sSeedUtc
			});

		await db.SaveChangesAsync();
	}

	/// <summary>
	/// Materializes an <see cref="IAsyncEnumerable{T}"/> into a list for assertion.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="source">The async enumerable to materialize.</param>
	/// <returns>A list containing all elements from <paramref name="source"/>.</returns>
	private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
	{
		List<T> result = [];
		await foreach (T item in source.ConfigureAwait(false))
		{
			result.Add(item);
		}

		return result;
	}
}
