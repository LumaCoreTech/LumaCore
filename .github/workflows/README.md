# GitHub Workflows

CI/CD workflows for LumaCore.

---

## Workflows Overview

### Build & Test Workflows

#### `linux-build.yml`
- **Triggers:** Push to main, Pull Requests
- **Purpose:** Build verification, testing, API documentation verification
- **Jobs:**
  - `build` - Compile, test, and verify API documentation

#### `windows-build.yml`
- Same as `linux-build.yml` but runs on Windows
- Ensures cross-platform compatibility

### Release Workflow

#### `release.yml`
- **Triggers:** Version tags (`v1.0.0`, `v1.2.3`, etc.)
- **Purpose:** Build releases, publish packages
- **Jobs:**
  - Build release artifacts
  - Run tests
  - Create GitHub Release

---

## Configuration

### Required Secrets (for release.yml)

Configure in **Settings → Secrets and variables → Actions:**

| Secret | Purpose | Required |
|--------|---------|----------|
| `GITHUB_TOKEN` | Create releases | ✅ Auto-provided |
| `NUGET_API_KEY` | Publish to NuGet.org | ⚠️ Optional |
| `DOCKER_USERNAME` | Docker Hub login | ⚠️ Optional |
| `DOCKER_PASSWORD` | Docker Hub password | ⚠️ Optional |

### Branch Protection (Recommended)

**Settings → Branches → Branch protection rules:**

- ✅ Require pull request reviews before merging
- ✅ Require status checks to pass before merging:
  - `build` (linux-build.yml)
  - `build` (windows-build.yml)
- ✅ Do not allow bypassing the above settings

---

## Troubleshooting

### "API documentation is outdated" Error

**Problem:** CI detects that committed API documentation doesn't match current API surface.

**Solution:**
```powershell
# Regenerate documentation locally
.\Pre-Commit-Actions.ps1

# Commit updated docs
git add docs/api/
git commit -m "docs: update API documentation"
git push
```

### Release Workflow Not Triggering

**Problem:** Pushed tag but `release.yml` didn't run.

**Check:**
1. Tag format correct? Must be `v*` (e.g., `v1.0.0`)
2. Check **Actions** tab for workflow runs
3. Ensure workflow file exists in main branch

---

© 2025 LumaCoreTech • MIT License