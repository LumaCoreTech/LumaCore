# Auth Feature (JWT Authentication)

The *Auth* feature provides JWT-based authentication and identity introspection for the LumaCore HTTP API. It validates the JWT configuration at startup, configures bearer authentication, and issues access tokens for clients. Additionally, it provides helper endpoints that allow clients to inspect their current identity and token details — useful for debugging and building user interfaces.

> [!NOTE]
> Auth handles token issuance and validation, not user management. User accounts, passwords, and registration will be handled by the upcoming *UserStore* feature.

---

## Endpoints

The *Auth* feature exposes three endpoints: one for obtaining a token, and two for inspecting the current authentication state.

### `POST /api/v1/auth/login`

Authenticates a user and returns a JWT access token.

> [!NOTE]
> The current implementation uses a single hard-coded administrator account (`admin`). This is a bootstrap mechanism until a persistent user store is available.

On successful authentication, the endpoint returns a signed JWT. If authentication fails, the response does not reveal whether the username exists — this prevents user enumeration attacks.

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

All JWT-related settings are configured in `appsettings.json` (or via environment variables) under the `Jwt` section. All options are required — if any option is missing or invalid, LumaCore refuses to start rather than running with insecure defaults.

### Options

| Option | Required | Default | Validation | Description |
|--------|----------|---------|------------|-------------|
| `Issuer` | Yes | — | Non-empty string | Token issuer claim (`iss`) |
| `Audience` | Yes | — | Non-empty string | Token audience claim (`aud`) |
| `SigningKey` | Yes | — | At least 32 characters | Secret key for signing tokens |
| `AccessTokenLifetimeMinutes` | No | `60` | 1–1440 | Token validity duration in minutes |

### Example: `appsettings.json`

```json
{
  "Jwt": {
    "Issuer": "LumaCore",
    "Audience": "LumaCore",
    "SigningKey": "your-secret-key-at-least-32-characters-long",
    "AccessTokenLifetimeMinutes": 60
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
```

The feature is registered via `builder.AddAuthFeature()` and mapped to the versioned API group in `Program.cs`.

---

## Registered Services

The *Auth* feature registers the following services for dependency injection:

| Service | Lifetime | Description |
|---------|----------|-------------|
| `IJwtTokenFactory` | Singleton | Creates signed JWT tokens |

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

A typical authentication flow works as follows:

1. Client sends credentials to `POST /api/v1/auth/login`.
2. On success, the API returns a JWT in the `accessToken` field.
3. Client stores the token (typically in memory or secure storage).
4. For subsequent requests, client includes the token as `Authorization: Bearer <token>`.
5. Protected endpoints validate the token; invalid or missing tokens return `401 Unauthorized`.
6. Client can use `GET /api/v1/auth/whoami` to display the current user in the UI.
7. Client can use `GET /api/v1/auth/introspect` for debugging token issues.

> [!WARNING]
> Never store JWTs in LocalStorage — it is vulnerable to XSS attacks. Use HttpOnly cookies or secure in-memory storage for production applications.

Once a proper user store and role management are implemented, the bootstrap admin flow will be replaced with persistent accounts and stronger credential management.

---

## Related Features

- [*Admin*](admin.md) — Requires authentication; displays JWT configuration in status

---

© 2025 LumaCoreTech • MIT License