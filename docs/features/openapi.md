# OpenAPI Feature (API Documentation)

The *OpenAPI* feature generates OpenAPI 3.1 specification documents for the LumaCore API, enabling interactive documentation and client code generation.

> [!NOTE]
> This is a **foundation feature** that provides infrastructure for API documentation. OpenAPI endpoints are only available in Development mode.

---

## Overview

LumaCore uses native .NET 10 OpenAPI generation (not Swashbuckle) to produce specification documents. Each API version has its own document, ensuring clients can generate code for specific versions.

Key behaviors:

- **One document per version** – `/openapi/v1.json`, `/openapi/v2.json`, etc.
- **Development only** – OpenAPI endpoints are not exposed in Production.
- **JWT security scheme** – All documents include Bearer authentication definitions.
- **Automatic error responses** – Common error responses (400, 401, 403) are documented automatically.

---

## Endpoints

| Endpoint | Environment | Description |
|----------|-------------|-------------|
| `GET /openapi/v1.json` | Development | OpenAPI 3.1 spec for API v1 |
| `GET /swagger` | Development | Swagger UI for interactive exploration |

> [!WARNING]
> OpenAPI endpoints are intentionally disabled in Production to avoid exposing API structure details. Use the build-time generation script for Production deployments.

---

## Document Contents

Each generated OpenAPI document includes:

### Metadata

- Title: "LumaCore API"
- Description: API surface of the LumaCore server
- Contact: LumaCore Project (https://lumacore.tech)
- License: MIT License

### Security Scheme

```yaml
components:
  securitySchemes:
    Bearer:
      type: http
      scheme: bearer
      bearerFormat: JWT
      description: Enter your JWT token
```

### Automatic Error Responses

The `SecurityResponsesTransformer` automatically adds error response documentation based on endpoint metadata:

| Condition | Response Added |
|-----------|----------------|
| Has `[Authorize]` | 401 Unauthorized, 403 Forbidden |
| Accepts request body | 400 Bad Request |

---

## Build-Time Generation

For CI/CD pipelines and Production deployments, use the PowerShell script to generate OpenAPI JSON at build time:

```powershell
./build.net/OpenApi/generate-openapi-json.ps1
```

This script:

1. Builds the project
2. Launches the app with `GetDocument.Insider` (MSBuild tooling)
3. Extracts the OpenAPI document without starting the full server
4. Saves to `build.net/OpenApi/output/v1.json`

---

## Configuration

The *OpenAPI* feature does not introduce additional configuration options. Document generation is configured in code.

The feature is registered via `builder.AddOpenApiFeature()` and mapped via `app.MapOpenApi()` in `Program.cs`.

---

## Registered Services

The *OpenAPI* feature registers services via the standard `AddOpenApi()` infrastructure:

| Service | Lifetime | Description |
|---------|----------|-------------|
| OpenAPI document services | Scoped | Native .NET 10 OpenAPI generation |

---

## Adding a New API Version

When adding a new API version, register its OpenAPI document in `OpenApi/ServiceRegistration.cs`:

```csharp
AddOpenApiDocument(builder.Services, ApiVersions.V2);
```

The document will be available at `/openapi/v2.json` in Development mode.

---

## Related Features

- [*API Versioning*](api-versioning.md) — Defines the versions for which documents are generated
- [*Auth*](auth.md) — JWT Bearer authentication documented in the security scheme

---

© 2025 LumaCoreTech • MIT License