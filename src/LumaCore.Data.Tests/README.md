# `LumaCore.Data.Tests`

Unit and integration tests for the `LumaCore.Data` layer, covering:

- **Configuration** — `DatabaseOptions` (auto-migration, recovery, user deletion)
- **Initialization** — `DatabaseInitializer`, `DatabaseConnectionInterceptor`, `DatabaseInitializationStatus`,
  `DatabaseInitializationException`
- **Services** — `LumaCoreDataService` facade (users, roles, conversations, messages, model endpoints, integrity)
- **Security** — `AesGcmSecretProtector` (encrypt/decrypt, key rotation, domain separation, disposal)
- **Providers** — `SqliteProviderOperations`, `PostgreSqlProviderOperations`, `SqlServerProviderOperations`,
  `MySqlProviderOperations`, `DatabaseProviderFactory`, `DatabaseProviders`, plus provider operations integration tests
- **DataPort** — SQLite export/import (`SqliteExportReader`, `SqliteImportWriter`), shuttle format
  (`SqliteShuttleReader`, `SqliteShuttleWriter`, `SqliteShuttleReaderFactory`, shuttle roundtrip),
  `SqlIdentifierHelper`, `MigrationInfo`, `DataPortSchemaMismatchException`, `DataPortProgressReport`
- **Migrations** — `MigrationIntegrationTests` (schema creation/teardown against real providers)
- **Queries** — `CompiledQueryRegressionTests` (compiled query regression against real databases)
- **Smoke tests** — entity model, service registration, design-time factory

By default (CI-friendly), tests run against **SQLite in-memory** (`sqlitememory`).
Use `sqlite` for file-based SQLite matching production, or target **PostgreSQL** or **SQL Server**.

> **Note:** MySQL/MariaDB support is temporarily unavailable — Pomelo.EntityFrameworkCore.MySql has not yet released
> an EF Core 10 compatible version. The `mysql` provider option exists for forward compatibility but selecting it
> will throw `NotSupportedException`.

## Provider selection

The shared fixtures (`DbFixture`, `IntegrationTestHarness`) and the `DatabaseInitializerTests`-internal
`TestHarness` all read test database settings via `DbTestSettingsLoader`.
Settings are loaded in this order (last wins):

1. `appsettings.json` (committed — `sqlitememory` default, other providers as commented-out templates)
2. `appsettings.Development.json` (optional, not committed)
3. Environment variables prefixed with `LUMACORE_TESTS__`

If nothing is configured, the default is `sqlitememory` (in-memory, no connection string required).

### `Db:Provider`

| Value | Description |
|-------|-------------|
| `sqlitememory` | In-memory, fastest — **default** when no provider is configured |
| `sqlite` | File-based — matches production behavior |
| `postgresql` | Requires external server — connection string required |
| `sqlserver` | Requires external server — connection string required |
| `mysql` | Temporarily unavailable — throws `NotSupportedException` |

### `Db:ConnectionString`

- Required for `postgresql` and `sqlserver`.
- Must **not** include a database name — each fixture derives its own unique name from `Db:DatabasePrefix`.
- Ignored for `sqlite` and `sqlitememory` (managed automatically).

### `Db:DatabasePrefix`

Prefix for the unique per-fixture database name (default: `lumacore_test`). Only relevant for external
providers (PostgreSQL, SQL Server). Each fixture appends a GUID suffix (e.g., `lumacore_test_a1b2c3…`) to
create an isolated database per test class and drops it on disposal (best-effort).

## Configuration

### Option A: Environment variables (recommended)

The loader expects environment variables with prefix `LUMACORE_TESTS__`.
Hierarchical keys use `__` as separator (the cross-platform standard for .NET configuration).

#### PostgreSQL (example)

- `LUMACORE_TESTS__Db__Provider=postgresql`
- `LUMACORE_TESTS__Db__ConnectionString=Host=localhost;Port=5432;Username=postgres;Password=postgres`

#### SQL Server (example)

- `LUMACORE_TESTS__Db__Provider=sqlserver`
- `LUMACORE_TESTS__Db__ConnectionString=Server=localhost;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True`

### Option B: `appsettings.Development.json`

Create `src/LumaCore.Data.Tests/appsettings.Development.json` locally (do not commit secrets).

Example:

```json
{
  "Db": {
    "Provider": "postgresql",
    "ConnectionString": "Host=localhost;Port=5432;Username=postgres;Password=postgres"
  }
}
```

## Integration tests

Integration tests (`MigrationIntegrationTests`, `ProviderOperationsIntegrationTests`, `SqliteShuttleRoundtripTests`,
etc.) exercise real database interactions. They are tagged with `[Trait("Category", "<scope>")]` — e.g.,
`"Migrations"`, `"Providers"`, `"DataPort"` — and can be filtered in CI.

All tests run against the configured provider (defaulting to SQLite in-memory). No special environment variable
or skip attribute is needed — provider selection via `DbTestSettingsLoader` controls which database engine the
tests target.

## Notes / limitations

- CI uses `sqlite` (file-based, production-like, self-contained).
- `EnsureCreatedAsync()` is used to create the schema for most tests.
  Migration integration tests use `IMigrator` to drive schema creation explicitly.

## Test implementation details

### `__EFMigrationsHistory` manipulation

Some `DatabaseInitializer` tests manipulate `__EFMigrationsHistory` to control EF Core's migration state without
having to execute a full `MigrateAsync()` flow (which can be incompatible with `EnsureCreatedAsync()` schemas).

### Reflection usage

A few tests call private `DatabaseInitializer` helpers via reflection to keep them deterministic and focused
(e.g. backup retention cleanup runs only after a successful migration in production).
