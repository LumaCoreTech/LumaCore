# System Feature (Runtime & Configuration Diagnostics)

The *System* feature provides diagnostic endpoints for monitoring and troubleshooting LumaCore instances. It exposes instance identity, runtime metrics, and configuration values with automatic secret masking.

> [!NOTE]
> This is an **API feature** that exposes diagnostic endpoints. All endpoints require the `admin` role.

---

## Endpoints

All endpoints require a valid JWT token with the `admin` role.

### `GET /api/v1/system/info`

Returns identity information about the LumaCore instance including environment, version, and machine name. For runtime metrics (memory, GC, thread pool), use `/api/v1/system/metrics` instead.

**Requires:** `admin` role (`Authorization: Bearer <token>`)

> For request/response schemas and error codes, see the [OpenAPI documentation](../api/README.md).

---

### `GET /api/v1/system/metrics`

Returns a comprehensive snapshot of runtime diagnostics including memory usage, garbage collection statistics, process resources, and thread pool state.

**Requires:** `admin` role (`Authorization: Bearer <token>`)

The response includes core metrics and any additional metrics from registered feature contributors.

**Core metrics (always present):**

- **gc:** Generation collection counts, server GC mode, total allocated bytes
- **memory:** Working set, GC heap, fragmentation, pinned objects, system memory (if available)
- **process:** Thread count, handle count, start time, uptime
- **threadPool:** Available threads, min/max configuration, pending work items

**Feature metrics (when registered):**

Additional features can contribute their own metrics sections (e.g., a chat feature might add queue depths and active sessions).

**Response structure:**

- `timestamp` — snapshot time
- Core metric sections (gc, memory, process, threadPool)
- Feature metric sections (alphabetically ordered for readability)
- `_errors` — only present if any contributor failed

*Note:* Per JSON specification, object property order is not guaranteed. The server produces a human-friendly ordering for readability, but clients must not rely on property order.

**Example response (abbreviated):**

```json
{
  "timestamp": "2025-12-25T10:06:14.265Z",
  "gc": {
    "gen0Collections": 3,
    "gen1Collections": 1,
    "gen2Collections": 0,
    "isServerGc": true,
    "totalAllocatedBytes": 17005296
  },
  "memory": {
    "managed": { "liveBytes": 10022008, "heapSizeBytes": 7248464 },
    "process": { "workingSetBytes": 125308928 },
    "system": { "totalPhysicalBytes": 34281783296 },
    "effective": { "limitBytes": 34281783296, "usageBytes": 125308928 }
  },
  "process": {
    "threadCount": 45,
    "handleCount": 713,
    "uptime": "00:01:24.007"
  },
  "threadPool": {
    "availableWorkerThreads": 32766,
    "pendingWorkItemCount": 0
  }
}
```

> For the complete schema and error codes, see the [OpenAPI documentation](../api/README.md).

All values represent a point-in-time snapshot captured at the `timestamp` field. For time-series analysis, poll this endpoint at regular intervals.

---

### Adding Custom Metrics Contributors

Features can contribute their own metrics by implementing `IMetricsContributor` and registering with a unique section name:

```csharp
// 1. Implement the interface in your feature
public sealed class MyFeatureMetricsContributor : IMetricsContributor
{
    public async Task<object> CollectAsync(CancellationToken cancellationToken)
    {
        return new
        {
            ItemsProcessed = 42,
            QueueDepth = 7
        };
    }
}

// 2. Register in your feature's ServiceRegistration
builder.AddMetricsContributor<MyFeatureMetricsContributor>("myfeature");
```

**Validation:** Section names are validated at registration time (fail-fast). Duplicate names or conflicts with core metrics will cause the application to fail to start with a clear error message.

**Error handling:** If a contributor throws during collection, its section is set to `null` and the error details appear in a separate `_errors` section. This keeps the schema stable for each section.

---

### `GET /api/v1/system/configuration`

Returns all registered configuration options with secrets automatically masked. Properties marked with `[Secret]` are replaced with `"*** (length N)"` — see [Secret Masking](#secret-masking) below.

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
| `MetricsContributorRegistry` | Singleton | Tracks registered feature metrics contributors and validates section names |
| `MetricsAggregator` | Singleton | Collects core metrics via factories and feature metrics via contributors |

Core metrics (GC, memory, process, thread pool) are collected directly via static factory classes in `LumaCore.Core.Diagnostics`, not through the contributor pattern.

The `OptionsTracker` used by this feature is registered automatically by `AddFeatureOptions<T>()` calls, not by the *System* feature itself.

---

## Pipeline Order

The *System* feature only registers endpoints and supporting services. The order of `AddSystemFeature()` in service registration does not matter — it should be called before any feature that wants to register its own metrics contributors.

`MapSystemFeature()` can be called in any order relative to other features.

---

## Related Features

- [*Auth*](auth.md) — Required for endpoint authorization; its `JwtOptions` are exposed via configuration endpoint

---

© 2025 LumaCoreTech • MIT License