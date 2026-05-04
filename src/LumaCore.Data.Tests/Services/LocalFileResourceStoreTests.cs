// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Xunit;

namespace LumaCore.Data.Tests.Services;

// Filesystem-backed resource store: from construction through every CRUD-style operation.
//
// These tests follow the lifecycle of a single file under the store, plus the cross-cutting
// path-traversal protection that every operation enforces:
//
//   1. Construction: storage root is resolved to an absolute path; null arguments are rejected
//      (Constructor file).
//
//   2. SaveAsync: writes a new file, refuses to overwrite, and cleans up partial writes when
//      content streaming fails or is cancelled (SaveAsync file).
//
//   3. DeleteAsync: honours the IResourceStore contract — true if the file existed and was
//      removed, false if it never existed or vanished mid-call (DeleteAsync file). This is the
//      method the audit identified as previously broken; coverage is intentionally thorough.
//
//   4. OpenReadAsync: returns a readable stream or null without throwing on missing files
//      (OpenReadAsync file).
//
//   5. ExistsAsync: thin wrapper over File.Exists (ExistsAsync file).
//
//   6. Path traversal: every public operation refuses paths that escape the storage root
//      (PathTraversal file).
//
// Helpers (temporary storage root, log capture, stream factory) live in the Helpers file.
/// <summary>
/// Tests for <see cref="LocalFileResourceStore"/>.
/// </summary>
[Trait("Category", "Resources")]
public sealed partial class LocalFileResourceStoreTests { }
