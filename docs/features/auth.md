# Auth Feature (JWT Authentication)

The *Auth* feature provides JWT-based authentication, token revocation, and identity introspection for the LumaCore HTTP API. It validates the JWT configuration at startup, configures bearer authentication, issues access tokens, and supports dual transport — browser clients receive an `HttpOnly` cookie while API clients use the `Authorization: Bearer` header. Additionally, it provides helper endpoints that allow clients to inspect their current identity and token details — useful for debugging and building user interfaces.

> [!NOTE]
> Auth handles token issuance, revocation, and validation, not user management. User accounts, passwords, and registration will be handled by the upcoming *UserStore* feature.

---

## Endpoints

The *Auth* feature exposes four endpoints: one for obtaining a token, one for logout with token revocation, and two for inspecting the current authentication state.

### `POST /api/v1/auth/login`

Authenticates a user and returns a JWT access token.

> [!NOTE]
> The current implementation uses a single hard-coded administrator account (`admin`). This is a bootstrap mechanism until a persistent user store is available.

On successful authentication, the endpoint returns a signed JWT. When cookie transport is enabled (default), an `HttpOnly` cookie is also set for browser clients — eliminating the need to store the token in JavaScript-accessible storage and mitigating XSS-based token theft. The request supports a `rememberMe` flag: when `true`, the cookie is persistent with an explicit expiry matching the token lifetime; when `false` (default), the cookie is session-scoped and cleared when the browser closes.

If authentication fails, the response does not reveal whether the username exists — this prevents user enumeration attacks.

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).

---

### `POST /api/v1/auth/logout`

Revokes the current access token and clears the authentication cookie.

Token revocation records the token's `jti` (JWT ID) in the `RevokedJwts` database table. On the same instance, the revoked token is rejected immediately (the local cache is evicted on write). In multi-instance deployments, other instances recognize the revocation after at most `CacheDurationSeconds` (default: 5 s). The `HttpOnly` cookie is also cleared so browser clients are fully logged out.

Both browser and API clients should call this endpoint to invalidate their token. API clients using `Authorization: Bearer` still benefit because the revoked `jti` is checked in the authentication pipeline.

**Requires:** an authenticated user

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).

---

### `GET /api/v1/auth/whoami`

Returns basic information about the currently authenticated user. This endpoint works with any authentication method supported by LumaCore — it reads from the underlying authentication context, not from JWT-specific claims.

**Requires:** an authenticated user

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).

---

### `GET /api/v1/auth/introspect`

Returns diagnostic information about the current authentication state. When using JWT authentication, the response includes token-specific metadata such as the validity window, remaining lifetime, and configured issuer and audience. This endpoint is primarily intended for development and debugging.

**Requires:** a valid JWT (`Authorization: Bearer <token>`)

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).

---

## Configuration

All JWT-related settings are configured in `appsettings.json` (or via environment variables) under the `Jwt` section. Core options are required — if any is missing or invalid, LumaCore refuses to start rather than running with insecure defaults. Cookie and token revocation options have sensible defaults and are optional.

### Core Options

| Option | Required | Default | Validation | Description |
|--------|----------|---------|------------|-------------|
| `Issuer` | Yes | — | Non-empty string | Token issuer claim (`iss`) |
| `Audience` | Yes | — | Non-empty string | Token audience claim (`aud`) |
| `SigningKey` | Yes | — | At least 32 characters | Secret key for signing tokens |
| `AccessTokenLifetimeMinutes` | No | `60` | 1–1440 | Token validity duration in minutes |

### Cookie Options (`Jwt:Cookie`)

Controls how access tokens are transported via `HttpOnly` cookies for browser-based clients (Blazor WASM). API clients are unaffected — they continue to use the `Authorization: Bearer` header.

| Option | Required | Default | Validation | Description |
|--------|----------|---------|------------|-------------|
| `Enabled` | No | `true` | — | Enable cookie-based token transport |
| `Name` | No | `lumacore_access` | Non-empty string | Cookie name |
| `SecureOnly` | No | `true` | — | Require HTTPS for cookie transmission |
| `Path` | No | `/api` | Non-empty string | URL path scope (restricts cookie to API routes) |
| `Domain` | No | `null` | — | Domain restriction (set for cross-subdomain setups) |

> [!NOTE]
> `SameSite=Strict` is hardcoded and not configurable — this provides CSRF protection for LumaCore's SPA architecture. Combined with the CORS policy, this eliminates the most common CSRF attack vectors without requiring anti-forgery tokens.

