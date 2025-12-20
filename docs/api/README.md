# LumaCore API Documentation

This directory contains the OpenAPI specifications and generated documentation for all API versions.

## Available Versions

| Version | Status | OpenAPI Spec | Documentation |
|---------|--------|--------------|---------------|
| **v1** | Current | [v1.json](v1.json) | [v1.md](v1.md) |

## Quick Links

- **Swagger UI**: Available at `/swagger` when running in Development mode
- **OpenAPI Endpoints**: `/openapi/v1.json` (runtime)

## Generating Documentation

The documentation is generated from OpenAPI specifications using a two-step process:

### Step 1: Generate OpenAPI JSON

```bash
./build.net/OpenApi/generate-openapi-json.ps1
```

By default, this generates `v1.json`. When V2+ exists:

```bash
./build.net/OpenApi/generate-openapi-json.ps1 -Versions @('v1', 'v2')
```

### Step 2: Generate Markdown

```bash
./build.net/OpenApi/generate-api-docs.ps1
```

This reads all `v*.json` files and generates corresponding `v*.md` documentation.

## CI Verification

The `verify-api-docs.ps1` script ensures documentation stays in sync:

```bash
./build.net/OpenApi/verify-api-docs.ps1 -ApiProject src/LumaCore.Api/LumaCore.Api.csproj
```

## Adding a New API Version

When adding V2:

1. Register the version in `ApiVersions.cs`
2. Add contracts in `LumaCore.Api.Contracts/V2/<Feature>/` folders
3. Map endpoints with `.MapToApiVersion(ApiVersions.V2)`
4. Regenerate documentation
5. Update this README with the new version entry
