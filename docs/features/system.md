# System Feature (Runtime & Configuration Diagnostics)

The *System* feature provides diagnostic endpoints for monitoring and troubleshooting LumaCore instances. It exposes runtime information and configuration values with automatic secret masking.

> [!NOTE]
> This is an **API feature** that exposes diagnostic endpoints. All endpoints require the `admin` role.

---

## Endpoints

All endpoints require a valid JWT token with the `admin` role.

### `GET /api/v1/system/info`

Returns runtime information about the LumaCore instance.

**Requires:** `admin` role (`Authorization: Bearer <token>`)

> For request/response schemas, see the [OpenAPI documentation](../api/README.md).

---

### `GET /api/v1/system/configuration`

Returns all registered configuration options with secrets automatically masked.

**Requires:** `admin` role (`Authorization: Bearer <token>`)

The response structure is dynamic — it reflects whichever Options classes are registered in the application. Each section corresponds to a configuration section in `appsettings.json`.

**Example Response (illustrative):**

```json
{
  "Jwt": {
    "Issuer": "lumacore",
    "Audience": "lumacore-api",
    "SigningKey": "*** (length 32)",
    "AccessTokenLifetimeMinutes": 60
  },
  "Cors": {
    "Enabled": true,
    "AllowedOrigins": ["http://localhost:5173"],
    "AllowCredentials": true
  }
}
```

Properties marked with `[Secret]` are automatically masked (e.g., `SigningKey` above).

---

### `GET /api/v1/system/configuration/{section}`

Returns a specific configuration section.

**Requires:** `admin` role (`Authorization: Bearer <token>`)

Returns `404 Not Found` if the section does not exist.

---

### `GET /api/v1/system/configuration/{section}/{key}`

Returns a specific configuration value. Useful for scripting.

**Requires:** `admin` role (`Authorization: Bearer <token>`)

Returns `404 Not Found` if the section or key does not exist.

**Example:**

```bash
curl -H "Authorization: Bearer $TOKEN" \
  https://localhost:5001/api/v1/system/configuration/Jwt/Issuer

# → "lumacore"
```

---

## Configuration

The *System* feature does not introduce additional configuration options. It automatically discovers all Options registered via `AddFeatureOptions<T>()`.

The feature is registered via `builder.AddSystemFeature()` and mapped to the versioned API group in `Program.cs`.

---

## Secret Masking

Properties marked with `[Secret]` are automatically masked in the configuration output.

```csharp
using LumaCore.Api.Configuration;

public sealed class MyOptions
{
    public string PublicValue { get; set; }
    
    [Secret]
    public string ApiKey { get; set; }  // → "*** (length 32)"
    
    [Secret(ShowLength = false)]
    public string Password { get; set; }  // → "***"
}
```

The `OptionsSanitizer` processes these attributes when serializing configuration for the diagnostic endpoints.

---

## Section Name Resolution

The configuration section name comes directly from the `AddFeatureOptions<T>()` registration:

```csharp
// The section name "Jwt" is tracked and used in diagnostic output
services.AddFeatureOptions<JwtOptions>(configuration, "Jwt");
```

Since all LumaCore Options must be registered via `AddFeatureOptions<T>()`, the section name is always the one actually used for binding — no guesswork, no conventions, no attributes needed.

---

## Registered Services

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `OptionsRegistry` | Singleton | Provides access to sanitized Options values at runtime |

The `OptionsTracker` used by this feature is registered automatically by `AddFeatureOptions<T>()` calls, not by the *System* feature itself.

---

## Pipeline Order

The *System* feature only registers endpoints. The order of `AddSystemFeature()` in service registration does not matter — the `OptionsRegistry` is instantiated lazily on first request, at which point all Options have been registered and finalized.

`MapSystemFeature()` can be called in any order relative to other features.

---

## Related Features

- [*Auth*](auth.md) — Required for endpoint authorization; its `JwtOptions` are exposed via configuration endpoint

---

© 2025 LumaCoreTech • MIT License
