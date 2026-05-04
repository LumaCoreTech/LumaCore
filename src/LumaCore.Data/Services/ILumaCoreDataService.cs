// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Services;

/// <summary>
/// Provides a high-level use-case oriented API for interacting with the LumaCore database.
/// </summary>
/// <remarks>
///     <para>
///     This service is intended as the default entry point for common database operations in application code.
///     It encapsulates privacy-first policies (e.g. message redaction) and ensures consistent behavior across
///     features.
///     </para>
///     <para>
///     Lower-level access (direct <see cref="LumaCoreDbContext"/> usage, or query helpers in
///     <c>LumaCore.Data.Queries</c>) is still possible for advanced scenarios, but should be used intentionally.
///     </para>
///     <para>
///     <b>Contract / boundary rules:</b> Methods in this facade generally validate input at the boundary and throw
///     exceptions for invalid arguments (empty strings, invalid IDs, out-of-range limits). This keeps failures
///     deterministic and prevents database-provider-specific exceptions for common validation errors.
///     </para>
/// </remarks>
public interface ILumaCoreDataService :
	IUserDataService,
	IRoleDataService,
	IConversationDataService,
	IModelEndpointDataService,
	IMessageDataService,
	IPersonaDataService,
	IResourceDataService,
	IDataIntegrityService;
