// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Security;

using Xunit;

namespace LumaCore.Data.Tests.Security;

/// <summary>
/// Unit tests for <see cref="AesGcmSecretProtector"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify encryption/decryption correctness, key rotation, input validation,
///     and error handling to achieve 100% code coverage.
///     </para>
///     <para>
///     Test files are organized using a hybrid approach:
///     <list type="bullet">
///         <item>
///         <b>Method-based files</b> (for generic method behavior):
///         <list type="bullet">
///             <item><c>AesGcmSecretProtectorTests.Construction.cs</c> — Constructor tests</item>
///             <item>
///             <c>AesGcmSecretProtectorTests.Protect.cs</c> — <see cref="AesGcmSecretProtector.Protect"/> tests
///             (format, nondeterminism, validation)
///             </item>
///             <item>
///             <c>AesGcmSecretProtectorTests.Unprotect.cs</c> — <see cref="AesGcmSecretProtector.Unprotect"/> tests
///             (roundtrip, format errors, tampering)
///             </item>
///             <item><c>AesGcmSecretProtectorTests.Dispose.cs</c> — Dispose behavior tests</item>
///         </list>
///         </item>
///         <item>
///         <b>Feature-based files</b> (for specific features that span multiple methods):
///         <list type="bullet">
///             <item>
///             <c>AesGcmSecretProtectorTests.KeyRotation.cs</c> — Key rotation scenarios (uses both
///             Protect/Unprotect)
///             </item>
///             <item>
///             <c>AesGcmSecretProtectorTests.DomainSeparation.cs</c> — HKDF domain isolation (uses both
///             Protect/Unprotect)
///             </item>
///         </list>
///         </item>
///         <item><c>AesGcmSecretProtectorTests.Helpers.cs</c> — Shared test helpers</item>
///     </list>
///     </para>
///     <para>
///     <b>Organization Rationale:</b> Generic method behavior (e.g., null checks, format validation) is organized
///     by method. Complex features that involve multiple methods working together (e.g., key rotation, domain separation)
///     are organized by feature to keep related tests cohesive.
///     </para>
/// </remarks>
[Trait("Category", "Security")]
[Trait("Category", "Cryptography")]
public sealed partial class AesGcmSecretProtectorTests;
