# Admin Feature (Administration & Status)

The *Admin* feature provides endpoints intended for operational insight and administrative access. It allows administrators to verify that a LumaCore instance is running correctly, check its configuration, and monitor basic runtime metrics. Admin operations are security-sensitive and must not be exposed without proper authentication and role enforcement.

> [!WARNING]
> All admin endpoints require the `admin` role. Never expose admin endpoints publicly or in API documentation intended for regular users.

---

## Endpoints

The *Admin* feature currently exposes a single endpoint for retrieving instance status. Additional administrative endpoints will be added as the platform evolves.

### `GET /api/admin/status`

Returns high-level status information about the running LumaCore instance. This endpoint is useful for health dashboards, deployment verification, and troubleshooting configuration issues. For security reasons, the signing key is never exposed — only a masked representation is returned.

**Requires:** a valid JWT with the `admin` role (`Authorization: Bearer <token>`)

> For request/response schemas and examples, see the [OpenAPI documentation](../api/openapi.md).

---

## Configuration

The *Admin* feature does not introduce additional configuration options. It relies on the *Auth* feature for JWT bearer authentication and role-based access control — any user with the `admin` role can access the admin endpoints.

The feature is registered via `builder.AddAdminFeature()` and `app.MapAdminFeature()` in `Program.cs`.

---

## Registered Services

The *Admin* feature does not register any injectable services.

---

## Pipeline Order

The *Admin* feature registers endpoints only — no middleware. Since it has no middleware components, the order of `MapAdminFeature()` relative to other features does not matter.

---

## Typical Usage

A typical administrative workflow works as follows:

1. Administrator obtains a JWT via the *Auth* feature (`POST /api/auth/login`).
2. Administrator calls `GET /api/admin/status` with the token.
3. The response confirms that LumaCore is running and configured correctly.
4. Administrator verifies environment, version, and JWT settings match expectations.

The status response is useful for deployment verification, health dashboards, and troubleshooting configuration issues. As the platform evolves, more administrative endpoints will be added under the `/admin` prefix, reusing the same authentication and authorization model.

---

## Related Features

- [*Auth*](auth.md) — Required for authentication; JWT configuration is displayed in status

---

© 2025 LumaCoreTech • MIT License
