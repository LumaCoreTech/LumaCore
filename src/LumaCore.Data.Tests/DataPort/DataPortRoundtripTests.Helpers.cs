// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

// Shared helpers: harness factories, seed data insertion, and verification logic.
//
// The source harness uses MigrateAsync() (not EnsureCreated) so that the __EFMigrationsHistory table
// is populated — DataPortService.RunImportAsync() requires matching migration histories between shuttle
// and target database.
public sealed partial class DataPortRoundtripTests
{
	/// <summary>
	/// Holds the entities seeded into the source database, serving as expected-value reference
	/// for roundtrip verification after export and re-import.
	/// </summary>
	/// <param name="UserParticipants">The participants representing human users (3).</param>
	/// <param name="PersonaParticipants">The participants representing AI personas (3).</param>
	/// <param name="Users">The user entities, each linked to a user participant (3).</param>
	/// <param name="Roles">The system-wide role definitions (3).</param>
	/// <param name="UserRoles">The user-to-role assignments (3).</param>
	/// <param name="ModelEndpoints">The model endpoint configurations (3).</param>
	/// <param name="Personas">The persona entities, each linked to a persona participant (3).</param>
	/// <param name="SystemPrompts">The system prompts, one per persona (3).</param>
	/// <param name="Conversations">The conversation threads (3).</param>
	/// <param name="ConversationParticipants">The conversation membership entries across all conversations (7).</param>
	/// <param name="Messages">The messages across all conversations, including one orphaned and one redacted message (8).</param>
	/// <param name="MessageGenerationMetadata">The generation metadata for bot-generated messages (3).</param>
	private sealed record SeedData(
		ParticipantEntity[]               UserParticipants,
		ParticipantEntity[]               PersonaParticipants,
		UserEntity[]                      Users,
		RoleEntity[]                      Roles,
		UserRoleEntity[]                  UserRoles,
		ModelEndpointEntity[]             ModelEndpoints,
		PersonaEntity[]                   Personas,
		SystemPromptEntity[]              SystemPrompts,
		ConversationEntity[]              Conversations,
		ConversationParticipantEntity[]   ConversationParticipants,
		MessageEntity[]                   Messages,
		MessageGenerationMetadataEntity[] MessageGenerationMetadata);

	/// <summary>
	/// Creates a source <see cref="IntegrationTestHarness"/> with an empty database. Schema is created
	/// via <c>MigrateAsync()</c> to populate the <c>__EFMigrationsHistory</c> table, which is required
	/// for the import schema-mismatch check.
	/// </summary>
	/// <returns>A disposable harness with the full schema applied via migrations.</returns>
	private static async Task<IntegrationTestHarness> CreateSourceHarnessAsync()
	{
		IntegrationTestHarness harness = await IntegrationTestHarness
			                                 .CreateAsync("dport_src", ensureCreated: false)
			                                 .ConfigureAwait(false);

		await harness.Migrator.MigrateAsync().ConfigureAwait(false);
		return harness;
	}

	/// <summary>
	/// Creates a target <see cref="IntegrationTestHarness"/> with an empty database. Schema is created
	/// via <c>MigrateAsync()</c> so that the migration history matches the source.
	/// </summary>
	/// <returns>A disposable harness with the full schema applied via migrations.</returns>
	private static async Task<IntegrationTestHarness> CreateTargetHarnessAsync()
	{
		IntegrationTestHarness harness = await IntegrationTestHarness
			                                 .CreateAsync("dport_tgt", ensureCreated: false)
			                                 .ConfigureAwait(false);

		await harness.Migrator.MigrateAsync().ConfigureAwait(false);
		return harness;
	}

