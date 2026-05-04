// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace LumaCore.Data.Tests.Entities;

/// <summary>
/// POCO coverage smoke tests for entity classes. These tests ensure that all auto-properties (scalar, reference
/// navigation, and collection navigation) and default initializers are executed by the test suite.
/// </summary>
/// <remarks>
///     <para>
///         <b>What these tests cover:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>Coverage baseline — every getter/setter is exercised so Coverlet reports them.</description>
///         </item>
///         <item>
///             <description>Breakage detection — renaming or removing a property causes a compile error here.</description>
///         </item>
///         <item>
///             <description>
///             Collection initialization — verifies that collection navigation properties are non-
///             <see langword="null"/>.
///             </description>
///         </item>
///         <item>
///             <description>
///             Reference navigation assignability — verifies that reference navigation properties can be set
///             and read back.
///             </description>
///         </item>
///     </list>
///     <para>
///         <b>What these tests do NOT cover:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>EF Core mapping correctness (column types, FK constraints, indexes, delete behaviors).</description>
///         </item>
///         <item>
///             <description>Navigation property loading (<c>Include()</c>, lazy loading, change tracking).</description>
///         </item>
///         <item>
///             <description>Database round-trip behavior (insert → read → compare).</description>
///         </item>
///     </list>
///     <para>
///     Those concerns are validated by integration tests that run against a real database via the data service layer.
///     </para>
///     <para>
///     These tests intentionally do not involve <see cref="DbContext"/>; they are designed to be fast
///     and to validate that entity types can be constructed and assigned as plain CLR objects.
///     </para>
///     <para>
///     <b>Exception to the smoke-test charter:</b> A small number of tests verify <i>default values</i> of
///     properties that are not exercised by the standard <c>CanSetAllProperties</c> shape (e.g.
///     <see cref="UserPreferencesEntity_PreferencesJson_DefaultsToNull"/>). These remain in this file
///     because they target the same POCO surface and would not justify a separate file by themselves.
///     </para>
/// </remarks>
[Trait("Category", "Entities")]
public sealed class EntitySmokeTests
{
	#region ConversationEntity

	/// <summary>
	/// Verifies that <see cref="ConversationEntity"/> can be constructed, all properties assigned, and collection
	/// navigation properties are initialized.
	/// </summary>
	[Fact]
	public void ConversationEntity_CanSetAllProperties()
	{
		// Arrange
		var publicId = Guid.NewGuid();
		var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var updated = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

		// Act
		var sut = new ConversationEntity
		{
			Id = new ConversationId(1),
			PublicId = publicId,
			Title = "Test conversation",
			Description = "A test description",
			CreatedAtUtc = created,
			UpdatedAtUtc = updated
		};

		// Assert
		Assert.Equal(new ConversationId(1), sut.Id);
		Assert.Equal(publicId, sut.PublicId);
		Assert.Equal("Test conversation", sut.Title);
		Assert.Equal("A test description", sut.Description);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal(updated, sut.UpdatedAtUtc);
		Assert.Empty(sut.Messages);
		Assert.Empty(sut.Participants);
	}

	#endregion

	#region ConversationParticipantEntity

	/// <summary>
	/// Verifies that <see cref="ConversationParticipantEntity"/> (a composite-key join entity) can be constructed,
	/// all scalar and reference navigation properties assigned.
	/// </summary>
	[Fact]
	public void ConversationParticipantEntity_CanSetAllProperties()
	{
		// Arrange
		var joined = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var conversation = new ConversationEntity();
		var participant = new ParticipantEntity();
		var lastReadMessage = new MessageEntity();

		// Act
		var sut = new ConversationParticipantEntity
		{
			ConversationId = new ConversationId(1),
			Conversation = conversation,
			ParticipantId = new ParticipantId(2),
			Participant = participant,
			JoinedAtUtc = joined,
			Role = ConversationParticipantRole.Member,
			LastReadMessageId = new MessageId(7),
			LastReadMessage = lastReadMessage
		};

		// Assert
		Assert.Equal(new ConversationId(1), sut.ConversationId);
		Assert.Same(conversation, sut.Conversation);
		Assert.Equal(new ParticipantId(2), sut.ParticipantId);
		Assert.Same(participant, sut.Participant);
		Assert.Equal(joined, sut.JoinedAtUtc);
		Assert.Equal(ConversationParticipantRole.Member, sut.Role);
		Assert.Equal(new MessageId(7), sut.LastReadMessageId);
		Assert.Same(lastReadMessage, sut.LastReadMessage);
	}

