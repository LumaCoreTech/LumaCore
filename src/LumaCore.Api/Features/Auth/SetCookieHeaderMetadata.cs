// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Endpoint metadata that instructs <see cref="SetCookieHeaderTransformer"/> to add a <c>Set-Cookie</c> response
/// header to the OpenAPI specification for the annotated endpoint.
/// </summary>
/// <param name="StatusCode">
/// The HTTP status code (e.g., <c>"200"</c>) whose response receives the <c>Set-Cookie</c> header.
/// </param>
/// <param name="Description">The description text for the <c>Set-Cookie</c> header in the OpenAPI specification.</param>
sealed record SetCookieHeaderMetadata(string StatusCode, string Description);
