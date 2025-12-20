# Release Process

This document describes the release process for LumaCore maintainers who have permission to create releases and publish packages.

---

## Creating a Release

When ready to publish a new version, use a tag-based process. Create a version tag, push it, and *GitHub Actions* handles the rest.

### Version Numbers

LumaCore uses semantic versioning: `v{major}.{minor}.{patch}`

Choose the version number based on changes since the last release:
- **Major** (v1.0.0 → v2.0.0): Breaking changes requiring user code updates
- **Minor** (v1.0.0 → v1.1.0): New features, backwards compatible
- **Patch** (v1.0.0 → v1.0.1): Bug fixes only

For example, adding an optional parameter is a minor version bump. Removing an endpoint or changing a required parameter is a major version bump.

### Creating the Tag

Ensure everything is merged to `main` and all tests pass. Create an annotated tag:

```bash
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

### Automated Release

The `release.yml` workflow runs automatically when you push the tag. It:

1. Builds the solution in *Release* configuration (without regenerating documentation - it uses committed docs)
2. Runs the full test suite (any failure stops the release)
3. Packs *NuGet* packages to `./artifacts/packages/`
4. Creates a *GitHub Release* with auto-generated release notes
5. Attaches `.nupkg` files to the release
6. Optionally publishes to *NuGet.org* (if configured)

The version number comes from the git tag via *MinVer*, so project files don't need manual version updates.

### Verification

Check the *GitHub Releases* page to verify:
- Release created with correct version
- Release notes look accurate
- `.nupkg` files attached
- If publishing to *NuGet.org*, verify packages appear there (may take a few minutes)

### When Things Go Wrong

If the release workflow fails, check *GitHub Actions* logs. Common causes:
- Test failures: Fix tests, create new patch version
- Missing secrets: Configure `NUGET_API_KEY` in repository settings
- Wrong tag format: Must be `v{major}.{minor}.{patch}`

> [!IMPORTANT]
> If a release was published incorrectly, don't delete or move the tag. Create a new patch version with the fix. Once published, versions should remain immutable - users may have already pulled that version.

For critically broken releases (security issues, critical bugs), mark the *GitHub Release* as pre-release to hide it, but leave the tag. Document the issue in a new release with the fix.

---

## Related Documentation

- [Developer Workflow](workflow.md) - Contributing to LumaCore
- [CI/CD Workflows](../../.github/workflows/README.md) - Workflow architecture

---

© 2025 LumaCoreTech • MIT License
