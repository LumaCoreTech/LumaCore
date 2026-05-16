// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Core.Tests.Cryptography;

/// <summary>
/// Unit tests for <see cref="LumaCore.Core.Cryptography.Pbkdf2PasswordHasher"/>.
/// </summary>
/// <remarks>
/// The story across the partial files:
/// <list type="number">
///     <item>
///         <description>Construction — option binding, validation, IOptions wrapper handling.</description>
///     </item>
///     <item>
///         <description>Hash — produces a self-describing string, never repeats output, rejects bad input.</description>
///     </item>
///     <item>
///         <description>
///         Verify — round-trip correctness, tamper detection, malformed-hash diagnostics, and the
///         combined verify + rehash overload (including the information-leak guard that suppresses
///         the rehash signal on failed verifications).
///         </description>
///     </item>
///     <item>
///         <description>NeedsRehash — flags only weaker stored hashes for migration.</description>
///     </item>
/// </list>
/// Helpers (factory + cached known-good hash) live in the Helpers partial.
/// </remarks>
[Trait("Category", "Cryptography")]
public sealed partial class Pbkdf2PasswordHasherTests;
