# GitHub Workflows

CI/CD workflows for LumaCore.

---

## Architecture

```
Build Workflows (Win + Linux)     Integration Workflows (Linux only)
┌─────────────────────────┐       ┌──────────────────────────────┐
│ ALL tests               │       │ LumaCore.Data.Tests only     │
│ SQLite in-memory        │       │ Real database providers      │
│ Coverage collection     │       │ Coverage collection          │
└────────────┬────────────┘       └──────────────┬───────────────┘
             │                                   │
             └────────────────┬──────────────────┘
                              ▼
                   Coverage Merge Workflow
                   ┌─────────────────────┐
                   │ ReportGenerator     │
                   │ Merged badge → Gist │
                   └─────────────────────┘
```

**Key design decisions:**
- Build workflows run **all tests** (unit + data) with SQLite in-memory — fast, complete coverage.
- Integration workflows run **only `LumaCore.Data.Tests`** — no redundant unit test execution per provider.
- Integration workflows run on **Linux only** (GitHub Actions `services` require Linux runners).
- Coverage fragments are uploaded as artifacts and merged by a dedicated workflow.

---

## Workflows Overview

### Build & Test Workflows

#### `linux-build.yml`
- **Triggers:** Push to main, Pull Requests
- **Runner:** `ubuntu-latest`
- **Provider:** SQLite in-memory (default, no env var needed)
- **Purpose:** Build verification, testing, coverage, API documentation verification
- **Uploads:** Coverage artifact (`coverage-build-ubuntu`)

#### `windows-build.yml`
- Same as `linux-build.yml` but runs on `windows-latest`
- Ensures cross-platform compatibility
- **Uploads:** Coverage artifact (`coverage-build-windows`)

### Integration Test Workflows

All integration workflows delegate to the shared composite action (`.github/actions/integration-test/`).
They run **only `LumaCore.Data.Tests`** against the configured provider.

- **Triggers:** Push to main, Pull Requests, Manual dispatch (`workflow_dispatch`)

#### `integration-tests-sqlite.yml`
- **Runner:** `ubuntu-latest` (no Docker needed)
- **Provider:** SQLite file-based — exercises real I/O, WAL journaling, and locking

#### `integration-tests-postgres.yml`
- **Runner:** `ubuntu-latest` with PostgreSQL 17 service container
- **Provider:** PostgreSQL

#### `integration-tests-sqlserver.yml`
- **Runner:** `ubuntu-latest` with SQL Server 2022 service container
- **Provider:** SQL Server

#### `integration-tests-mysql.yml`
- **Runner:** `ubuntu-latest` with MySQL 8 service container
- **Provider:** MySQL

> ⚠️ **MySQL is currently disabled** (manual dispatch only). Pomelo.EntityFrameworkCore.MySql does not yet
> support EF Core 10. Once a compatible version ships, uncomment the push/PR triggers in the workflow file.

**Environment variables** (set by composite action):
- `LUMACORE_INTEGRATION_TESTS=true` — enables `[IntegrationFact]` tests
- `LUMACORE_TESTS__Db__Provider` — selects the database provider
- `LUMACORE_TESTS__Db__ConnectionString` — provider-specific connection string
- `LUMACORE_TESTS__Db__EnsureDeleted=true` — drops and recreates the test database

### Coverage Merge Workflow

#### `coverage-merge.yml`
- **Triggers:** `workflow_run` — fires when any coverage-producing workflow completes on main
- **Purpose:** Downloads coverage artifacts from all workflows, merges them with ReportGenerator, and pushes a combined badge to the shared gist
- Uses `dawidd6/action-download-artifact` with `continue-on-error` so partial merges work when some workflows haven't run yet

### Release Workflow

#### `release.yml`
- **Triggers:** Version tags (`v1.0.0`, `v1.2.3`, etc.)
- **Purpose:** Build releases, publish packages
- **Jobs:**
  - Build release artifacts
  - Run tests
  - Create GitHub Release

---

## Badges

All badges use the [shields.io endpoint API](https://shields.io/endpoint) backed by JSON files in a shared GitHub Gist (`d3957602a84fcab1aa66ebfef44da7eb`). Each workflow generates its own badge JSON and pushes it to the gist via `exuanbo/actions-deploy-gist`.

### Badge Overview

| Badge | Source Workflow | Gist File | Content |
|-------|---------------|-----------|---------|
| Windows Build | `windows-build.yml` | *(shields.io workflow badge)* | Pass/fail status |
| Windows Tests | `windows-build.yml` | `lumacore-windows-test-badge.json` | `passed/total passed` |
| Windows Coverage | `windows-build.yml` | `lumacore-windows-coverage-badge.json` | `L: x% \| B: y%` |
| Ubuntu Build | `linux-build.yml` | *(shields.io workflow badge)* | Pass/fail status |
| Ubuntu Tests | `linux-build.yml` | `lumacore-ubuntu-test-badge.json` | `passed/total passed` |
| Ubuntu Coverage | `linux-build.yml` | `lumacore-ubuntu-coverage-badge.json` | `L: x% \| B: y%` |
| SQLite | `integration-tests-sqlite.yml` | `lumacore-integration-sqlite-badge.json` | `passed/total passed` |
| PostgreSQL | `integration-tests-postgres.yml` | `lumacore-integration-postgresql-badge.json` | `passed/total passed` |
| SQL Server | `integration-tests-sqlserver.yml` | `lumacore-integration-sqlserver-badge.json` | `passed/total passed` |
| MySQL | `integration-tests-mysql.yml` | `lumacore-integration-mysql-badge.json` | `pending` (disabled) |
| Merged Coverage | `coverage-merge.yml` | `lumacore-merged-coverage-badge.json` | `L: x% \| B: y%` |

### Data Sources

- **Build status badges** — Native shields.io GitHub Actions badges (no gist needed).
- **Test count badges** — Parsed from `.trx` (TRX) files via PowerShell (`$trx.TestRun.ResultSummary.Counters`).
- **Per-platform coverage badges** — Generated by `dotnet-reportgenerator-globaltool` from Coverlet's `coverage.cobertura.xml`, parsed from `Summary.txt`.
- **Merged coverage badge** — Same as per-platform, but ReportGenerator merges multiple `.cobertura.xml` files from across all workflows (downloaded as artifacts).

---

## Configuration

### Required Secrets

Configure in **Settings → Secrets and variables → Actions:**

| Secret | Purpose | Required |
|--------|---------|----------|
| `GITHUB_TOKEN` | Create releases, workflow access | ✅ Auto-provided |
| `GIST_TOKEN` | Update badge gists (test counts, coverage) | ✅ Required |
| `NUGET_API_KEY` | Publish to NuGet.org | ⚠️ Optional |
| `DOCKER_USERNAME` | Docker Hub login | ⚠️ Optional |
| `DOCKER_PASSWORD` | Docker Hub password | ⚠️ Optional |

### Branch Protection (Recommended)

**Settings → Branches → Branch protection rules:**

- ✅ Require pull request reviews before merging
- ✅ Require status checks to pass before merging:
  - `build` (linux-build.yml)
  - `build` (windows-build.yml)
  - `Integration Tests (SQLite)` (integration-tests-sqlite.yml)
  - `Integration Tests (PostgreSQL)` (integration-tests-postgres.yml)
  - `Integration Tests (SQL Server)` (integration-tests-sqlserver.yml)
  - `Integration Tests (MySQL)` (integration-tests-mysql.yml) — *add once enabled*
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

© 2025-2026 LumaCoreTech • MIT License