	#endregion

	#region MessageEntity

	/// <summary>
	/// Verifies that <see cref="MessageEntity"/> can be constructed, all scalar and reference navigation properties
	/// assigned.
	/// </summary>
	[Fact]
	public void MessageEntity_CanSetAllProperties()
	{
		// Arrange
		var publicId = Guid.NewGuid();
		var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var redacted = new DateTime(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc);
		var conversation = new ConversationEntity();
		var sender = new ParticipantEntity();
		var metadata = new MessageGenerationMetadataEntity();

		// Act
		var sut = new MessageEntity
		{
			Id = new MessageId(1),
			PublicId = publicId,
			ConversationId = new ConversationId(2),
			Conversation = conversation,
			SenderId = new ParticipantId(3),
			Sender = sender,
			GenerationMetadata = metadata,
			Type = MessageType.System,
			Content = "Hello",
			CreatedAtUtc = created,
			RedactedAtUtc = redacted,
			RedactionReason = MessageRedactionReason.Moderation
		};

		// Assert
		Assert.Equal(new MessageId(1), sut.Id);
		Assert.Equal(publicId, sut.PublicId);
		Assert.Equal(new ConversationId(2), sut.ConversationId);
		Assert.Same(conversation, sut.Conversation);
		Assert.Equal(new ParticipantId(3), sut.SenderId);
		Assert.Same(sender, sut.Sender);
		Assert.Same(metadata, sut.GenerationMetadata);
		Assert.Equal(MessageType.System, sut.Type);
		Assert.Equal("Hello", sut.Content);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal(redacted, sut.RedactedAtUtc);
		Assert.Equal(MessageRedactionReason.Moderation, sut.RedactionReason);
	}

	#endregion

	#region MessageGenerationMetadataEntity

	/// <summary>
	/// Verifies that <see cref="MessageGenerationMetadataEntity"/> can be constructed, all scalar and reference
	/// navigation properties assigned.
	/// </summary>
	[Fact]
	public void MessageGenerationMetadataEntity_CanSetAllProperties()
	{
		// Arrange
		TimeSpan responseTime = TimeSpan.FromSeconds(1.5);
		var message = new MessageEntity();
		var modelEndpoint = new ModelEndpointEntity();
		var systemPrompt = new SystemPromptEntity();

		// Act
		var sut = new MessageGenerationMetadataEntity
		{
			MessageId = new MessageId(1),
			Message = message,
			ModelEndpointId = new ModelEndpointId(2),
			ModelEndpoint = modelEndpoint,
			SystemPromptId = new SystemPromptId(3),
			SystemPrompt = systemPrompt,
			Model = "gpt-test",
			FullPrompt = "system: be helpful",
			PromptTokens = 10,
			CompletionTokens = 20,
			ResponseTime = responseTime,
			MaxTokens = 4096,
			Temperature = 0.7,
			TopP = 0.9
		};

		// Assert
		Assert.Equal(new MessageId(1), sut.MessageId);
		Assert.Same(message, sut.Message);
		Assert.Equal(new ModelEndpointId(2), sut.ModelEndpointId);
		Assert.Same(modelEndpoint, sut.ModelEndpoint);
		Assert.Equal(new SystemPromptId(3), sut.SystemPromptId);
		Assert.Same(systemPrompt, sut.SystemPrompt);
		Assert.Equal("gpt-test", sut.Model);
		Assert.Equal("system: be helpful", sut.FullPrompt);
		Assert.Equal(10, sut.PromptTokens);
		Assert.Equal(20, sut.CompletionTokens);
		Assert.Equal(responseTime, sut.ResponseTime);
		Assert.Equal(4096, sut.MaxTokens);
		Assert.Equal(0.7, sut.Temperature);
		Assert.Equal(0.9, sut.TopP);
	}

