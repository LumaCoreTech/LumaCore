// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Xunit;

namespace LumaCore.Data.Tests.Services;

/// <summary>
/// Tests for <see cref="LumaCoreDataService"/>. This partial class serves as the anchor; individual interface
/// implementations are tested in nested classes organized into separate partial files.
/// </summary>
/// <remarks>
///     <para>
///     The nested-class layout mirrors the service's implemented interfaces. Each nested class inherits from
///     <see cref="TestBase"/> and runs against a fresh SQLite in-memory database, keeping tests isolated while
///     exercising real EF Core behavior.
///     </para>
///     <para>
///         <b>Partial files (reading order):</b>
///     </para>
///     <list type="number">
///         <item>
///             <term>TestBase</term>
///             <description>
///             Shared fixture setup, lifecycle hooks, and helper methods used by all nested
///             test classes.
///             </description>
///         </item>
///         <item>
///             <term>Users</term>
///             <description>
///             <see cref="IUserDataService"/> — validation/normalization (trimming, limits, null
///             handling), query helpers (lookup and existence checks), and privacy-sensitive deletion behavior
///             (participant scrubbing, message redaction).
///             </description>
///         </item>
///         <item>
///             <term>Conversations</term>
///             <description>
///             <see cref="IConversationDataService"/> — creation, membership management, title
///             updates, deletion, and lookup behaviors.
///             </description>
///         </item>
///         <item>
///             <term>Messages</term>
///             <description>
///             <see cref="IMessageDataService"/> — message creation, listing (ordering/limit
///             behavior), and redaction APIs.
///             </description>
///         </item>
///         <item>
///             <term>ModelEndpoints</term>
///             <description>
///             <see cref="IModelEndpointDataService"/> — endpoint CRUD, credential
///             encryption/decryption roundtrips, active/inactive filtering, and metadata updates.
///             </description>
///         </item>
///         <item>
///             <term>Roles</term>
///             <description>
///             <see cref="IRoleDataService"/> — role assignment and removal, including idempotent
///             behavior and database-constraint-backed duplicate handling.
///             </description>
///         </item>
///         <item>
///             <term>Personas</term>
///             <description>
///             <see cref="IPersonaDataService"/> — persona CRUD with ownership and visibility
///             filtering, system prompt versioning with SHA-256 deduplication, participant identity
///             management, cloning, and activation/deactivation.
///             </description>
///         </item>
///         <item>
///             <term>Integrity</term>
///             <description>
///             <see cref="IDataIntegrityService"/> — integrity queries and cleanup routines that
///             detect and remove invalid data shapes (e.g., conversations with no user participants).
///             </description>
///         </item>
///         <item>
///             <term>Resources</term>
///             <description>
///             <see cref="IResourceDataService"/> — resource reference metadata queries and set-based
///             owner assignment for attachment wiring.
///             </description>
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Services")]
public sealed partial class LumaCoreDataServiceTests { }
