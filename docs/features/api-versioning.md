# API Versioning Feature (URL Segment Versioning)

The *API Versioning* feature provides URL segment-based versioning for all API endpoints, ensuring clients can reliably target specific API versions.

> [!NOTE]
> This is a **foundation feature** that provides infrastructure for other features. It does not expose API endpoints itself.

---

## Overview

LumaCore uses URL segment versioning (`/api/v1/...`) rather than header-based or query string versioning. This approach makes the version explicit in every request and simplifies debugging, logging, and documentation.

Key behaviors:

- **Explicit versioning required** – Every endpoint must declare its supported versions via `MapToApiVersion()`. The application validates this at startup and fails fast if any endpoint is missing a version mapping.
- **No default version** – Clients must specify the version in the URL. Requests without a version segment (e.g., `/api/auth/login`) return `404 Not Found`. Requests with an unsupported version return `400 Bad Request` with details about available versions.
- **Deprecation support** – Older versions can be marked as deprecated, adding the `api-deprecated-versions` header to responses.

---

## How It Works

All versioned API endpoints are mounted under a central route group created by `MapVersionedApiGroup()`. This group applies:

- URL segment versioning (`/api/v{version}/`)
- Automatic request validation via `WithValidation()`
- Version reporting in response headers

### Supported Versions

| Version | Status | Endpoints |
|---------|--------|-----------|
| `v1` | Current | `/api/v1/auth/*`, `/api/v1/admin/*`, `/api/v1/health/*` |

### Adding a New Version

1. Add a new constant in `ApiVersions.cs`:
   ```csharp
   public static readonly ApiVersion V2 = new(2);
   ```

2. Register it in `VersionedApiGroup.MapVersionedApiGroup()`:
   ```csharp
   .HasApiVersion(ApiVersions.V2)
   ```

3. Use `MapToApiVersion()` in endpoint mappings:
   ```csharp
   group.MapGet("/new-endpoint", HandleNew)
        .MapToApiVersion(ApiVersions.V2);
   ```

4. Add an OpenAPI document in `OpenApi/ServiceRegistration.cs`:
   ```csharp
   AddOpenApiDocument(builder.Services, ApiVersions.V2);
   ```

### Deprecating a Version

To deprecate an older version:

```csharp
.HasDeprecatedApiVersion(ApiVersions.V1)
```

This adds the `api-deprecated-versions: v1` header to responses, signaling clients to migrate.

---

## Configuration

The *API Versioning* feature does not introduce additional configuration options. Versioning behavior is defined in code via the `ApiVersions` class.

The feature is registered via `builder.AddApiVersioningFeature()` in `Program.cs`.

---

## Registered Services

The *API Versioning* feature registers the following services:

| Service | Lifetime | Description |
|---------|----------|-------------|
| `IApiVersioningFeature` | Scoped | ASP.NET Core API versioning infrastructure |
| `IApiVersionDescriptionProvider` | Singleton | Provides metadata about available API versions |

---

## Startup Validation

The application validates at startup that every endpoint has an explicit version mapping via `MapToApiVersion()`. Endpoints without explicit version declarations cause startup failure.

This fail-fast behavior prevents accidental exposure of unversioned endpoints.

---

## Related Features

- [*Validation*](validation.md) — Request validation is automatically applied to the versioned API group
- [*OpenApi*](openapi.md) — Generates one OpenAPI document per API version

---

© 2025 LumaCoreTech • MIT License