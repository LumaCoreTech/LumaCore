# Health Feature (Liveness & Readiness Probes)

The *Health* feature provides lightweight endpoints for monitoring and orchestration systems. It enables container orchestrators, reverse proxies, and uptime monitors to verify that LumaCore is running and responsive. The feature is designed to be minimal but extensible — additional checks for databases, vector stores, or LLM backends can be added over time without breaking existing consumers.

> [!NOTE]
> **Split Mapping:** The *Health* feature uses two mapping methods. Infrastructure probes (`/health`) are mapped directly to the application root for orchestrator compatibility. The liveness API endpoint (`/api/v1/health/live`) is part of the versioned API surface and mapped via the central route group.

---

## Endpoints

The *Health* feature exposes two endpoints: one for readiness checks (standard ASP.NET Core) and one for lightweight liveness checks.

### `GET /health`

The standard ASP.NET Core health check endpoint for readiness probes. This endpoint aggregates all registered health checks and returns a simple status. Container orchestrators like Kubernetes typically use this endpoint to determine if the application is ready to receive traffic.

Currently, no custom health checks are registered, so the endpoint always returns `Healthy` as long as the application is running. As the platform evolves, checks for databases, vector stores, and LLM backends will be added here.

This endpoint is anonymous — no authentication is required.

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).

---

### `GET /api/v1/health/live`

Returns a minimal JSON payload indicating whether the backend is responsive. This endpoint is intentionally lightweight and always returns successfully as long as the API process can handle requests.

This endpoint is anonymous — no authentication is required. This allows monitoring systems and the Web UI to check backend availability even before authentication is configured.

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).

---

## Configuration

The *Health* feature does not introduce additional configuration options. It registers the standard ASP.NET Core health check infrastructure, which can be extended with custom health checks in future versions.

The feature is registered via `builder.AddHealthFeature()` and mapped via `app.MapHealthProbesFeature()` (for `/health`) and `app.MapHealthApiFeature()` (for `/api/v1/health/live`) in `Program.cs`.

---

## Registered Services

The *Health* feature does not register any injectable services. It configures the ASP.NET Core health check infrastructure internally, but this is not exposed as a consumer-facing service.

---

## Pipeline Order

The *Health* feature registers endpoints only — no middleware. The order of `MapHealthProbesFeature()` and `MapHealthApiFeature()` relative to other features does not matter.

---

## Typical Usage

A typical health monitoring setup works as follows:

1. Configure your orchestrator's **readiness probe** to poll `GET /health`.
2. Configure your orchestrator's **liveness probe** to poll `GET /api/v1/health/live`.
3. If readiness returns `Healthy`, the instance is added to load balancer rotation.
4. If liveness fails or times out, the orchestrator restarts the container.
5. External monitoring systems can use either endpoint for uptime checks.

> [!NOTE]
> Keep health endpoints fast and lightweight. Avoid exposing secrets or detailed error messages in health responses — log detailed diagnostics via the logging subsystem instead.

---

© 2025 LumaCoreTech • MIT License
