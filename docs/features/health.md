# Health Feature (Liveness & Readiness Probes)

The *Health* feature provides endpoints for monitoring, orchestration systems, and the LumaCore Web UI. It enables container orchestrators, reverse proxies, uptime monitors, and the UI health indicator to verify that LumaCore is running, reachable, and operationally ready. The feature exposes three endpoints — a lightweight liveness probe, a component-level readiness probe, and the standard ASP.NET Core aggregated health check.

> [!NOTE]
> **Split Mapping:** The *Health* feature uses two mapping methods. Infrastructure probes (`/health`) are mapped directly to the application root for orchestrator compatibility. The versioned API endpoints (`/api/v1/health/live` and `/api/v1/health/ready`) are part of the versioned API surface and mapped via the central route group.

---

## Endpoints

The *Health* feature exposes three endpoints with different purposes:

| Endpoint | Purpose | Auth | HTTP Status |
|---|---|---|---|
| `GET /api/v1/health/live` | Liveness — is the process reachable? | Anonymous | Always 200 |
| `GET /api/v1/health/ready` | Readiness — is the backend operationally ready? | Anonymous | 200 (ready) / 503 (degraded) |
| `GET /health` | Orchestrator probe — aggregated ASP.NET Core health checks | Anonymous | 200 / 503 |

### `GET /api/v1/health/live`

Returns a minimal JSON payload (`ApiHealthLiveResponse`) indicating whether the backend is responsive. This endpoint is intentionally lightweight — it always returns successfully as long as the API process can handle requests. It performs no dependency checks.

This endpoint is anonymous — no authentication is required. This allows the Web UI and monitoring systems to check backend availability even before authentication is configured.

**Response:**

```json
{ "status": "ok" }
```

All responses are marked non-cacheable (`Cache-Control: no-store`).

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).

---

### `GET /api/v1/health/ready`

Returns a JSON payload (`ApiHealthReadyResponse`) that indicates whether the backend is operationally ready to handle requests. Unlike `/live` (pure connectivity check), this endpoint queries the `DatabaseInitializationStatus` singleton and reports actual operational readiness with **per-subsystem component detail**.

The Web UI uses this endpoint in a two-step probing sequence: first `/live` (is the backend reachable?), then `/ready` (is it fully operational?). This allows the UI to distinguish three states:

| UI State | Indicator | Meaning |
|---|---|---|
| Healthy | 🟢 Green | `/live` ok AND `/ready` returns `"ready"` |
| Not Ready | 🟠 Orange | `/live` ok BUT `/ready` returns `"degraded"` (HTTP 503) |
| Unhealthy | 🔴 Red | `/live` unreachable |

**Response (ready):** HTTP 200

```json
{
  "status": "ready",
  "components": {
    "database": { "status": "ready", "message": null }
  }
}
```

**Response (degraded):** HTTP 503

```json
{
  "status": "degraded",
  "components": {
    "database": { "status": "initializing", "message": "Database initialization is in progress." }
  }
}
```

The `components` dictionary is keyed by subsystem name. Currently, only `"database"` is reported. Additional subsystems (vector store, LLM backend) will be added as they are implemented.

**Component status values (database):**

| Status | Meaning |
|---|---|
| `"ready"` | Database is fully operational |
| `"initializing"` | Initialization has not started or is in progress |
| `"failed"` | Initialization or a runtime operation failed |
| `"disconnected"` | Database connection was lost at runtime |

The top-level `status` is `"ready"` when **all** components report `"ready"`, and `"degraded"` otherwise.

All responses are marked non-cacheable (`Cache-Control: no-store`).

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).

---

### `GET /health`

The standard ASP.NET Core health check endpoint for orchestrator probes. This endpoint aggregates all registered health checks and returns a simple status (`Healthy`, `Degraded`, or `Unhealthy`). Container orchestrators like Kubernetes typically use this endpoint to determine if the application is ready to receive traffic.

The following health checks are registered:

| Check Name | Tags | Description |
|---|---|---|
| `database-initialization` | `database`, `startup` | Reports whether database migrations and seeding completed successfully. Maps `DatabaseInitializationState` to `Healthy`/`Degraded`/`Unhealthy`. |

This endpoint is anonymous, unversioned, and mapped directly to the application root — container orchestrators expect it at a fixed, well-known path.

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).

---

## Configuration

The *Health* feature does not introduce additional configuration options. It registers the standard ASP.NET Core health check infrastructure and adds the `DatabaseInitializationHealthCheck` with tags `["database", "startup"]`.

The feature is registered via `builder.AddHealthFeature()` and mapped via:
- `api.MapHealthApiFeature()` — for `/api/v1/health/live` and `/api/v1/health/ready` (versioned API group)
- `app.MapHealthProbesFeature()` — for `/health` (application root)

---

## Registered Services

The *Health* feature registers the following services:

| Service | Lifetime | Description |
|---|---|---|
| `DatabaseInitializationHealthCheck` | Transient | ASP.NET Core `IHealthCheck` implementation that queries `DatabaseInitializationStatus` and maps the current `DatabaseInitializationState` to `HealthStatus.Healthy`, `Degraded`, or `Unhealthy`. |

The `DatabaseInitializationStatus` singleton itself is registered by the Data feature — the Health feature only consumes it.

---

## Pipeline Order

The *Health* feature registers endpoints only — no middleware. The order of `MapHealthProbesFeature()` and `MapHealthApiFeature()` relative to other features does not matter.

> [!NOTE]
> The `DatabaseNotReadyMiddleware` (registered by the Data feature) bypasses health endpoints — requests to `/health`, `/api/v1/health/live`, and `/api/v1/health/ready` are never rejected with 503 by the middleware. This ensures health probes remain available even when the database is unavailable.

---

## Typical Usage

A typical health monitoring setup works as follows:

1. Configure your orchestrator's **readiness probe** to poll `GET /health`. The endpoint returns `Healthy` only when all registered health checks (including database) pass.
2. Configure your orchestrator's **liveness probe** to poll `GET /api/v1/health/live`. This endpoint is always successful as long as the process is running.
3. The **Web UI** automatically performs two-step probing (`/live` → `/ready`) and displays a three-state health indicator (green/orange/red).
4. The **Status Page** displays per-subsystem component detail from the `/ready` endpoint, showing operators exactly which subsystem is healthy or degraded.
5. If readiness returns `Healthy` / `200`, the instance is added to load balancer rotation.
6. If liveness fails or times out, the orchestrator restarts the container.

> [!NOTE]
> Keep health endpoints fast and lightweight. Avoid exposing secrets or detailed error messages in health responses — log detailed diagnostics via the logging subsystem instead.

---

© 2025-2026 LumaCoreTech • MIT License