	#endregion

	#region ModelEndpointEntity

	/// <summary>
	/// Verifies that <see cref="ModelEndpointEntity"/> can be constructed, all properties assigned, and collection
	/// navigation properties are initialized.
	/// </summary>
	[Fact]
	public void ModelEndpointEntity_CanSetAllProperties()
	{
		// Arrange
		var publicId = Guid.NewGuid();
		var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var updated = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

		// Act
		var sut = new ModelEndpointEntity
		{
			Id = new ModelEndpointId(1),
			PublicId = publicId,
			CreatedAtUtc = created,
			UpdatedAtUtc = updated,
			ProviderType = "ollama",
			BaseUrl = "http://localhost:11434",
			Name = "Local Ollama",
			Description = "Dev instance",
			IsActive = true,
			EncryptedCredentials = "enc-cred"
		};

		// Assert
		Assert.Equal(new ModelEndpointId(1), sut.Id);
		Assert.Equal(publicId, sut.PublicId);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal(updated, sut.UpdatedAtUtc);
		Assert.Equal("ollama", sut.ProviderType);
		Assert.Equal("http://localhost:11434", sut.BaseUrl);
		Assert.Equal("Local Ollama", sut.Name);
		Assert.Equal("Dev instance", sut.Description);
		Assert.True(sut.IsActive);
		Assert.Equal("enc-cred", sut.EncryptedCredentials);
		Assert.Empty(sut.GenerationMetadata);
	}

	#endregion

	#region ParticipantEntity

	/// <summary>
	/// Verifies that <see cref="ParticipantEntity"/> can be constructed, all scalar, reference navigation, and
	/// collection navigation properties assigned.
	/// </summary>
	[Fact]
	public void ParticipantEntity_CanSetAllProperties()
	{
		// Arrange
		var publicId = Guid.NewGuid();
		var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var user = new UserEntity();
		var persona = new PersonaEntity();

		// Act
		var sut = new ParticipantEntity
		{
			Id = new ParticipantId(1),
			PublicId = publicId,
			CreatedAtUtc = created,
			DisplayName = "Alice",
			User = user,
			Persona = persona
		};

		// Assert
		Assert.Equal(new ParticipantId(1), sut.Id);
		Assert.Equal(publicId, sut.PublicId);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal("Alice", sut.DisplayName);
		Assert.Same(user, sut.User);
		Assert.Same(persona, sut.Persona);
		Assert.Empty(sut.ConversationParticipants);
		Assert.Empty(sut.Messages);
	}

	#endregion

	#region PersonaEntity

	/// <summary>
	/// Verifies that <see cref="PersonaEntity"/> can be constructed, all scalar, reference navigation, and collection
	/// navigation properties assigned.
	/// </summary>
	[Fact]
	public void PersonaEntity_CanSetAllProperties()
	{
		// Arrange
		var updated = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
		var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var participant = new ParticipantEntity();
		var createdBy = new ParticipantEntity();
		var activePrompt = new SystemPromptEntity();

		// Act
		var sut = new PersonaEntity
		{
			Id = new PersonaId(1),
			ParticipantId = new ParticipantId(2),
			Participant = participant,
			CreatedByParticipantId = new ParticipantId(4),
			CreatedByParticipant = createdBy,
			ActiveSystemPromptId = new SystemPromptId(3),
			ActiveSystemPrompt = activePrompt,
			CreatedAtUtc = created,
			UpdatedAtUtc = updated,
			DefaultModel = "gpt-test",
			IsActive = true,
			Visibility = PersonaVisibility.Shared
		};

		// Assert
		Assert.Equal(new PersonaId(1), sut.Id);
		Assert.Equal(new ParticipantId(2), sut.ParticipantId);
		Assert.Same(participant, sut.Participant);
		Assert.Equal(new ParticipantId(4), sut.CreatedByParticipantId);
		Assert.Same(createdBy, sut.CreatedByParticipant);
		Assert.Equal(new SystemPromptId(3), sut.ActiveSystemPromptId);
		Assert.Same(activePrompt, sut.ActiveSystemPrompt);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal(updated, sut.UpdatedAtUtc);
		Assert.Equal("gpt-test", sut.DefaultModel);
		Assert.True(sut.IsActive);
		Assert.Equal(PersonaVisibility.Shared, sut.Visibility);
		Assert.Empty(sut.SystemPrompts);
		Assert.Empty(sut.DescriptionTranslations);
	}

