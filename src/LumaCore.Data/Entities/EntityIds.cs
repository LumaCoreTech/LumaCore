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
/// Strongly-typed identifier for a <see cref="ResourceEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct ResourceId(long Value);

/// <summary>
/// Strongly-typed identifier for a <see cref="ResourceReferenceEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct ResourceReferenceId(long Value);

/// <summary>
/// Strongly-typed identifier for a <see cref="SystemPromptEntity"/>.
/// </summary>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct SystemPromptId(long Value);

/// <summary>
/// Strongly-typed polymorphic identifier for the owner of a <see cref="ResourceReferenceEntity"/>.
/// </summary>
/// <remarks>
///     <para>
///     The interpretation of <see cref="Value"/> depends on the accompanying
///     <see cref="ResourceOwnerKind"/> discriminator carried alongside on
///     <see cref="ResourceReferenceEntity.OwnerKind"/>: e.g. for <see cref="ResourceOwnerKind.Message"/>
///     the value is a <see cref="MessageId"/> value, for <see cref="ResourceOwnerKind.User"/> a
///     <see cref="UserId"/> value, for <see cref="ResourceOwnerKind.Persona"/> a <see cref="PersonaId"/>
///     value, and so on.
///     </para>
///     <para>
///     Because the discriminator and id form a logical unit, the conversion from a per-table strongly-typed
///     id (e.g. <see cref="UserId"/>) to <see cref="ResourceOwnerId"/> is intentionally explicit at the
///     call site (<c>new ResourceOwnerId(userId.Value)</c>) — there is no implicit conversion.
///     </para>
///     <para>
///     Use <see cref="Unassigned"/> as the sentinel for pending references that have not yet been wired to
///     their final owner; see <see cref="ResourceReferenceEntity.OwnerId"/> for the full sentinel rationale.
///     </para>
/// </remarks>
/// <param name="Value">The underlying database identifier.</param>
public readonly record struct ResourceOwnerId(long Value)
{
	/// <summary>
	/// Sentinel value indicating that the reference is <em>pending</em> — it has been created (typically
	/// during an upload that precedes the owning entity's INSERT) but not yet wired to its final owner.
	/// </summary>
	/// <remarks>
	/// Zero is safe as a sentinel because every entity-table primary key in this database is a positive
	/// auto-increment <see cref="long"/> — no real <see cref="ResourceReferenceEntity.OwnerId"/> can ever
	/// take this value.
	/// </remarks>
	public static ResourceOwnerId Unassigned { get; } = new(0);
}

// EF Core value converters that allow EF to transparently persist XxxId ↔ long in the database.
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

/// <summary>EF Core value converter for <see cref="ResourceId"/>.</summary>
sealed class ResourceIdConverter()
	: ValueConverter<ResourceId, long>(id => id.Value, value => new ResourceId(value));

/// <summary>EF Core value converter for <see cref="ResourceReferenceId"/>.</summary>
sealed class ResourceReferenceIdConverter()
	: ValueConverter<ResourceReferenceId, long>(id => id.Value, value => new ResourceReferenceId(value));

/// <summary>EF Core value converter for <see cref="SystemPromptId"/>.</summary>
sealed class SystemPromptIdConverter()
	: ValueConverter<SystemPromptId, long>(id => id.Value, value => new SystemPromptId(value));

/// <summary>EF Core value converter for <see cref="ResourceOwnerId"/>.</summary>
sealed class ResourceOwnerIdConverter()
	: ValueConverter<ResourceOwnerId, long>(id => id.Value, value => new ResourceOwnerId(value));