	/// <summary>
	/// Seeds a rich, FK-correct dataset into the source database covering all domain entity tables.
	/// </summary>
	/// <param name="dbContext">The source database context.</param>
	/// <returns>
	/// A <see cref="SeedData"/> instance holding all seeded entities for use as verification reference.
	/// </returns>
	/// <remarks>
	///     <para>
	///     The seed graph covers all domain tables with ~3 rows each:
	///     <see cref="ParticipantEntity"/> (6), <see cref="UserEntity"/> (3),
	///     <see cref="RoleEntity"/> (3), <see cref="UserRoleEntity"/> (3),
	///     <see cref="ModelEndpointEntity"/> (3), <see cref="PersonaEntity"/> (3),
	///     <see cref="SystemPromptEntity"/> (3), <see cref="ConversationEntity"/> (3),
	///     <see cref="ConversationParticipantEntity"/> (7), <see cref="MessageEntity"/> (8),
	///     <see cref="MessageGenerationMetadataEntity"/> (3).
	///     </para>
	///     <para>
	///     Data variety includes: nullable fields with both <see langword="null"/> and non-null values
	///     (Email, AvatarUrl, EncryptedCredentials, FullPrompt, MaxTokens, TopP, LastLoginAtUtc,
	///     LastTokenRefreshAtUtc, Content, RedactedAtUtc), boolean defaults vs. explicit <see langword="false"/>
	///     (IsActive), all <see cref="ConversationParticipantRole"/> enum values (Owner, Member, Observer),
	///     a nullable enum with non-null value (<see cref="MessageRedactionReason"/>),
	///     an orphaned message with <see cref="MessageEntity.SenderId"/> = <see langword="null"/> (simulates
	///     <c>DeleteBehavior.SetNull</c>), a redacted message with
	///     <see cref="MessageEntity.Content"/> = <see langword="null"/>, and different
	///     <see cref="ConversationEntity.CreatedAtUtc"/>/<see cref="ConversationEntity.UpdatedAtUtc"/> timestamp
	///     combinations.
	///     </para>
	///     <para>
	///     <see cref="RevokedJwtEntity"/> and <see cref="SeedHistoryEntity"/> are not seeded here because
	///     the existing tables already provide sufficient FK and type-variety coverage for the roundtrip.
	///     Both tables participate normally in a real export/import — they are simply empty in this test.
	///     </para>
	/// </remarks>
	private static async Task<SeedData> SeedTestDataAsync(LumaCoreDbContext dbContext)
	{
		var now = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

		// ====================================================================
		// Participants (6): 3 human users + 3 AI personas.
		// Creative Writer has AvatarUrl set — nullable string roundtrip.
		// Different CreatedAtUtc values on users test timestamp variety.
		// ====================================================================
		var alice = new ParticipantEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
			CreatedAtUtc = now,
			DisplayName = "Alice"
		};
		var bob = new ParticipantEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
			CreatedAtUtc = now.AddMinutes(1),
			DisplayName = "Bob"
		};
		var carol = new ParticipantEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
			CreatedAtUtc = now.AddMinutes(2),
			DisplayName = "Carol"
		};
		var helpfulBot = new ParticipantEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000004"),
			CreatedAtUtc = now,
			DisplayName = "Helpful Bot"
		};
		var codeAssistant = new ParticipantEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000005"),
			CreatedAtUtc = now,
			DisplayName = "Code Assistant"
		};
		var creativeWriter = new ParticipantEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000006"),
			CreatedAtUtc = now,
			DisplayName = "Creative Writer",
			AvatarUrl = "https://example.com/writer-avatar.png" // Non-null — nullable string roundtrip.
		};

		ParticipantEntity[] userParticipants = [alice, bob, carol];
		ParticipantEntity[] personaParticipants = [helpfulBot, codeAssistant, creativeWriter];
		dbContext.Participants.AddRange(userParticipants);
		dbContext.Participants.AddRange(personaParticipants);
		await dbContext.SaveChangesAsync().ConfigureAwait(false);

		// ====================================================================
		// Users (3): each linked to their participant.
		//   - alice: standard (all nullable fields null)
		//   - bob:   Email set — nullable string roundtrip
		//   - carol: LastLoginAtUtc + LastTokenRefreshAtUtc — nullable DateTime roundtrip
		// ====================================================================
		var userAlice = new UserEntity
		{
			ParticipantId = alice.Id,
			Username = "alice",
			UsernameNormalized = "ALICE",
			PasswordHash = "$2a$12$fakehashfakehashfakehashfakehashfakehashfakehashfake12"
		};
		var userBob = new UserEntity
		{
			ParticipantId = bob.Id,
			Username = "bob",
			UsernameNormalized = "BOB",
			PasswordHash = "$2a$12$anotherfakehashfakehashfakehashfakehashfakehashfake34",
			Email = "bob@example.com" // Non-null — nullable string roundtrip.
		};
		var userCarol = new UserEntity
		{
			ParticipantId = carol.Id,
			Username = "carol",
			UsernameNormalized = "CAROL",
			PasswordHash = "$2a$12$yetanotherfakehashfakehashfakehashfakehashfakehashf56",
			LastLoginAtUtc = now.AddHours(-1),          // Non-null — nullable DateTime roundtrip.
			LastTokenRefreshAtUtc = now.AddMinutes(-30) // Non-null — nullable DateTime roundtrip.
		};

		UserEntity[] users = [userAlice, userBob, userCarol];
		dbContext.Users.AddRange(users);

		// ====================================================================
		// Roles (3): admin has Description set, the other two have null.
		// ====================================================================
		var roleAdmin = new RoleEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000010"),
			CreatedAtUtc = now,
			Name = "admin",
			Description = "Full system access" // Non-null — nullable string roundtrip.
		};
		var roleModerator = new RoleEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000011"),
			CreatedAtUtc = now,
			Name = "moderator"
		};
		var roleUser = new RoleEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000012"),
			CreatedAtUtc = now,
			Name = "user"
		};

		RoleEntity[] roles = [roleAdmin, roleModerator, roleUser];
		dbContext.Roles.AddRange(roles);
		await dbContext.SaveChangesAsync().ConfigureAwait(false);

		// ====================================================================
		// UserRoles (3): one per user. Different AssignedAtUtc to test timestamps.
		// ====================================================================
		var urAliceAdmin = new UserRoleEntity
		{
			UserId = userAlice.Id,
			RoleId = roleAdmin.Id,
			AssignedAtUtc = now
		};
		var urBobModerator = new UserRoleEntity
		{
			UserId = userBob.Id,
			RoleId = roleModerator.Id,
			AssignedAtUtc = now.AddMinutes(1)
		};
		var urCarolUser = new UserRoleEntity
		{
			UserId = userCarol.Id,
			RoleId = roleUser.Id,
			AssignedAtUtc = now.AddMinutes(2)
		};

		UserRoleEntity[] userRoles = [urAliceAdmin, urBobModerator, urCarolUser];
		dbContext.UserRoles.AddRange(userRoles);

		// ====================================================================
		// ModelEndpoints (3):
		//   - Ollama:    active (default true), no credentials, no description
		//   - OpenAI:    active, with EncryptedCredentials + Description
		//   - Anthropic: IsActive=false — boolean roundtrip (default is true)
		// ====================================================================
		var endpointOllama = new ModelEndpointEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000020"),
			CreatedAtUtc = now,
			ProviderType = "ollama",
			BaseUrl = "http://localhost:11434",
			Name = "Local Ollama"
		};
		var endpointOpenAi = new ModelEndpointEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000021"),
			CreatedAtUtc = now,
			ProviderType = "openai",
			BaseUrl = "https://api.openai.com",
			Name = "OpenAI Cloud",
			Description = "OpenAI API endpoint",           // Non-null — nullable string roundtrip.
			EncryptedCredentials = "enc:sk-fake-key-12345" // Non-null — nullable string roundtrip.
		};
		var endpointAnthropic = new ModelEndpointEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000022"),
			CreatedAtUtc = now,
			ProviderType = "anthropic",
			BaseUrl = "https://api.anthropic.com",
			Name = "Anthropic Cloud",
			IsActive = false,                                      // Explicit false — boolean roundtrip.
			EncryptedCredentials = "enc:sk-fake-anthropic-key-789" // Non-null — second non-null credential.
		};

		ModelEndpointEntity[] endpoints = [endpointOllama, endpointOpenAi, endpointAnthropic];
		dbContext.ModelEndpoints.AddRange(endpoints);
		await dbContext.SaveChangesAsync().ConfigureAwait(false);

		// ====================================================================
		// Personas (3): each linked to a persona participant.
		//   - Creative Writer has IsActive=false — boolean roundtrip (default is true).
		//   - ActiveSystemPromptId is wired up after prompts are created below.
		// ====================================================================
		var personaBot = new PersonaEntity
		{
			ParticipantId = helpfulBot.Id,
			DefaultModel = "mistral:7b",
			Description = "A helpful assistant"
		};
		var personaCoder = new PersonaEntity
		{
			ParticipantId = codeAssistant.Id,
			DefaultModel = "codellama:13b",
			Description = "A code review expert"
		};
		var personaWriter = new PersonaEntity
		{
			ParticipantId = creativeWriter.Id,
			DefaultModel = "llama2:70b",
			Description = "A creative writing aide",
			IsActive = false // Explicit false — boolean roundtrip.
		};

		PersonaEntity[] personas = [personaBot, personaCoder, personaWriter];
		dbContext.Personas.AddRange(personas);
		await dbContext.SaveChangesAsync().ConfigureAwait(false);

		// ====================================================================
		// SystemPrompts (3): one per persona.
		// After creation, ActiveSystemPromptId is wired up on each persona.
		// ====================================================================
		var promptBot = new SystemPromptEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000030"),
			PersonaId = personaBot.Id,
			CreatedAtUtc = now,
			Content = "You are a helpful assistant.",
			Hash = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2"
		};
		var promptCoder = new SystemPromptEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000031"),
			PersonaId = personaCoder.Id,
			CreatedAtUtc = now,
			Content = "You are a code review expert. Focus on correctness and readability.",
			Hash = "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3"
		};
		var promptWriter = new SystemPromptEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000032"),
			PersonaId = personaWriter.Id,
			CreatedAtUtc = now,
			Content = "You are a creative writing aide. Be imaginative and eloquent.",
			Hash = "c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4"
		};

		SystemPromptEntity[] prompts = [promptBot, promptCoder, promptWriter];
		dbContext.SystemPrompts.AddRange(prompts);
		await dbContext.SaveChangesAsync().ConfigureAwait(false);

		// Wire up ActiveSystemPromptId now that prompts have their IDs.
		personaBot.ActiveSystemPromptId = promptBot.Id;
		personaCoder.ActiveSystemPromptId = promptCoder.Id;
		personaWriter.ActiveSystemPromptId = promptWriter.Id;

		// ====================================================================
		// Conversations (3):
		//   - General Chat:     CreatedAtUtc == UpdatedAtUtc (no updates yet)
		//   - Code Review:      UpdatedAtUtc 1 h after CreatedAtUtc — different timestamps
		//   - Creative Writing: UpdatedAtUtc 2 h after CreatedAtUtc
		// ====================================================================
		var convGeneral = new ConversationEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000040"),
			Title = "General Chat",
			CreatedAtUtc = now,
			UpdatedAtUtc = now // Same as CreatedAtUtc — tests equal-timestamp roundtrip.
		};
		var convCodeReview = new ConversationEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000041"),
			Title = "Code Review Session",
			CreatedAtUtc = now,
			UpdatedAtUtc = now.AddHours(1) // Different from CreatedAtUtc — timestamp roundtrip.
		};
		var convCreative = new ConversationEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000042"),
			Title = "Creative Writing",
			CreatedAtUtc = now,
			UpdatedAtUtc = now.AddHours(2)
		};

		ConversationEntity[] conversations = [convGeneral, convCodeReview, convCreative];
		dbContext.Conversations.AddRange(conversations);
		await dbContext.SaveChangesAsync().ConfigureAwait(false);

		// ====================================================================
		// ConversationParticipants (7):
		//   Conv 1: Alice (Owner) + Helpful Bot (Member)
		//   Conv 2: Bob (Owner) + Code Assistant (Member)
		//   Conv 3: Carol (Owner) + Alice (Observer) + Creative Writer (Member)
		//   → All three ConversationParticipantRole enum values exercised.
		//   Alice in Conv 3 joined later (JoinedAtUtc + 5 min) — different timestamp.
		// ====================================================================
		var cpAliceGeneral = new ConversationParticipantEntity
		{
			ConversationId = convGeneral.Id,
			ParticipantId = alice.Id,
			JoinedAtUtc = now,
			Role = ConversationParticipantRole.Owner
		};
		var cpBotGeneral = new ConversationParticipantEntity
		{
			ConversationId = convGeneral.Id,
			ParticipantId = helpfulBot.Id,
			JoinedAtUtc = now,
			Role = ConversationParticipantRole.Member
		};
		var cpBobReview = new ConversationParticipantEntity
		{
			ConversationId = convCodeReview.Id,
			ParticipantId = bob.Id,
			JoinedAtUtc = now,
			Role = ConversationParticipantRole.Owner
		};
		var cpCoderReview = new ConversationParticipantEntity
		{
			ConversationId = convCodeReview.Id,
			ParticipantId = codeAssistant.Id,
			JoinedAtUtc = now,
			Role = ConversationParticipantRole.Member
		};
		var cpCarolCreative = new ConversationParticipantEntity
		{
			ConversationId = convCreative.Id,
			ParticipantId = carol.Id,
			JoinedAtUtc = now,
			Role = ConversationParticipantRole.Owner
		};
		var cpAliceCreative = new ConversationParticipantEntity
		{
			ConversationId = convCreative.Id,
			ParticipantId = alice.Id,
			JoinedAtUtc = now.AddMinutes(5),            // Joined later — tests different timestamp.
			Role = ConversationParticipantRole.Observer // Third enum value — Observer roundtrip.
		};
		var cpWriterCreative = new ConversationParticipantEntity
		{
			ConversationId = convCreative.Id,
			ParticipantId = creativeWriter.Id,
			JoinedAtUtc = now,
			Role = ConversationParticipantRole.Member
		};

		ConversationParticipantEntity[] convParticipants =
		[
			cpAliceGeneral, cpBotGeneral, cpBobReview, cpCoderReview,
			cpCarolCreative, cpAliceCreative, cpWriterCreative
		];
		dbContext.ConversationParticipants.AddRange(convParticipants);

		// ====================================================================
		// Messages (8): 2 per conversation (user + bot reply) + 1 orphaned message
		//   (sender deleted) + 1 redacted message.
		//   Conv 1 includes a redacted follow-up from Alice — Content=null,
		//     RedactedAtUtc=non-null, RedactionReason=non-null. This covers:
		//     nullable string (Content) with actual null, nullable DateTime
		//     (RedactedAtUtc) with non-null value, and nullable enum
		//     (MessageRedactionReason) roundtrip.
		//   Conv 3 includes an orphaned message (SenderId = null) — simulates
		//     DeleteBehavior.SetNull after participant removal. Tests nullable FK roundtrip.
		//   Bot replies have generation metadata (see next section).
		// ====================================================================

		// Conv 1: Alice ↔ Helpful Bot
		var msgAlice1 = new MessageEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000050"),
			ConversationId = convGeneral.Id,
			SenderId = alice.Id,
			CreatedAtUtc = now,
			Content = "Hello, can you help me?"
		};
		var msgBot1 = new MessageEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000051"),
			ConversationId = convGeneral.Id,
			SenderId = helpfulBot.Id,
			CreatedAtUtc = now.AddSeconds(1),
			Content = "Of course! How can I assist you today?"
		};

		// Redacted follow-up from Alice: content removed at user's request.
		// Covers nullable string (Content=null), nullable DateTime (RedactedAtUtc non-null),
		// and nullable enum (MessageRedactionReason non-null) roundtrip.
		var msgAliceRedacted = new MessageEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000057"),
			ConversationId = convGeneral.Id,
			SenderId = alice.Id,
			CreatedAtUtc = now.AddSeconds(2),
			Content = null, // Redacted — nullable string with actual null.
			RedactedAtUtc = now.AddHours(1), // Non-null — nullable DateTime roundtrip.
			RedactionReason = MessageRedactionReason.UserRequestedDeletion // Non-null — nullable enum roundtrip.
		};

		// Conv 2: Bob ↔ Code Assistant
		var msgBob1 = new MessageEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000052"),
			ConversationId = convCodeReview.Id,
			SenderId = bob.Id,
			CreatedAtUtc = now,
			Content = "Please review this code for potential issues."
		};
		var msgCoder1 = new MessageEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000053"),
			ConversationId = convCodeReview.Id,
			SenderId = codeAssistant.Id,
			CreatedAtUtc = now.AddSeconds(2),
			Content = "The code looks clean. Consider adding error handling for edge cases."
		};

		// Conv 3: orphaned message + Carol ↔ Creative Writer.
		// SenderId is null — simulates DeleteBehavior.SetNull after the original sender was removed.
		// This tests nullable FK (ParticipantId?) roundtrip on the SenderId column.
		var msgOrphaned = new MessageEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000054"),
			ConversationId = convCreative.Id,
			SenderId = null,
			CreatedAtUtc = now,
			Content = "This message's sender was deleted."
		};
		var msgCarol1 = new MessageEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000055"),
			ConversationId = convCreative.Id,
			SenderId = carol.Id,
			CreatedAtUtc = now.AddSeconds(1),
			Content = "Let's write a story together."
		};
		var msgWriter1 = new MessageEntity
		{
			PublicId = Guid.Parse("00000000-0000-0000-0000-000000000056"),
			ConversationId = convCreative.Id,
			SenderId = creativeWriter.Id,
			CreatedAtUtc = now.AddSeconds(3),
			Content = "Once upon a time in a digital realm..."
		};

		MessageEntity[] messages =
		[
			msgAlice1,
			msgBot1,
			msgAliceRedacted,
			msgBob1,
			msgCoder1,
			msgOrphaned,
			msgCarol1,
			msgWriter1
		];
		dbContext.Messages.AddRange(messages);
		await dbContext.SaveChangesAsync().ConfigureAwait(false);

		// ====================================================================
		// MessageGenerationMetadata (3): one per bot-generated message.
		//   - Bot 1:    basic — no FullPrompt, no MaxTokens, no TopP (all null)
		//   - Coder 1:  FullPrompt + MaxTokens set — nullable string/int roundtrip
		//   - Writer 1: TopP + MaxTokens set — nullable double/int roundtrip, no FullPrompt
		// ====================================================================
		var metaBot1 = new MessageGenerationMetadataEntity
		{
			MessageId = msgBot1.Id,
			ModelEndpointId = endpointOllama.Id,
			SystemPromptId = promptBot.Id,
			Model = "mistral:7b",
			PromptTokens = 42,
			CompletionTokens = 128,
			ResponseTime = TimeSpan.FromMilliseconds(1500),
			Temperature = 0.7
		};
		var metaCoder1 = new MessageGenerationMetadataEntity
		{
			MessageId = msgCoder1.Id,
			ModelEndpointId = endpointOpenAi.Id,
			SystemPromptId = promptCoder.Id,
			Model = "gpt-4-turbo",
			PromptTokens = 85,
			CompletionTokens = 256,
			ResponseTime = TimeSpan.FromMilliseconds(3200),
			Temperature = 0.3,
			MaxTokens = 512,                                                // Non-null — nullable int roundtrip.
			FullPrompt = "system: You are a code review expert.\nuser: ..." // Non-null — nullable string.
		};
		var metaWriter1 = new MessageGenerationMetadataEntity
		{
			MessageId = msgWriter1.Id,
			ModelEndpointId = endpointAnthropic.Id,
			SystemPromptId = promptWriter.Id,
			Model = "claude-3-opus",
			PromptTokens = 120,
			CompletionTokens = 512,
			ResponseTime = TimeSpan.FromMilliseconds(5800),
			Temperature = 0.9,
			MaxTokens = 1024,
			TopP = 0.95 // Non-null — nullable double roundtrip.
		};

		MessageGenerationMetadataEntity[] metadata = [metaBot1, metaCoder1, metaWriter1];
		dbContext.MessageGenerationMetadata.AddRange(metadata);
		await dbContext.SaveChangesAsync().ConfigureAwait(false);

		return new SeedData(
			UserParticipants: userParticipants,
			PersonaParticipants: personaParticipants,
			Users: users,
			Roles: roles,
			UserRoles: userRoles,
			ModelEndpoints: endpoints,
			Personas: personas,
			SystemPrompts: prompts,
			Conversations: conversations,
			ConversationParticipants: convParticipants,
			Messages: messages,
			MessageGenerationMetadata: metadata);
	}

	/// <summary>
	/// Verifies that the target database contains the same row counts as the source database
	/// by comparing each domain table directly.
	/// </summary>
	/// <param name="source">The source harness containing the seeded data.</param>
	/// <param name="target">The target harness whose data is being verified.</param>
	private static async Task VerifyRowCountsAsync(IntegrationTestHarness source, IntegrationTestHarness target)
	{
		string[] tables =
		[
			"Participants",
			"Users",
			"Roles",
			"UserRoles",
			"Conversations",
			"ConversationParticipants",
			"Messages",
			"ModelEndpoints",
			"Personas",
			"SystemPrompts",
			"MessageGenerationMetadata"
		];

		foreach (string table in tables)
		{
			Assert.Equal(
				await source.CountRowsAsync(table).ConfigureAwait(false),
				await target.CountRowsAsync(table).ConfigureAwait(false));
		}
	}

	/// <summary>
	/// Verifies that all entity data in the target database matches the original <paramref name="seed"/>
	/// values by querying each entity type through <see cref="IntegrationTestHarness.DbContext"/> and
	/// asserting every persisted property — PKs, FKs, PublicIds, timestamps, nullable fields, booleans,
	/// and enums.
	/// </summary>
	/// <param name="seed">The seed entities used as expected-value reference.</param>
	/// <param name="target">The target harness whose data is being verified.</param>
	private static async Task VerifyEntityDataAsync(SeedData seed, IntegrationTestHarness target)
	{
		LumaCoreDbContext db = target.DbContext;

		// --- Participants: PK, PublicId, CreatedAtUtc, DisplayName, AvatarUrl (nullable) ---
		ParticipantEntity[] allSeedParticipants = [.. seed.UserParticipants, .. seed.PersonaParticipants];
		List<ParticipantEntity> actualParticipants = await db.Participants.ToListAsync().ConfigureAwait(false);
		Assert.Equal(allSeedParticipants.Length, actualParticipants.Count);

		foreach (ParticipantEntity expected in allSeedParticipants)
		{
			ParticipantEntity actual = Assert.Single(actualParticipants, p => p.Id == expected.Id);
			Assert.Equal(expected.PublicId, actual.PublicId);
			Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
			Assert.Equal(expected.DisplayName, actual.DisplayName);
			Assert.Equal(expected.AvatarUrl, actual.AvatarUrl);
		}

		// --- Users: PK, FK (ParticipantId), all scalars, nullable Email/LastLogin/LastTokenRefresh ---
		List<UserEntity> actualUsers = await db.Users.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.Users.Length, actualUsers.Count);

		foreach (UserEntity expected in seed.Users)
		{
			UserEntity actual = Assert.Single(actualUsers, u => u.Id == expected.Id);
			Assert.Equal(expected.ParticipantId, actual.ParticipantId);
			Assert.Equal(expected.Username, actual.Username);
			Assert.Equal(expected.UsernameNormalized, actual.UsernameNormalized);
			Assert.Equal(expected.PasswordHash, actual.PasswordHash);
			Assert.Equal(expected.Email, actual.Email);
			Assert.Equal(expected.LastLoginAtUtc, actual.LastLoginAtUtc);
			Assert.Equal(expected.LastTokenRefreshAtUtc, actual.LastTokenRefreshAtUtc);
		}

		// --- Roles: PK, PublicId, CreatedAtUtc, Name, Description (nullable) ---
		List<RoleEntity> actualRoles = await db.Roles.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.Roles.Length, actualRoles.Count);

		foreach (RoleEntity expected in seed.Roles)
		{
			RoleEntity actual = Assert.Single(actualRoles, r => r.Id == expected.Id);
			Assert.Equal(expected.PublicId, actual.PublicId);
			Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
			Assert.Equal(expected.Name, actual.Name);
			Assert.Equal(expected.Description, actual.Description);
		}

		// --- UserRoles: composite PK (UserId+RoleId), AssignedAtUtc ---
		List<UserRoleEntity> actualUserRoles = await db.UserRoles.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.UserRoles.Length, actualUserRoles.Count);

		foreach (UserRoleEntity expected in seed.UserRoles)
		{
			UserRoleEntity actual = Assert.Single(
				actualUserRoles,
				ur => ur.UserId == expected.UserId && ur.RoleId == expected.RoleId);
			Assert.Equal(expected.AssignedAtUtc, actual.AssignedAtUtc);
		}

		// --- ModelEndpoints: PK, PublicId, all scalars, IsActive (bool), nullable Description/Credentials ---
		List<ModelEndpointEntity> actualEndpoints =
			await db.ModelEndpoints.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.ModelEndpoints.Length, actualEndpoints.Count);

		foreach (ModelEndpointEntity expected in seed.ModelEndpoints)
		{
			ModelEndpointEntity actual = Assert.Single(actualEndpoints, e => e.Id == expected.Id);
			Assert.Equal(expected.PublicId, actual.PublicId);
			Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
			Assert.Equal(expected.ProviderType, actual.ProviderType);
			Assert.Equal(expected.BaseUrl, actual.BaseUrl);
			Assert.Equal(expected.Name, actual.Name);
			Assert.Equal(expected.Description, actual.Description);
			Assert.Equal(expected.IsActive, actual.IsActive);
			Assert.Equal(expected.EncryptedCredentials, actual.EncryptedCredentials);
		}

		// --- Personas: PK, FKs (ParticipantId, ActiveSystemPromptId nullable), IsActive (bool) ---
		List<PersonaEntity> actualPersonas = await db.Personas.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.Personas.Length, actualPersonas.Count);

		foreach (PersonaEntity expected in seed.Personas)
		{
			PersonaEntity actual = Assert.Single(actualPersonas, p => p.Id == expected.Id);
			Assert.Equal(expected.ParticipantId, actual.ParticipantId);
			Assert.Equal(expected.ActiveSystemPromptId, actual.ActiveSystemPromptId);
			Assert.Equal(expected.DefaultModel, actual.DefaultModel);
			Assert.Equal(expected.Description, actual.Description);
			Assert.Equal(expected.IsActive, actual.IsActive);
		}

		// --- SystemPrompts: PK, PublicId, FK (PersonaId), CreatedAtUtc, Content, Hash ---
		List<SystemPromptEntity> actualPrompts =
			await db.SystemPrompts.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.SystemPrompts.Length, actualPrompts.Count);

		foreach (SystemPromptEntity expected in seed.SystemPrompts)
		{
			SystemPromptEntity actual = Assert.Single(actualPrompts, sp => sp.Id == expected.Id);
			Assert.Equal(expected.PublicId, actual.PublicId);
			Assert.Equal(expected.PersonaId, actual.PersonaId);
			Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
			Assert.Equal(expected.Content, actual.Content);
			Assert.Equal(expected.Hash, actual.Hash);
		}

		// --- Conversations: PK, PublicId, Title, CreatedAtUtc, UpdatedAtUtc ---
		List<ConversationEntity> actualConversations = await db.Conversations.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.Conversations.Length, actualConversations.Count);

		foreach (ConversationEntity expected in seed.Conversations)
		{
			ConversationEntity actual = Assert.Single(actualConversations, c => c.Id == expected.Id);
			Assert.Equal(expected.PublicId, actual.PublicId);
			Assert.Equal(expected.Title, actual.Title);
			Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
			Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
		}

		// --- ConversationParticipants: composite PK (ConversationId+ParticipantId), JoinedAtUtc, Role (enum) ---
		List<ConversationParticipantEntity> actualConvParticipants =
			await db.ConversationParticipants.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.ConversationParticipants.Length, actualConvParticipants.Count);

		foreach (ConversationParticipantEntity expected in seed.ConversationParticipants)
		{
			ConversationParticipantEntity actual = Assert.Single(
				actualConvParticipants,
				cp => cp.ConversationId == expected.ConversationId &&
				      cp.ParticipantId == expected.ParticipantId);
			Assert.Equal(expected.JoinedAtUtc, actual.JoinedAtUtc);
			Assert.Equal(expected.Role, actual.Role);
		}

		// --- Messages: PK, PublicId, FK (ConversationId), FK (SenderId nullable), Content, timestamps ---
		List<MessageEntity> actualMessages = await db.Messages.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.Messages.Length, actualMessages.Count);

		foreach (MessageEntity expected in seed.Messages)
		{
			MessageEntity actual = Assert.Single(actualMessages, m => m.Id == expected.Id);
			Assert.Equal(expected.PublicId, actual.PublicId);
			Assert.Equal(expected.ConversationId, actual.ConversationId);
			Assert.Equal(expected.SenderId, actual.SenderId);
			Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
			Assert.Equal(expected.Content, actual.Content);
			Assert.Equal(expected.RedactedAtUtc, actual.RedactedAtUtc);
			Assert.Equal(expected.RedactionReason, actual.RedactionReason);
		}

		// --- MessageGenerationMetadata: PK (MessageId), FKs, Model, nullable FullPrompt/MaxTokens/TopP ---
		List<MessageGenerationMetadataEntity> actualMetadata =
			await db.MessageGenerationMetadata.ToListAsync().ConfigureAwait(false);
		Assert.Equal(seed.MessageGenerationMetadata.Length, actualMetadata.Count);

		foreach (MessageGenerationMetadataEntity expected in seed.MessageGenerationMetadata)
		{
			MessageGenerationMetadataEntity actual =
				Assert.Single(actualMetadata, m => m.MessageId == expected.MessageId);
			Assert.Equal(expected.ModelEndpointId, actual.ModelEndpointId);
			Assert.Equal(expected.SystemPromptId, actual.SystemPromptId);
			Assert.Equal(expected.Model, actual.Model);
			Assert.Equal(expected.FullPrompt, actual.FullPrompt);
			Assert.Equal(expected.PromptTokens, actual.PromptTokens);
			Assert.Equal(expected.CompletionTokens, actual.CompletionTokens);
			Assert.Equal(expected.ResponseTime, actual.ResponseTime);
			Assert.Equal(expected.MaxTokens, actual.MaxTokens);
			Assert.Equal(expected.Temperature, actual.Temperature);
			Assert.Equal(expected.TopP, actual.TopP);
		}
	}
}