	#endregion

	#region ResourceEntity

	/// <summary>
	/// Verifies that <see cref="ResourceEntity"/> can be constructed, all scalar, reference navigation, and
	/// collection navigation properties assigned.
	/// </summary>
	[Fact]
	public void ResourceEntity_CanSetAllProperties()
	{
		// Arrange
		var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var createdBy = new ParticipantEntity();

		// Act
		var sut = new ResourceEntity
		{
			Id = new ResourceId(1),
			CreatedByParticipantId = new ParticipantId(2),
			CreatedByParticipant = createdBy,
			CreatedAtUtc = created,
			ContentHash = "abc123",
			StoragePath = "ab/abc123-def456",
			SizeBytes = 1024,
			DeletionState = ResourceDeletionState.Active
		};

		// Assert
		Assert.Equal(new ResourceId(1), sut.Id);
		Assert.Equal(new ParticipantId(2), sut.CreatedByParticipantId);
		Assert.Same(createdBy, sut.CreatedByParticipant);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal("abc123", sut.ContentHash);
		Assert.Equal("ab/abc123-def456", sut.StoragePath);
		Assert.Equal(1024, sut.SizeBytes);
		Assert.Equal(ResourceDeletionState.Active, sut.DeletionState);
		Assert.Empty(sut.References);
	}

	#endregion

	#region ResourceGcStateEntity

	/// <summary>
	/// Verifies that <see cref="ResourceGcStateEntity"/> (a singleton state row) can be constructed and all
	/// properties assigned.
	/// </summary>
	[Fact]
	public void ResourceGcStateEntity_CanSetAllProperties()
	{
		// Arrange
		var lastRun = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		// Act
		var sut = new ResourceGcStateEntity
		{
			Id = 1,
			LastRunAtUtc = lastRun
		};

		// Assert
		Assert.Equal(1, sut.Id);
		Assert.Equal(lastRun, sut.LastRunAtUtc);
	}

	#endregion

	#region ResourceReferenceEntity

	/// <summary>
	/// Verifies that <see cref="ResourceReferenceEntity"/> can be constructed, all scalar and reference
	/// navigation properties assigned.
	/// </summary>
	[Fact]
	public void ResourceReferenceEntity_CanSetAllProperties()
	{
		// Arrange
		var publicId = Guid.NewGuid();
		var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var resource = new ResourceEntity();

		// Act
		var sut = new ResourceReferenceEntity
		{
			Id = new ResourceReferenceId(1),
			PublicId = publicId,
			ResourceId = new ResourceId(2),
			Resource = resource,
			CreatedAtUtc = created,
			OwnerKind = ResourceOwnerKind.Message,
			OwnerId = new ResourceOwnerId(42),
			ContentType = "image/png",
			OriginalFileName = "avatar.png"
		};

		// Assert
		Assert.Equal(new ResourceReferenceId(1), sut.Id);
		Assert.Equal(publicId, sut.PublicId);
		Assert.Equal(new ResourceId(2), sut.ResourceId);
		Assert.Same(resource, sut.Resource);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal(ResourceOwnerKind.Message, sut.OwnerKind);
		Assert.Equal(new ResourceOwnerId(42), sut.OwnerId);
		Assert.Equal("image/png", sut.ContentType);
		Assert.Equal("avatar.png", sut.OriginalFileName);
	}

