// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;

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
///     Supports multiple database providers: SQLite, PostgreSQL, MySQL, and SQL Server.
///     The provider is configured via <see cref="DatabaseOptions"/>.
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
public sealed class LumaCoreDbContext : DbContext
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
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for user-role assignments.
	/// </summary>
	public DbSet<UserRoleEntity> UserRoles { get; set; } = null!;

	/// <summary>
	/// Gets or sets the <see cref="DbSet{TEntity}"/> for users.
	/// </summary>
	public DbSet<UserEntity> Users { get; set; } = null!;

	/// <summary>
	/// Registers model-wide value converters: UTC normalization for all <see cref="DateTime"/> properties
	/// and strongly-typed entity identifier mappings (<c>XxxId</c> ↔ <see cref="long"/>).
	/// </summary>
	/// <param name="configurationBuilder">The conventions builder.</param>
	/// <remarks>
	/// The <see cref="UtcDateTimeConverter"/> stamps <see cref="DateTimeKind.Utc"/> on every
	/// <see cref="DateTime"/> value entering or leaving EF Core. This prevents Npgsql from rejecting
	/// <see cref="DateTimeKind.Unspecified"/> values for <c>timestamp with time zone</c> columns and
	/// keeps the model provider-agnostic.
	/// </remarks>
	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		base.ConfigureConventions(configurationBuilder);

		// Normalize all DateTime properties to DateTimeKind.Utc (defense-in-depth for multi-provider).
		configurationBuilder
			.Properties<DateTime>()
			.HaveConversion<UtcDateTimeConverter>();

		configurationBuilder
			.Properties<ConversationId>()
			.HaveConversion<ConversationIdConverter>();

		configurationBuilder
			.Properties<ParticipantId>()
			.HaveConversion<ParticipantIdConverter>();

		configurationBuilder
			.Properties<UserId>()
			.HaveConversion<UserIdConverter>();

		configurationBuilder
			.Properties<MessageId>()
			.HaveConversion<MessageIdConverter>();

		configurationBuilder
			.Properties<RoleId>()
			.HaveConversion<RoleIdConverter>();

		configurationBuilder
			.Properties<ModelEndpointId>()
			.HaveConversion<ModelEndpointIdConverter>();

		configurationBuilder
			.Properties<PersonaId>()
			.HaveConversion<PersonaIdConverter>();

		configurationBuilder
			.Properties<SystemPromptId>()
			.HaveConversion<SystemPromptIdConverter>();
	}

	/// <summary>
	/// Configures the database schema, relationships, and constraints.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model for this context.</param>
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		ConfigureParticipant(modelBuilder);
		ConfigureUser(modelBuilder);
		ConfigurePersona(modelBuilder);
		ConfigureRole(modelBuilder);
		ConfigureUserRole(modelBuilder);
		ConfigureConversation(modelBuilder);
		ConfigureConversationParticipant(modelBuilder);
		ConfigureMessage(modelBuilder);
		ConfigureSystemPrompt(modelBuilder);
		ConfigureModelEndpoint(modelBuilder);
		ConfigureMessageGenerationMetadata(modelBuilder);
		ConfigureSeedHistory(modelBuilder);
	}

	/// <summary>
	/// Configures table mapping, property constraints, and indexes for <see cref="ConversationEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureConversation(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ConversationEntity>(entity =>
		{
			entity.ToTable("Conversations");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.Property(e => e.Title)
				.HasMaxLength(EntityLimits.ConversationTitleMaxLength)
				.IsRequired();

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			entity.Property(e => e.UpdatedAtUtc)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_Conversations_PublicId");

			entity.HasIndex(e => e.UpdatedAtUtc)
				.HasDatabaseName("IX_Conversations_UpdatedAtUtc");
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
			entity.ToTable("ConversationParticipants");

			entity.HasKey(e => new { e.ConversationId, e.ParticipantId });

			entity.Property(e => e.Role)
				.IsRequired();

			entity.Property(e => e.JoinedAtUtc)
				.IsRequired();

			entity.HasOne(e => e.Conversation)
				.WithMany(c => c.Participants)
				.HasForeignKey(e => e.ConversationId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.Participant)
				.WithMany(p => p.ConversationParticipants)
				.HasForeignKey(e => e.ParticipantId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(e => e.ParticipantId)
				.HasDatabaseName("IX_ConversationParticipants_ParticipantId");
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
			entity.ToTable("Messages");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.Property(e => e.ConversationId)
				.IsRequired();

			entity.Property(e => e.SenderId);

			entity.Property(e => e.Content);

			entity.Property(e => e.RedactedAtUtc);

			entity.Property(e => e.RedactionReason);

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_Messages_PublicId");

			entity.HasIndex(e => e.ConversationId)
				.HasDatabaseName("IX_Messages_ConversationId");

			entity.HasIndex(e => e.SenderId)
				.HasDatabaseName("IX_Messages_SenderId");

			entity.HasIndex(e => e.CreatedAtUtc)
				.HasDatabaseName("IX_Messages_CreatedAtUtc");

			entity.HasIndex(e => new { e.ConversationId, e.CreatedAtUtc })
				.HasDatabaseName("IX_Messages_ConversationId_CreatedAtUtc");

			entity.HasOne(e => e.Conversation)
				.WithMany(c => c.Messages)
				.HasForeignKey(e => e.ConversationId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.Sender)
				.WithMany(p => p.Messages)
				.HasForeignKey(e => e.SenderId)
				.OnDelete(DeleteBehavior.SetNull); // Preserve history; messages can outlive a deleted participant
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
			entity.ToTable("MessageGenerationMetadata");

			entity.HasKey(e => e.MessageId);

			entity.Property(e => e.Model)
				.HasMaxLength(EntityLimits.ModelIdentifierMaxLength)
				.IsRequired();

			entity.Property(e => e.PromptTokens)
				.IsRequired();

			entity.Property(e => e.CompletionTokens)
				.IsRequired();

			entity.Property(e => e.ResponseTime)
				.IsRequired();

			entity.Property(e => e.ModelEndpointId)
				.IsRequired();

			entity.HasOne(e => e.Message)
				.WithOne(m => m.GenerationMetadata)
				.HasForeignKey<MessageGenerationMetadataEntity>(e => e.MessageId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.SystemPrompt)
				.WithMany(sp => sp.GenerationMetadata)
				.HasForeignKey(e => e.SystemPromptId)
				.OnDelete(DeleteBehavior.SetNull);

			entity.HasIndex(e => e.SystemPromptId)
				.HasDatabaseName("IX_MessageGenerationMetadata_SystemPromptId");

			entity.HasIndex(e => e.ModelEndpointId)
				.HasDatabaseName("IX_MessageGenerationMetadata_ModelEndpointId");

			entity.HasOne(e => e.ModelEndpoint)
				.WithMany(me => me.GenerationMetadata)
				.HasForeignKey(e => e.ModelEndpointId)
				.OnDelete(DeleteBehavior.Restrict);
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
			entity.ToTable("ModelEndpoints");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

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

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_ModelEndpoints_PublicId");

			entity.HasIndex(e => e.IsActive)
				.HasDatabaseName("IX_ModelEndpoints_IsActive");
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
			entity.ToTable("Participants");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.Property(e => e.DisplayName)
				.HasMaxLength(EntityLimits.DisplayNameMaxLength)
				.IsRequired();

			entity.Property(e => e.AvatarUrl)
				.HasMaxLength(EntityLimits.AvatarUrlMaxLength);

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_Participants_PublicId");
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
			entity.ToTable("Personas");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			entity.Property(e => e.ParticipantId)
				.IsRequired();

			entity.Property(e => e.ActiveSystemPromptId);

			entity.Property(e => e.DefaultModel)
				.HasMaxLength(EntityLimits.ModelIdentifierMaxLength);

			entity.Property(e => e.Description)
				.HasMaxLength(EntityLimits.PersonaDescriptionMaxLength);

			entity.Property(e => e.IsActive)
				.IsRequired()
				.HasDefaultValue(true)
				.HasSentinel(true); // see ConfigureModelEndpoint() for detailed explanation

			entity.HasIndex(e => e.ParticipantId)
				.IsUnique()
				.HasDatabaseName("IX_Personas_ParticipantId");

			entity.HasIndex(e => e.IsActive)
				.HasDatabaseName("IX_Personas_IsActive");

			entity.HasIndex(e => e.ActiveSystemPromptId)
				.HasDatabaseName("IX_Personas_ActiveSystemPromptId");

			entity.HasOne(e => e.Participant)
				.WithOne(p => p.Persona)
				.HasForeignKey<PersonaEntity>(e => e.ParticipantId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.ActiveSystemPrompt)
				.WithMany()
				.HasForeignKey(e => e.ActiveSystemPromptId)
				.OnDelete(DeleteBehavior.Restrict);
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
			entity.ToTable("Roles");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.Property(e => e.Name)
				.HasMaxLength(EntityLimits.RoleNameMaxLength)
				.IsRequired();

			entity.Property(e => e.Description)
				.HasMaxLength(EntityLimits.RoleDescriptionMaxLength);

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_Roles_PublicId");

			entity.HasIndex(e => e.Name)
				.IsUnique()
				.HasDatabaseName("IX_Roles_Name");
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
			entity.ToTable("SeedHistory");
			entity.HasKey(e => e.Id);

			entity.Property(e => e.SeedId)
				.HasMaxLength(EntityLimits.SeedIdMaxLength)
				.IsRequired();

			entity.Property(e => e.Version)
				.IsRequired();

			entity.Property(e => e.Description)
				.HasMaxLength(EntityLimits.SeedHistoryDescriptionMaxLength);

			entity.Property(e => e.AppliedAtUtc)
				.IsRequired();

			// Unique constraint to prevent duplicate seed entries
			entity.HasIndex(e => e.SeedId)
				.IsUnique()
				.HasDatabaseName("IX_SeedHistory_SeedId");

			// For chronological seed queries and auditing
			entity.HasIndex(e => e.AppliedAtUtc)
				.HasDatabaseName("IX_SeedHistory_AppliedAtUtc");
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
			entity.ToTable("SystemPrompts");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			entity.Property(e => e.PublicId)
				.IsRequired();

			entity.Property(e => e.PersonaId)
				.IsRequired();

			entity.Property(e => e.Content)
				.IsRequired();

			entity.Property(e => e.Hash)
				.HasMaxLength(EntityLimits.Sha256HexLength)
				.IsRequired();

			entity.Property(e => e.CreatedAtUtc)
				.IsRequired();

			entity.HasIndex(e => e.PublicId)
				.IsUnique()
				.HasDatabaseName("IX_SystemPrompts_PublicId");

			entity.HasIndex(e => new { e.PersonaId, e.Hash })
				.IsUnique()
				.HasDatabaseName("IX_SystemPrompts_PersonaId_Hash");

			entity.HasOne(e => e.Persona)
				.WithMany(p => p.SystemPrompts)
				.HasForeignKey(e => e.PersonaId)
				.OnDelete(DeleteBehavior.Cascade);
		});
	}

	/// <summary>
	/// Configures table mapping, property constraints, indexes, and relationships for <see cref="UserEntity"/>.
	/// </summary>
	/// <param name="modelBuilder">The builder used to construct the model.</param>
	private static void ConfigureUser(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<UserEntity>(entity =>
		{
			entity.ToTable("Users");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id)
				.ValueGeneratedOnAdd();

			entity.Property(e => e.ParticipantId)
				.IsRequired();

			entity.Property(e => e.Username)
				.HasMaxLength(EntityLimits.UsernameMaxLength)
				.IsRequired();

			entity.Property(e => e.UsernameNormalized)
				.HasMaxLength(EntityLimits.UsernameMaxLength)
				.IsRequired();

			entity.Property(e => e.Email)
				.HasMaxLength(EntityLimits.EmailMaxLength);

			entity.Property(e => e.PasswordHash)
				.HasMaxLength(EntityLimits.PasswordHashMaxLength)
				.IsRequired();

			entity.HasIndex(e => e.Username)
				.HasDatabaseName("IX_Users_Username");

			entity.HasIndex(e => e.UsernameNormalized)
				.IsUnique()
				.HasDatabaseName("IX_Users_UsernameNormalized");

			entity.HasIndex(e => e.Email)
				.IsUnique()
				.HasDatabaseName("IX_Users_Email");

			entity.HasIndex(e => e.ParticipantId)
				.IsUnique()
				.HasDatabaseName("IX_Users_ParticipantId");

			entity.HasOne(e => e.Participant)
				.WithOne(p => p.User)
				.HasForeignKey<UserEntity>(e => e.ParticipantId)
				.OnDelete(DeleteBehavior.Restrict); // Preserve participants for conversation history
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
			entity.ToTable("UserRoles");

			entity.HasKey(e => new { e.UserId, e.RoleId });

			entity.Property(e => e.AssignedAtUtc)
				.IsRequired();

			entity.HasOne(e => e.User)
				.WithMany(u => u.UserRoles)
				.HasForeignKey(e => e.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.Role)
				.WithMany(r => r.UserRoles)
				.HasForeignKey(e => e.RoleId)
				.OnDelete(DeleteBehavior.Cascade);
		});
	}
}
