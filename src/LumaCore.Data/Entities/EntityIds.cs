// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LumaCore.Data.Entities;

/// <summary>
/// Strongly-typed identifier for a <see cref="ConversationEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct ConversationId(long Value);

/// <summary>
/// Strongly-typed identifier for a <see cref="ParticipantEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct ParticipantId(long Value);

/// <summary>
/// Strongly-typed identifier for a <see cref="UserEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct UserId(long Value);

/// <summary>
/// Strongly-typed identifier for a <see cref="MessageEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct MessageId(long Value);

/// <summary>
/// Strongly-typed identifier for a <see cref="RoleEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct RoleId(long Value);

/// <summary>
/// Strongly-typed identifier for a <see cref="ModelEndpointEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct ModelEndpointId(long Value);

/// <summary>
/// Strongly-typed identifier for a <see cref="PersonaEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct PersonaId(long Value);

/// <summary>
/// Strongly-typed identifier for a <see cref="SystemPromptEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct SystemPromptId(long Value);

// ----- EF Core Value Converters -----
// These converters allow EF Core to transparently persist XxxId ↔ long in the database.
// They are registered globally via ConfigureConventions in LumaCoreDbContext.

/// <summary>EF Core value converter for <see cref="ConversationId"/>.</summary>
sealed class ConversationIdConverter()
	: ValueConverter<ConversationId, long>(id => id.Value, value => new ConversationId(value));

/// <summary>EF Core value converter for <see cref="ParticipantId"/>.</summary>
sealed class ParticipantIdConverter()
	: ValueConverter<ParticipantId, long>(id => id.Value, value => new ParticipantId(value));

/// <summary>EF Core value converter for <see cref="UserId"/>.</summary>
sealed class UserIdConverter()
	: ValueConverter<UserId, long>(id => id.Value, value => new UserId(value));

/// <summary>EF Core value converter for <see cref="MessageId"/>.</summary>
sealed class MessageIdConverter()
	: ValueConverter<MessageId, long>(id => id.Value, value => new MessageId(value));

/// <summary>EF Core value converter for <see cref="RoleId"/>.</summary>
sealed class RoleIdConverter()
	: ValueConverter<RoleId, long>(id => id.Value, value => new RoleId(value));

/// <summary>EF Core value converter for <see cref="ModelEndpointId"/>.</summary>
sealed class ModelEndpointIdConverter()
	: ValueConverter<ModelEndpointId, long>(id => id.Value, value => new ModelEndpointId(value));

/// <summary>EF Core value converter for <see cref="PersonaId"/>.</summary>
sealed class PersonaIdConverter()
	: ValueConverter<PersonaId, long>(id => id.Value, value => new PersonaId(value));

/// <summary>EF Core value converter for <see cref="SystemPromptId"/>.</summary>
sealed class SystemPromptIdConverter()
	: ValueConverter<SystemPromptId, long>(id => id.Value, value => new SystemPromptId(value));