	#endregion

	#region RevokedJwtEntity

	/// <summary>
	/// Verifies that <see cref="RevokedJwtEntity"/> can be constructed and all properties assigned.
	/// </summary>
	[Fact]
	public void RevokedJwtEntity_CanSetAllProperties()
	{
		// Arrange
		var expires = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
		var revoked = new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc);

		// Act
		var sut = new RevokedJwtEntity
		{
			Jti = "test-jti-001",
			ExpiresAtUtc = expires,
			RevokedAtUtc = revoked,
			Subject = "alice",
			Reason = "Logout"
		};

		// Assert
		Assert.Equal("test-jti-001", sut.Jti);
		Assert.Equal(expires, sut.ExpiresAtUtc);
		Assert.Equal(revoked, sut.RevokedAtUtc);
		Assert.Equal("alice", sut.Subject);
		Assert.Equal("Logout", sut.Reason);
	}

	#endregion

	#region RoleEntity

	/// <summary>
	/// Verifies that <see cref="RoleEntity"/> can be constructed, all properties assigned, and collection
	/// navigation properties are initialized.
	/// </summary>
	[Fact]
	public void RoleEntity_CanSetAllProperties()
	{
		// Arrange
		var publicId = Guid.NewGuid();
		var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		// Act
		var sut = new RoleEntity
		{
			Id = new RoleId(1),
			PublicId = publicId,
			CreatedAtUtc = created,
			Name = "admin",
			Description = "Full system access"
		};

		// Assert
		Assert.Equal(new RoleId(1), sut.Id);
		Assert.Equal(publicId, sut.PublicId);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal("admin", sut.Name);
		Assert.Equal("Full system access", sut.Description);
		Assert.Empty(sut.UserRoles);
	}

	#endregion

	#region SeedHistoryEntity

	/// <summary>
	/// Verifies that <see cref="SeedHistoryEntity"/> can be constructed and all properties assigned.
	/// </summary>
	[Fact]
	public void SeedHistoryEntity_CanSetAllProperties()
	{
		// Arrange
		var applied = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		// Act
		var sut = new SeedHistoryEntity
		{
			Id = 1,
			SeedId = "seed-roles",
			Version = 2,
			Description = "Initial roles",
			AppliedAtUtc = applied
		};

		// Assert
		Assert.Equal(1, sut.Id);
		Assert.Equal("seed-roles", sut.SeedId);
		Assert.Equal(2, sut.Version);
		Assert.Equal("Initial roles", sut.Description);
		Assert.Equal(applied, sut.AppliedAtUtc);
	}

	#endregion

	#region SystemPromptEntity

	/// <summary>
	/// Verifies that <see cref="SystemPromptEntity"/> can be constructed, all scalar, reference navigation, and
	/// collection navigation properties assigned.
	/// </summary>
	[Fact]
	public void SystemPromptEntity_CanSetAllProperties()
	{
		// Arrange
		var publicId = Guid.NewGuid();
		var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var persona = new PersonaEntity();

		// Act
		var sut = new SystemPromptEntity
		{
			Id = new SystemPromptId(1),
			PublicId = publicId,
			PersonaId = new PersonaId(2),
			Persona = persona,
			CreatedAtUtc = created,
			Content = "Be helpful and concise.",
			Hash = "abc123"
		};

		// Assert
		Assert.Equal(new SystemPromptId(1), sut.Id);
		Assert.Equal(publicId, sut.PublicId);
		Assert.Equal(new PersonaId(2), sut.PersonaId);
		Assert.Same(persona, sut.Persona);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal("Be helpful and concise.", sut.Content);
		Assert.Equal("abc123", sut.Hash);
		Assert.Empty(sut.GenerationMetadata);
	}

	#endregion

	#region UserEntity

	/// <summary>
	/// Verifies that <see cref="UserEntity"/> can be constructed, all scalar, reference navigation, and collection
	/// navigation properties assigned.
	/// </summary>
	[Fact]
	public void UserEntity_CanSetAllProperties()
	{
		// Arrange
		var created = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
		var lastLogin = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var lastRefresh = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
		var participant = new ParticipantEntity();
		var preferences = new UserPreferencesEntity();

		// Act
		var sut = new UserEntity
		{
			Id = new UserId(1),
			ParticipantId = new ParticipantId(2),
			Participant = participant,
			CreatedAtUtc = created,
			Username = "alice",
			UsernameNormalized = "ALICE",
			PasswordHash = "hash",
			Email = "alice@example.test",
			LastLoginAtUtc = lastLogin,
			LastTokenRefreshAtUtc = lastRefresh,
			Preferences = preferences
		};

		// Assert
		Assert.Equal(new UserId(1), sut.Id);
		Assert.Equal(new ParticipantId(2), sut.ParticipantId);
		Assert.Same(participant, sut.Participant);
		Assert.Equal("alice", sut.Username);
		Assert.Equal("ALICE", sut.UsernameNormalized);
		Assert.Equal("alice@example.test", sut.Email);
		Assert.Equal("hash", sut.PasswordHash);
		Assert.Equal(created, sut.CreatedAtUtc);
		Assert.Equal(lastLogin, sut.LastLoginAtUtc);
		Assert.Equal(lastRefresh, sut.LastTokenRefreshAtUtc);
		Assert.Same(preferences, sut.Preferences);
		Assert.Empty(sut.UserRoles);
	}

	#endregion

	#region UserPreferencesEntity

	/// <summary>
	/// Verifies that <see cref="UserPreferencesEntity"/> (a 1:1 extension entity) can be constructed, all scalar
	/// and reference navigation properties assigned.
	/// </summary>
	[Fact]
	public void UserPreferencesEntity_CanSetAllProperties()
	{
		// Arrange
		var user = new UserEntity();

		// Act
		var sut = new UserPreferencesEntity
		{
			UserId = new UserId(1),
			User = user,
			PreferencesJson = "{\"recentEmojis\":[\"😀\",\"🎉\"]}"
		};

		// Assert
		Assert.Equal(new UserId(1), sut.UserId);
		Assert.Same(user, sut.User);
		Assert.Equal("{\"recentEmojis\":[\"😀\",\"🎉\"]}", sut.PreferencesJson);
	}

	/// <summary>
	/// Verifies that <see cref="UserPreferencesEntity.PreferencesJson"/> defaults to <see langword="null"/>
	/// when no value is assigned. This is the only behavior-style assertion in this file (see the class-level
	/// note about the "Defaults" exception to the smoke-test charter).
	/// </summary>
	[Fact]
	public void UserPreferencesEntity_PreferencesJson_DefaultsToNull()
	{
		// Act
		var sut = new UserPreferencesEntity();

		// Assert
		Assert.Null(sut.PreferencesJson);
	}

	#endregion

	#region UserRoleEntity

	/// <summary>
	/// Verifies that <see cref="UserRoleEntity"/> (a composite-key join entity) can be constructed, all scalar and
	/// reference navigation properties assigned.
	/// </summary>
	[Fact]
	public void UserRoleEntity_CanSetAllProperties()
	{
		// Arrange
		var assigned = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var user = new UserEntity();
		var role = new RoleEntity();

		// Act
		var sut = new UserRoleEntity
		{
			UserId = new UserId(1),
			User = user,
			RoleId = new RoleId(2),
			Role = role,
			AssignedAtUtc = assigned
		};

		// Assert
		Assert.Equal(new UserId(1), sut.UserId);
		Assert.Same(user, sut.User);
		Assert.Equal(new RoleId(2), sut.RoleId);
		Assert.Same(role, sut.Role);
		Assert.Equal(assigned, sut.AssignedAtUtc);
	}

	#endregion
}
