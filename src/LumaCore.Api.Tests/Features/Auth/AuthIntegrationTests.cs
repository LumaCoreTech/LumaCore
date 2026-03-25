// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

/// <summary>
/// HTTP-level integration tests for the authentication feature.
/// </summary>
/// <remarks>
///     <para>
///     These tests exercise the complete JWT authentication pipeline through
///     <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>: from HTTP request through
///     <c>UseAuthentication()</c> / <c>UseAuthorization()</c> middleware to the endpoint handlers
///     and back. They verify behavior that is <b>not</b> observable through isolated unit tests —
///     in particular the <c>JwtBearerEvents</c> (cookie extraction, revocation check, failure logging)
///     wired up in <see cref="ServiceRegistration"/>.
///     </para>
///     <para>
///     The test harness uses SQLite in-memory as the database backend (same pattern as
///     <see cref="TokenRevocationServiceTests"/>), keeping tests fast and self-contained.
///     </para>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>Login — Credential validation, request validation, and token issuance.</item>
///         <item>ProtectedEndpoints — Bearer token acceptance and rejection on protected routes.</item>
///         <item>Logout — Token revocation and subsequent rejection of the revoked token.</item>
///         <item>CookieTransport — Cookie-based authentication as an alternative to Bearer headers.</item>
///     </list>
/// </remarks>
[Trait("Category", "Auth")]
public sealed partial class AuthIntegrationTests;