### Token Revocation Options (`Jwt:TokenRevocation`)

Controls the caching behavior of the token revocation blacklist check.

| Option | Required | Default | Validation | Description |
|--------|----------|---------|------------|-------------|
| `CacheDurationSeconds` | No | `5` | 0–60 | How long a "not revoked" result is cached in memory |

Setting this to `0` disables caching entirely — every authenticated request queries the database. In single-instance deployments the effective propagation delay is zero regardless of this setting, because the cache is evicted when a local revocation occurs. The configured duration only matters in multi-instance deployments where a token is revoked on instance A but the next request hits instance B.

### Example: `appsettings.json`

```json
{
  "Jwt": {
    "Issuer": "LumaCore",
    "Audience": "LumaCore",
    "SigningKey": "your-secret-key-at-least-32-characters-long",
    "AccessTokenLifetimeMinutes": 60,
    "Cookie": {
      "Enabled": true,
      "Name": "lumacore_access",
      "SecureOnly": true,
      "Path": "/api"
    },
    "TokenRevocation": {
      "CacheDurationSeconds": 5
    }
  }
}
```

### Environment Variables

Options can also be set via environment variables with the `Jwt__` prefix:

```text
Jwt__Issuer=LumaCore
Jwt__Audience=LumaCore
Jwt__SigningKey=your-secret-key-at-least-32-characters-long
Jwt__AccessTokenLifetimeMinutes=60
Jwt__Cookie__Enabled=true
Jwt__Cookie__SecureOnly=true
Jwt__TokenRevocation__CacheDurationSeconds=5
```

The feature is registered via `builder.AddAuthFeature()` and mapped to the versioned API group in `Program.cs`.

---

## Registered Services

The *Auth* feature registers the following services for dependency injection:

| Service | Lifetime | Description |
|---------|----------|-------------|
| `IJwtTokenFactory` | Singleton | Creates signed JWT tokens |
| `ITokenRevocationService` | Scoped | Checks and records token revocations against the `RevokedJwts` database table with negative-only memory cache |

---

## Pipeline Order

The *Auth* feature registers authentication middleware and endpoints. The order of `AddAuthFeature()` in service registration does not matter, and `MapAuthFeature()` can be called in any order relative to other features.

Note that `UseAuthentication()` and `UseAuthorization()` must come before endpoint mapping in the middleware pipeline — this is handled automatically by the ASP.NET Core framework.

---

## Startup Validation

The application validates at startup that every endpoint has an explicit authorization declaration — either `RequireAuthorization()` or `AllowAnonymous()`. Endpoints without explicit authorization cause startup failure.

This fail-fast behavior prevents accidental exposure of unprotected endpoints.

---

## Typical Usage

LumaCore supports two token transport mechanisms — browser clients use `HttpOnly` cookies (set automatically), while API clients use the `Authorization: Bearer` header. If both are present, the Bearer header takes priority.

### Browser Clients (Blazor WASM)

1. Client sends credentials (with optional `rememberMe` flag) to `POST /api/v1/auth/login`.
2. On success, the API sets an `HttpOnly` cookie and returns the token in the response body.
3. The browser automatically includes the cookie on subsequent API requests — no manual token storage needed.
4. Client can use `GET /api/v1/auth/whoami` to display the current user in the UI.
5. To log out, client calls `POST /api/v1/auth/logout` — the token is revoked and the cookie is cleared.

### API Clients (Companion AIs, CLI Tools)

1. Client sends credentials to `POST /api/v1/auth/login`.
2. On success, the API returns a JWT in the `accessToken` field.
3. Client stores the token in memory and includes it as `Authorization: Bearer <token>` on subsequent requests.
4. Protected endpoints validate the token; invalid, expired, or revoked tokens return `401 Unauthorized`.
5. To log out, client calls `POST /api/v1/auth/logout` — the token is immediately revoked.
6. Client can use `GET /api/v1/auth/introspect` for debugging token issues.

> [!WARNING]
> Never store JWTs in `localStorage` — it is vulnerable to XSS attacks. Browser clients should rely on the `HttpOnly` cookie transport. API clients should store tokens in memory only.

Once a proper user store and role management are implemented, the bootstrap admin flow will be replaced with persistent accounts and stronger credential management.

---

## Related Features

- [*System*](system.md) — Exposes JWT configuration (sanitized) via configuration endpoint

---

© 2025-2026 LumaCoreTech • MIT License
