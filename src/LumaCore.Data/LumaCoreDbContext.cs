// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Conventions;
using LumaCore.Data.Entities;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LumaCore.Data;

/// <summary>
/// Entity Framework Core database context for LumaCore.
/// </summary>
/// <remarks>
///     <para>
///     Manages all LumaCore entities with proper relationships, indexes, and constraints optimized for
///     chat history retrieval patterns and multi-participant conversations.
///     </para>
///     <para>
///     Supports multiple database providers: SQLite, PostgreSQL, and SQL Server.
///     The provider is configured via <see cref="DatabaseOptions"/>. MySQL/MariaDB support is temporarily
///     unavailable until an EF Core 10 compatible provider is available.
///     </para>
///     <para>
///     This context is registered via dependency injection and is intended to be used with a scoped lifetime.
///     Do not cache <see cref="LumaCoreDbContext"/> instances or use them concurrently across threads.
///     </para>
///     <para>
///     The database schema (tables, keys, indexes, constraints, and delete behaviors) is configured in
///     <see cref="OnModelCreating"/>.
///     </para>
/// </remarks>
public sealed partial class LumaCoreDbContext : DbContext
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LumaCoreDbContext"/> class.
	/// </summary>
	/// <param name="options">The database context options configured via dependency injection.</param>
	public LumaCoreDbContext(DbContextOptions<LumaCoreDbContext> options)
		: base(options) { }

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for conversation participants.
	/// </summary>
	public DbSet<ConversationParticipantEntity> ConversationParticipants { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for conversations.
	/// </summary>
	public DbSet<ConversationEntity> Conversations { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for message generation metadata.
	/// </summary>
	public DbSet<MessageGenerationMetadataEntity> MessageGenerationMetadata { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for messages.
	/// </summary>
	public DbSet<MessageEntity> Messages { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for model endpoints.
	/// </summary>
	public DbSet<ModelEndpointEntity> ModelEndpoints { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for participants.
	/// </summary>
	public DbSet<ParticipantEntity> Participants { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for personas.
	/// </summary>
	public DbSet<PersonaEntity> Personas { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for persona description translations.
	/// </summary>
	public DbSet<PersonaDescriptionTranslationEntity> PersonaDescriptionTranslations { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for resource GC state (singleton throttle row).
	/// </summary>
	public DbSet<ResourceGcStateEntity> ResourceGcState { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for resource references (ownership links to stored files).
	/// </summary>
	public DbSet<ResourceReferenceEntity> ResourceReferences { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for stored file resources.
	/// </summary>
	public DbSet<ResourceEntity> Resources { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for revoked JWT access tokens (token blacklist).
	/// </summary>
	public DbSet<RevokedJwtEntity> RevokedJwts { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for roles.
	/// </summary>
	public DbSet<RoleEntity> Roles { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for seed history (tracks applied database seeds).
	/// </summary>
	public DbSet<SeedHistoryEntity> SeedHistory { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for system prompts.
	/// </summary>
	public DbSet<SystemPromptEntity> SystemPrompts { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for user preferences.
	/// </summary>
	public DbSet<UserPreferencesEntity> UserPreferences { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for user-role assignments.
	/// </summary>
	public DbSet<UserRoleEntity> UserRoles { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for users.
	/// </summary>
	public DbSet<UserEntity> Users { get; set; } = null!;

	/// <summary>
	/// Configures context-level EF Core services.
	/// </summary>
	/// <param name="optionsBuilder">The builder used to configure this context instance.</param>
	/// <remarks>
	///     <para>
	///     <b>Per-provider model cache.</b> The model contains provider-specific SQL for the nullable unique
	///     <c>Users.Email</c> index filter. EF Core's default model cache key does not include the provider
	///     name, so the first provider used in a process would otherwise leak its model into later contexts of
	///     another provider. Replacing <see cref="IModelCacheKeyFactory"/> keeps one cached model per provider.
	///     </para>
	/// </remarks>
	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		base.OnConfiguring(optionsBuilder);

		optionsBuilder.ReplaceService<IModelCacheKeyFactory, DatabaseProviderModelCacheKeyFactory>();
	}

	/// <summary>
	/// Registers model-wide value converters: UTC normalization for all <see cref="DateTime"/> properties
	/// and strongly-typed entity identifier mappings (<c>XxxId</c> ↔ <see cref="long"/>).
	/// </summary>
	/// <param name="configurationBuilder">The conventions builder.</param>
	/// <remarks>
	///     <para>
	///     The <see cref="UtcDateTimeConverter"/> stamps <see cref="DateTimeKind.Utc"/> on every
	///     <see cref="DateTime"/> value entering or leaving EF Core. This prevents Npgsql from rejecting
	///     <see cref="DateTimeKind.Unspecified"/> values for <c>timestamp with time zone</c> columns and
	///     keeps the model provider-agnostic.
	///     </para>
	///     <para>
	///     <b>SQLite <c>AUTOINCREMENT</c> compensation.</b> EF Core's SQLite default-strategy resolver only
	///     recognizes integer primary keys when the <i>CLR</i> property type passes <c>IsInteger()</c>. The
	///     strongly-typed id converters mean the live model exposes <c>UserId</c> rather than
	///     <see cref="long"/>, so the resolver returns
	///     <see cref="Microsoft.EntityFrameworkCore.Metadata.SqliteValueGenerationStrategy.None"/>
	///     and <c>SqliteAnnotationProvider.For(IColumn)</c> emits no <c>Sqlite:Autoincrement</c>. Without
	///     compensation, schemas built directly from the model on SQLite (e.g. <see cref="DatabaseFacade.EnsureCreatedAsync"/>
	///     in tests) would lose <c>AUTOINCREMENT</c> on every PK column. The custom
	///     <see cref="SqliteAutoincrementForValueConvertedPrimaryKeysConvention"/> registered below runs at
	///     model-finalizing time and explicitly sets the autoincrement strategy on integer-backed PKs whose
	///     value converter targets <see cref="long"/> or <see cref="int"/>. Migration-driven schemas are
	///     unaffected either way because the migration source files carry <c>Sqlite:Autoincrement</c>
	///     annotations explicitly.
	///     </para>
	/// </remarks>
	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		base.ConfigureConventions(configurationBuilder);

		configurationBuilder.Conventions.Replace<SqliteValueGenerationConvention>(sp =>
			new SqliteAutoincrementForValueConvertedPrimaryKeysConvention(
				sp.GetRequiredService<ProviderConventionSetBuilderDependencies>(),
				sp.GetRequiredService<RelationalConventionSetBuilderDependencies>()));

		// Normalize all DateTime properties to DateTimeKind.Utc (defense-in-depth for multi-provider).
		configurationBuilder
			.Properties<DateTime>()
			.HaveConversion<UtcDateTimeConverter>();

		configurationBuilder
			.Properties<ConversationId>()
			.HaveConversion<ConversationIdConverter>();

		configurationBuilder
			.Properties<MessageId>()
			.HaveConversion<MessageIdConverter>();

		configurationBuilder
			.Properties<ModelEndpointId>()
			.HaveConversion<ModelEndpointIdConverter>();

		configurationBuilder
			.Properties<ParticipantId>()
			.HaveConversion<ParticipantIdConverter>();

		configurationBuilder
			.Properties<PersonaId>()
			.HaveConversion<PersonaIdConverter>();

		configurationBuilder
			.Properties<ResourceId>()
			.HaveConversion<ResourceIdConverter>();

		configurationBuilder
			.Properties<ResourceOwnerId>()
			.HaveConversion<ResourceOwnerIdConverter>();

		configurationBuilder
			.Properties<ResourceReferenceId>()
			.HaveConversion<ResourceReferenceIdConverter>();

		configurationBuilder
			.Properties<RoleId>()
			.HaveConversion<RoleIdConverter>();

		configurationBuilder
			.Properties<SystemPromptId>()
			.HaveConversion<SystemPromptIdConverter>();

		configurationBuilder
			.Properties<UserId>()
			.HaveConversion<UserIdConverter>();
	}

	/// <summary>
	/// Configures the database schema, relationships, and constraints.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model for this context.</param>
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		// Calls are intentionally kept in alphabetical order to match the physical method ordering in
		// this file. EF Core does not require a specific configuration order for independent entities,
		// so alphabetical is the cheapest convention to maintain (no domain-grouping debates, easy to
		// locate a single Configure*() method, additions slot in trivially).
		ConfigureConversation(modelBuilder);
		ConfigureConversationParticipant(modelBuilder);
		ConfigureMessage(modelBuilder);
		ConfigureMessageGenerationMetadata(modelBuilder);
		ConfigureModelEndpoint(modelBuilder);
		ConfigureParticipant(modelBuilder);
		ConfigurePersona(modelBuilder);
		ConfigurePersonaDescriptionTranslation(modelBuilder);
		ConfigureResource(modelBuilder);
		ConfigureResourceGcState(modelBuilder);
		ConfigureResourceReference(modelBuilder);
		ConfigureRevokedJwt(modelBuilder);
		ConfigureRole(modelBuilder);
		ConfigureSeedHistory(modelBuilder);
		ConfigureSystemPrompt(modelBuilder);
		ConfigureUser(modelBuilder, Database.ProviderName);
		ConfigureUserPreferences(modelBuilder);
		ConfigureUserRole(modelBuilder);
	}

	/// <summary>
	/// Configures table mapping, property constraints, and indexes for <see cref="ConversationEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureConversation(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ConversationEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("Conversations");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier ---

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_Conversations_PublicId");

			// --- 3. Foreign keys + Navigation properties (none) ---

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			entity.Property(e => e.UpdatedAtUtc)
				.IsRequired();

			entity.HasIndex(e => e.UpdatedAtUtc)
				.HasDatabaseName("IX_Conversations_UpdatedAtUtc");

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.Title)
				.HasMaxLength(EntityLimits.ConversationTitleMaxLength)
				.IsRequired();

			entity.Property(e => e.Description)
				.HasMaxLength(EntityLimits.ConversationDescriptionMaxLength);

			// --- 6. Collection navigation properties (configured from the owning sides:
			//        Messages, ConversationParticipants) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships
	/// for <see cref="ConversationParticipantEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureConversationParticipant(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ConversationParticipantEntity>(entity =>
		{
			// --- 1. Table mapping & composite key ---

			// Composite primary key — the two FK columns combined; the per-FK configuration
			// (HasOne / OnDelete) lives in the dedicated FK + Navigation sections below.
			entity.ToTable("ConversationParticipants");

			entity.HasKey(e => new { e.ConversationId, e.ParticipantId });

			// --- 2. First FK + Navigation ---

			entity.HasOne(e => e.Conversation)
				.WithMany(c => c.Participants)
				.HasForeignKey(e => e.ConversationId)
				.OnDelete(DeleteBehavior.Cascade);

			// --- 3. Second FK + Navigation ---

			entity.HasOne(e => e.Participant)
				.WithMany(p => p.ConversationParticipants)
				.HasForeignKey(e => e.ParticipantId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(e => e.ParticipantId)
				.HasDatabaseName("IX_ConversationParticipants_ParticipantId");

			// --- 4. Timestamps ---

			entity.Property(e => e.JoinedAtUtc)
				.IsRequired();

			// --- 5. Other properties ---

			entity.Property(e => e.Role)
				.IsRequired();

			entity.Property(e => e.LastReadMessageId);

			// NoAction (instead of SetNull) to avoid a SQL Server multi-cascade-path error:
			// Conversation cascades into both ConversationParticipants and Messages, and SetNull on this
			// secondary FK would form a second cascade path into ConversationParticipants. Safe in practice
			// because messages are only deleted via the Conversation cascade — which also removes the
			// dependent ConversationParticipants rows in the same operation, so no orphaned
			// LastReadMessageId values can survive.
			entity.HasOne(e => e.LastReadMessage)
				.WithMany()
				.HasForeignKey(e => e.LastReadMessageId)
				.OnDelete(DeleteBehavior.NoAction);

			entity.HasIndex(e => e.LastReadMessageId)
				.HasDatabaseName("IX_ConversationParticipants_LastReadMessageId");
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships for <see cref="MessageEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureMessage(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<MessageEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("Messages");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier ---

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_Messages_PublicId");

			// --- 3. Foreign keys + Navigation properties ---

			entity.Property(e => e.ConversationId)
				.IsRequired();

			entity.HasOne(e => e.Conversation)
				.WithMany(c => c.Messages)
				.HasForeignKey(e => e.ConversationId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.Property(e => e.SenderId);

			entity.HasOne(e => e.Sender)
				.WithMany(p => p.Messages)
				.HasForeignKey(e => e.SenderId)
				.OnDelete(DeleteBehavior.SetNull); // Preserve history; messages can outlive a deleted participant

			entity.HasIndex(e => e.SenderId)
				.HasDatabaseName("IX_Messages_SenderId");

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields ---

			// HasSentinel is intentionally omitted: MessageType.User == 0 is already the CLR default
			// for the enum, so EF Core's implicit sentinel and our DB default coincide — any
			// explicitly-set MessageType.System value is preserved. See ConfigureModelEndpoint() for
			// the inverted case (default true with HasSentinel(true)) and ConfigurePersona() for the
			// same coinciding-defaults pattern applied to PersonaVisibility.
			entity.Property(e => e.Type)
				.IsRequired()
				.HasDefaultValue(MessageType.User);

			entity.Property(e => e.Content);

			entity.Property(e => e.RedactedAtUtc);

			entity.Property(e => e.RedactionReason);

			// Composite (ConversationId, CreatedAtUtc) covers both per-conversation lookups (leading
			// column suffices for WHERE ConversationId = X) and chronological retrieval within a
			// conversation (ORDER BY CreatedAtUtc, Take(N) for context-window queries). No standalone
			// indexes on ConversationId or CreatedAtUtc exist — the leading-column rule covers the
			// former, and the codebase has no global "messages by time" query that would justify the
			// latter's INSERT-time write tax on this hot table.
			entity.HasIndex(e => new { e.ConversationId, e.CreatedAtUtc })
				.HasDatabaseName("IX_Messages_ConversationId_CreatedAtUtc");

			// --- 6. Collection navigation properties (none) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships
	/// for <see cref="MessageGenerationMetadataEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureMessageGenerationMetadata(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<MessageGenerationMetadataEntity>(entity =>
		{
			// --- 1. Table mapping & primary key (also foreign key to Messages.Id) ---

			entity.ToTable("MessageGenerationMetadata");

			entity.HasKey(e => e.MessageId);
			// MessageId is both the PK and the FK to Messages.Id (1:0..1 owned-side pattern — each
			// metadata row borrows its parent message's ID). Configured up front so the rest of this
			// method mirrors the entity layout (FK + Nav grouped per relationship, then scalars).
			entity.HasOne(e => e.Message)
				.WithOne(m => m.GenerationMetadata)
				.HasForeignKey<MessageGenerationMetadataEntity>(e => e.MessageId)
				.OnDelete(DeleteBehavior.Cascade);

			// --- 2. Public identifier (none) ---

			// --- 3. Foreign keys + Navigation properties ---

			entity.Property(e => e.ModelEndpointId)
				.IsRequired();

			entity.HasOne(e => e.ModelEndpoint)
				.WithMany(me => me.GenerationMetadata)
				.HasForeignKey(e => e.ModelEndpointId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(e => e.ModelEndpointId)
				.HasDatabaseName("IX_MessageGenerationMetadata_ModelEndpointId");

			entity.Property(e => e.SystemPromptId);

			entity.HasOne(e => e.SystemPrompt)
				.WithMany(sp => sp.GenerationMetadata)
				.HasForeignKey(e => e.SystemPromptId)
				.OnDelete(DeleteBehavior.SetNull);

			entity.HasIndex(e => e.SystemPromptId)
				.HasDatabaseName("IX_MessageGenerationMetadata_SystemPromptId");

			// --- 4. Timestamps (none) ---

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.Model)
				.HasMaxLength(EntityLimits.ModelIdentifierMaxLength)
				.IsRequired();

			// FullPrompt is intentionally stored without a length cap. It captures the complete prompt
			// sent to the model — system prompt + full conversation history + memory — and can grow into
			// the megabyte range for long sessions. On SQL Server this maps to NVARCHAR(MAX), on
			// PostgreSQL to text, on SQLite to TEXT. The column is store-only (never indexed, never
			// filtered, never used in WHERE clauses or joins), so the usual NVARCHAR(MAX) drawbacks
			// (cannot live in-row past 8 KB, no index seek possible) do not apply. Treat the value as
			// potentially sensitive — it may contain user content; see the retention/redaction guidance
			// on MessageGenerationMetadataEntity.FullPrompt before exposing it outside diagnostics.
			entity.Property(e => e.FullPrompt);

			entity.Property(e => e.PromptTokens)
				.IsRequired();

			entity.Property(e => e.CompletionTokens)
				.IsRequired();

			entity.Property(e => e.ResponseTime)
				.IsRequired();

			entity.Property(e => e.MaxTokens);

			entity.Property(e => e.Temperature);

			entity.Property(e => e.TopP);

			// --- 6. Collection navigation properties (none) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, and indexes for <see cref="ModelEndpointEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureModelEndpoint(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ModelEndpointEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("ModelEndpoints");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier ---

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_ModelEndpoints_PublicId");

			// --- 3. Foreign keys + Navigation properties (none) ---

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			entity.Property(e => e.UpdatedAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.ProviderType)
				.HasMaxLength(EntityLimits.ModelEndpointProviderTypeMaxLength)
				.IsRequired();

			entity.Property(e => e.BaseUrl)
				.HasMaxLength(EntityLimits.ModelEndpointBaseUrlMaxLength)
				.IsRequired();

			entity.Property(e => e.Name)
				.HasMaxLength(EntityLimits.ModelEndpointNameMaxLength)
				.IsRequired();

			entity.Property(e => e.Description)
				.HasMaxLength(EntityLimits.ModelEndpointDescriptionMaxLength);

			entity.Property(e => e.EncryptedCredentials)
				.HasMaxLength(EntityLimits.ModelEndpointEncryptedCredentialsMaxLength);

			// HasSentinel(true): Without this, EF Core uses the CLR default (false) as sentinel.
			// That means new entities with explicit IsActive = false would be silently overridden
			// by the database default (true), because EF Core treats false as "not set".
			// Setting the sentinel to true inverts the logic: true = "not set", false = "explicitly set".
			entity.Property(e => e.IsActive)
				.IsRequired()
				.HasDefaultValue(true)
				.HasSentinel(true);

			entity.HasIndex(e => e.IsActive)
				.HasDatabaseName("IX_ModelEndpoints_IsActive");

			// --- 6. Collection navigation properties (configured from the owning side:
			//        GenerationMetadata) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, and indexes for <see cref="ParticipantEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureParticipant(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ParticipantEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("Participants");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier ---

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_Participants_PublicId");

			// --- 3. Foreign keys + Navigation properties (single-entity reverse navs; configured
			//        from the User and Persona sides — see ConfigureUser / ConfigurePersona) ---

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.DisplayName)
				.HasMaxLength(EntityLimits.ParticipantDisplayNameMaxLength)
				.IsRequired();

			// --- 6. Collection navigation properties (configured from the owning sides:
			//        ConversationParticipants, Messages) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships for <see cref="PersonaEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigurePersona(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<PersonaEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("Personas");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier (none) ---

			// --- 3. Foreign keys + Navigation properties ---

			entity.Property(e => e.ParticipantId)
				.IsRequired();

			entity.HasOne(e => e.Participant)
				.WithOne(p => p.Persona)
				.HasForeignKey<PersonaEntity>(e => e.ParticipantId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(e => e.ParticipantId)
				.IsUnique()
				.HasDatabaseName("IX_Personas_ParticipantId");

			entity.Property(e => e.CreatedByParticipantId);

			// NoAction (instead of SetNull) to avoid a SQL Server multi-cascade-path error: Participants
			// already cascades into Personas via ParticipantId, so a SetNull on this secondary FK would
			// form a second path from Participants to Personas. Stand-alone deletes of a referenced
			// participant are blocked at the database level; in practice, participants are removed via the
			// user lifecycle (which scrubs/anonymizes rather than deletes the participant row).
			entity.HasOne(e => e.CreatedByParticipant)
				.WithMany()
				.HasForeignKey(e => e.CreatedByParticipantId)
				.OnDelete(DeleteBehavior.NoAction);

			entity.HasIndex(e => e.CreatedByParticipantId)
				.HasDatabaseName("IX_Personas_CreatedByParticipantId");

			entity.Property(e => e.ActiveSystemPromptId);

			entity.HasOne(e => e.ActiveSystemPrompt)
				.WithMany()
				.HasForeignKey(e => e.ActiveSystemPromptId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(e => e.ActiveSystemPromptId)
				.HasDatabaseName("IX_Personas_ActiveSystemPromptId");

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			entity.Property(e => e.UpdatedAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.DefaultModel)
				.HasMaxLength(EntityLimits.ModelIdentifierMaxLength);

			entity.Property(e => e.IsActive)
				.IsRequired()
				.HasDefaultValue(true)
				.HasSentinel(true); // see ConfigureModelEndpoint() for detailed explanation

			entity.HasIndex(e => e.IsActive)
				.HasDatabaseName("IX_Personas_IsActive");

			// HasSentinel is intentionally omitted: PersonaVisibility.Private == 0 is already the
			// CLR default for the enum, so EF Core's implicit sentinel and our DB default coincide —
			// any explicitly-set Shared value is preserved. Calling HasSentinel(Private) here would
			// be a no-op (and inverting it like ConfigureModelEndpoint does for IsActive is not
			// needed because Private is the desired default, not a "value we want to override").
			entity.Property(e => e.Visibility)
				.IsRequired()
				.HasDefaultValue(PersonaVisibility.Private);

			// --- 6. Collection navigation properties (configured from the owning sides:
			//        SystemPrompts, DescriptionTranslations) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships
	/// for <see cref="PersonaDescriptionTranslationEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigurePersonaDescriptionTranslation(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<PersonaDescriptionTranslationEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("PersonaDescriptionTranslations");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier (none) ---

			// --- 3. Foreign keys + Navigation properties ---

			entity.Property(e => e.PersonaId)
				.IsRequired();

			entity.HasOne(e => e.Persona)
				.WithMany(p => p.DescriptionTranslations)
				.HasForeignKey(e => e.PersonaId)
				.OnDelete(DeleteBehavior.Cascade);

			// Unique on (PersonaId, CultureCode) prevents duplicate translations for the same locale.
			entity.HasIndex(e => new { e.PersonaId, e.CultureCode })
				.IsUnique()
				.HasDatabaseName("IX_PersonaDescriptionTranslations_PersonaId_CultureCode");

			// --- 4. Timestamps (none) ---

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.CultureCode).IsRequired().HasMaxLength(10);
			entity.Property(e => e.Value).IsRequired().HasMaxLength(EntityLimits.PersonaDescriptionMaxLength);
			entity.Property(e => e.Source).IsRequired().HasConversion(s => (int)s, i => (TranslationSource)i);

			// --- 6. Collection navigation properties (none) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships for <see cref="ResourceEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureResource(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ResourceEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("Resources");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier (none) ---

			// --- 3. Foreign keys + Navigation properties ---

			entity.Property(e => e.CreatedByParticipantId);

			entity.HasOne(e => e.CreatedByParticipant)
				.WithMany()
				.HasForeignKey(e => e.CreatedByParticipantId)
				.OnDelete(DeleteBehavior.SetNull); // Preserve resource metadata when participant is deleted

			entity.HasIndex(e => e.CreatedByParticipantId)
				.HasDatabaseName("IX_Resources_CreatedByParticipantId");

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.ContentHash)
				.HasMaxLength(EntityLimits.Sha256HexLength)
				.IsRequired();

			entity.Property(e => e.StoragePath)
				.HasMaxLength(EntityLimits.ResourceStoragePathMaxLength)
				.IsRequired();

			// Unique on StoragePath: the application uses Guid.NewGuid() to derive paths, so collisions
			// are astronomically unlikely — but unique enforces that property at the database level. If a
			// collision ever occurs, the second insert fails with a DbUpdateException carrying this
			// index name, which ResourceService.UploadAsync catches to retry with a fresh GUID.
			entity.HasIndex(e => e.StoragePath)
				.IsUnique()
				.HasDatabaseName("IX_Resources_StoragePath");

			entity.Property(e => e.SizeBytes)
				.IsRequired();

			entity.Property(e => e.DeletionState)
				.IsRequired();

			// Composite unique: at most one Active and one PendingDeletion row per content hash.
			// This separates upload operations (Active only) from GC (PendingDeletion only).
			entity.HasIndex(e => new { e.ContentHash, e.DeletionState })
				.IsUnique()
				.HasDatabaseName("IX_Resources_ContentHash_DeletionState");

			// For GC MARK/SWEEP queries that filter by deletion state.
			entity.HasIndex(e => e.DeletionState)
				.HasDatabaseName("IX_Resources_DeletionState");

			// --- 6. Collection navigation properties (configured from the owning side:
			//        References) ---
		});
	}

	/// <summary>
	/// Configures table mapping and property constraints for <see cref="ResourceGcStateEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureResourceGcState(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ResourceGcStateEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("ResourceGcState");
			entity.HasKey(e => e.Id);

			// Not auto-incremented — the application always uses Id = 1 (singleton row).
			entity.Property(e => e.Id)
				.ValueGeneratedNever();

			// --- 2. Public identifier (none) ---

			// --- 3. Foreign keys + Navigation properties (none) ---

			// --- 4. Timestamps ---

			entity.Property(e => e.LastRunAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields (none) ---

			// --- 6. Collection navigation properties (none) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships
	/// for <see cref="ResourceReferenceEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureResourceReference(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ResourceReferenceEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("ResourceReferences");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier ---

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_ResourceReferences_PublicId");

			// --- 3. Foreign keys + Navigation properties ---

			entity.Property(e => e.ResourceId)
				.IsRequired();

			entity.HasOne(e => e.Resource)
				.WithMany(r => r.References)
				.HasForeignKey(e => e.ResourceId)
				.OnDelete(DeleteBehavior.Cascade); // GC sweep deletes resource → references cascade

			entity.HasIndex(e => e.ResourceId)
				.HasDatabaseName("IX_ResourceReferences_ResourceId");

			entity.Property(e => e.OwnerKind)
				.IsRequired();

			entity.Property(e => e.OwnerId)
				.IsRequired();

			// Composite index for per-owner lookups (e.g. "all attachments of message X").
			entity.HasIndex(e => new { e.OwnerKind, e.OwnerId })
				.HasDatabaseName("IX_ResourceReferences_OwnerKind_OwnerId");

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.ContentType)
				.HasMaxLength(EntityLimits.ResourceContentTypeMaxLength)
				.IsRequired();

			entity.Property(e => e.OriginalFileName)
				.HasMaxLength(EntityLimits.ResourceOriginalFileNameMaxLength);

			// --- 6. Collection navigation properties (none) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, and indexes for <see cref="RevokedJwtEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureRevokedJwt(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<RevokedJwtEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("RevokedJwts");
			entity.HasKey(e => e.Jti);

			entity.Property(e => e.Jti)
				.HasMaxLength(EntityLimits.RevokedJwtJtiMaxLength)
				.IsRequired();

			// --- 2. Public identifier (none) ---

			// --- 3. Foreign keys + Navigation properties (none) ---

			// --- 4. Timestamps ---

			entity.Property(e => e.RevokedAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.ExpiresAtUtc)
				.IsRequired();

			// For cleanup queries that remove expired revocation entries.
			entity.HasIndex(e => e.ExpiresAtUtc)
				.HasDatabaseName("IX_RevokedJwts_ExpiresAtUtc");

			entity.Property(e => e.Subject)
				.HasMaxLength(EntityLimits.RevokedJwtSubjectMaxLength)
				.IsRequired();

			entity.Property(e => e.Reason)
				.HasMaxLength(EntityLimits.RevokedJwtReasonMaxLength)
				.IsRequired();

			// --- 6. Collection navigation properties (none) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, and indexes for <see cref="RoleEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureRole(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<RoleEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("Roles");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier ---

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_Roles_PublicId");

			// --- 3. Foreign keys + Navigation properties (none) ---

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.Name)
				.HasMaxLength(EntityLimits.RoleNameMaxLength)
				.IsRequired();

			entity.HasIndex(e => e.Name)
				.IsUnique()
				.HasDatabaseName("IX_Roles_Name");

			entity.Property(e => e.Description)
				.HasMaxLength(EntityLimits.RoleDescriptionMaxLength);

			// --- 6. Collection navigation properties (configured from the owning side:
			//        UserRoles) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, and indexes for <see cref="SeedHistoryEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureSeedHistory(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<SeedHistoryEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("SeedHistory");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier (none) ---

			// --- 3. Foreign keys + Navigation properties (none) ---

			// --- 4. Timestamps ---

			entity.Property(e => e.AppliedAtUtc)
				.IsRequired();

			// For chronological seed queries and auditing
			entity.HasIndex(e => e.AppliedAtUtc)
				.HasDatabaseName("IX_SeedHistory_AppliedAtUtc");

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.SeedId)
				.HasMaxLength(EntityLimits.SeedIdMaxLength)
				.IsRequired();

			// Unique constraint to prevent duplicate seed entries
			entity.HasIndex(e => e.SeedId)
				.IsUnique()
				.HasDatabaseName("IX_SeedHistory_SeedId");

			entity.Property(e => e.Version)
				.IsRequired();

			entity.Property(e => e.Description)
				.HasMaxLength(EntityLimits.SeedHistoryDescriptionMaxLength)
				.IsRequired();

			// --- 6. Collection navigation properties (none) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships for <see cref="SystemPromptEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureSystemPrompt(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<SystemPromptEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("SystemPrompts");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier ---

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_SystemPrompts_PublicId");

			// --- 3. Foreign keys + Navigation properties ---

			entity.Property(e => e.PersonaId)
				.IsRequired();

			entity.HasOne(e => e.Persona)
				.WithMany(p => p.SystemPrompts)
				.HasForeignKey(e => e.PersonaId)
				.OnDelete(DeleteBehavior.Cascade);

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.Content)
				.IsRequired();

			entity.Property(e => e.Hash)
				.HasMaxLength(EntityLimits.Sha256HexLength)
				.IsRequired();

			// Composite (PersonaId, Hash) is unique: a persona cannot have two prompts with the
			// same content. The leading PersonaId column also covers "all prompts of persona X"
			// queries, so no standalone PersonaId index is needed.
			entity.HasIndex(e => new { e.PersonaId, e.Hash })
				.IsUnique()
				.HasDatabaseName("IX_SystemPrompts_PersonaId_Hash");

			// --- 6. Collection navigation properties (configured from the owning side:
			//        GenerationMetadata) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships for <see cref="UserEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	/// <param name="databaseProviderName">The current EF Core provider name.</param>
	private static void ConfigureUser(ModelBuilder modelBuilder, string? databaseProviderName)
	{
		modelBuilder.Entity<UserEntity>(entity =>
		{
			// --- 1. Table mapping & primary key ---

			entity.ToTable("Users");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			// --- 2. Public identifier (none) ---

			// --- 3. Foreign keys + Navigation properties ---

			entity.Property(e => e.ParticipantId)
				.IsRequired();

			entity.HasOne(e => e.Participant)
				.WithOne(p => p.User)
				.HasForeignKey<UserEntity>(e => e.ParticipantId)
				.OnDelete(DeleteBehavior.Restrict); // Preserve participants for conversation history

			entity.HasIndex(e => e.ParticipantId)
				.IsUnique()
				.HasDatabaseName("IX_Users_ParticipantId");

			// 1:1 to UserPreferences is owned/configured from the UserPreferences side
			// (see ConfigureUserPreferences). The reverse navigation is grouped here in the entity layout
			// because UserEntity.Preferences is a single-entity reverse navigation.

			// --- 4. Timestamps ---

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			entity.Property(e => e.LastLoginAtUtc);

			entity.Property(e => e.LastTokenRefreshAtUtc);

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.Username)
				.HasMaxLength(EntityLimits.UsernameMaxLength)
				.IsRequired();

			entity.HasIndex(e => e.Username)
				.HasDatabaseName("IX_Users_Username");

			entity.Property(e => e.UsernameNormalized)
				.HasMaxLength(EntityLimits.UsernameMaxLength)
				.IsRequired();

			entity.HasIndex(e => e.UsernameNormalized)
				.IsUnique()
				.HasDatabaseName("IX_Users_UsernameNormalized");

			entity.Property(e => e.PasswordHash)
				.HasMaxLength(EntityLimits.PasswordHashMaxLength)
				.IsRequired();

			entity.Property(e => e.Email)
				.HasMaxLength(EntityLimits.EmailMaxLength);

			// Keep the nullable-email uniqueness contract provider-safe: multiple missing emails are allowed,
			// but real email values must remain unique. The filter SQL is provider-specific, so we delegate
			// the lookup — unknown providers throw NotSupportedException loudly rather than silently dropping
			// the filter (which would weaken the uniqueness guarantee).
			entity.HasIndex(e => e.Email)
				.IsUnique()
				.HasDatabaseName("IX_Users_Email")
				.HasFilter(GetUniqueEmailIndexFilter(databaseProviderName));

			// --- 6. Collection navigation properties (configured from the owning side:
			//        UserRoles) ---
		});
	}

	/// <summary>
	/// Gets the provider-specific SQL filter used for the nullable unique <c>Users.Email</c> index.
	/// </summary>
	/// <param name="databaseProviderName">The current EF Core provider name.</param>
	/// <returns>The raw SQL filter expression for the supported providers.</returns>
	/// <exception cref="NotSupportedException">
	/// <paramref name="databaseProviderName"/> is not one of the supported providers (SQLite, PostgreSQL,
	/// SQL Server). Falling back to <see langword="null"/> would silently weaken the nullable-email
	/// uniqueness contract on unknown providers, so the misuse is reported loudly instead.
	/// </exception>
	/// <remarks>
	/// SQL Server requires a filtered unique index to allow multiple <see langword="null"/> values, while SQLite and
	/// PostgreSQL also support filtered unique indexes but require ANSI-style identifier quoting instead of SQL Server's
	/// bracket syntax.
	/// </remarks>
	internal static string GetUniqueEmailIndexFilter(string? databaseProviderName) => databaseProviderName switch
	{
		"Microsoft.EntityFrameworkCore.SqlServer" => "[Email] IS NOT NULL",
		"Microsoft.EntityFrameworkCore.Sqlite" or "Npgsql.EntityFrameworkCore.PostgreSQL" =>
			"\"Email\" IS NOT NULL",
		var _ => throw new NotSupportedException(
			         $"EF Core provider '{databaseProviderName ?? "<null>"}' is not supported by " +
			         $"{nameof(LumaCoreDbContext)}. Supported providers: SQLite, PostgreSQL, SQL Server.")
	};

	/// <summary>
	/// Configures table mapping, property constraints, and relationships
	/// for <see cref="UserPreferencesEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureUserPreferences(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<UserPreferencesEntity>(entity =>
		{
			// --- 1. Table mapping & primary key (also foreign key to UserEntity) ---

			entity.ToTable("UserPreferences");

			entity.HasKey(e => e.UserId);

			entity.HasOne(e => e.User)
				.WithOne(u => u.Preferences)
				.HasForeignKey<UserPreferencesEntity>(e => e.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			// --- 2. Public identifier (none) ---

			// --- 3. Foreign keys + Navigation properties (none) ---

			// --- 4. Timestamps (none) ---

			// --- 5. Scalar domain fields ---

			entity.Property(e => e.PreferencesJson)
				.HasMaxLength(EntityLimits.UserPreferencesJsonMaxLength);

			// --- 6. Collection navigation properties (none) ---
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, and relationships for <see cref="UserRoleEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureUserRole(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<UserRoleEntity>(entity =>
		{
			// --- 1. Table mapping & composite key ---

			// Composite primary key — the two FK columns combined; the per-FK configuration
			// (HasOne / OnDelete) lives in the dedicated FK + Navigation sections below.
			entity.ToTable("UserRoles");

			entity.HasKey(e => new { e.UserId, e.RoleId });

			// --- 2. First FK + Navigation ---

			entity.HasOne(e => e.User)
				.WithMany(u => u.UserRoles)
				.HasForeignKey(e => e.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			// --- 3. Second FK + Navigation ---

			entity.HasOne(e => e.Role)
				.WithMany(r => r.UserRoles)
				.HasForeignKey(e => e.RoleId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(e => e.RoleId)
				.HasDatabaseName("IX_UserRoles_RoleId");

			// --- 4. Timestamps ---

			entity.Property(e => e.AssignedAtUtc)
				.IsRequired();

			// --- 5. Other properties (none) ---
		});
	}
}
