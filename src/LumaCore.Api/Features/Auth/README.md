# LumaCore API – Authentication & JWT Feature

This document describes how authentication is implemented in the LumaCore API,
why certain decisions were made, and how to configure the JWT-based auth layer.

It is intended for developers and operators who maintain or extend the system.

---

## High-level Overview

The LumaCore API uses **JWT (JSON Web Tokens)** for stateless authentication.

- Tokens are issued by the `/auth/login` endpoint.
- Tokens are validated by ASP.NET Core's JWT bearer middleware.
- Protected endpoints require a valid JWT (via `RequireAuthorization()` or policies).
- Initial implementation uses a single hard-coded admin account for bootstrapping.

Later iterations are expected to:

- replace the hard-coded admin with a database-backed user store,
- introduce multiple roles and policies,
- add refresh tokens or external identity providers (IdP) if needed.

---

## Components

### AuthFeature

Location: `LumaCore.Api/Features/Auth/AuthFeature.cs`

Responsibilities:

- Bind and validate `JwtOptions` from configuration.
- Configure `AddAuthentication().AddJwtBearer(...)`.
- Register `AddAuthorization(...)` and authorization policies.
- Register `IJwtTokenFactory`.
- Map the `/auth/login` endpoint.

This feature is wired from `Program.Services.cs` and `Program.Pipeline.cs`:

```csharp
// Program.Services.cs
builder.AddAuthFeature();

// Program.Pipeline.cs
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthFeature();
```

### JwtOptions

Location: `LumaCore.Api/Features/Auth/JwtOptions.cs`

Bound from the `Jwt` configuration section:

- `Issuer`
- `Audience`
- `SigningKey`
- `AccessTokenLifetimeMinutes`

Validated via:

```csharp
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### JwtTokenFactory

Location: `LumaCore.Api/Features/Auth/JwtTokenFactory.cs`

Creates signed JWTs:

- includes standard JWT claims: `sub`, `jti`, `iss`, `aud`, `nbf`, `exp`
- accepts additional claims (roles, name)

---

## /auth/login Endpoint

Request:

```json
{
  "username": "admin",
  "password": "changeme"
}
```

Response (200 OK):

```json
{
  "accessToken": "<JWT>"
}
```

Invalid login → `401 Unauthorized`.

This is intentionally generic to avoid username enumeration.

---

## Token Content (Claims Schema)

Current tokens contain:

- `sub` (subject, currently username)
- `name`
- `role`
- `jti`
- `iss`
- `aud`
- `nbf` / `exp`

Example decoded payload:

```json
{
  "sub": "admin",
  "name": "admin",
  "role": "admin",
  "jti": "bafc4dd8c9ac4fecb629595b98acf537",
  "nbf": 1763671758,
  "exp": 1763675358,
  "iss": "LumaCore",
  "aud": "LumaCore-AdminUi"
}
```

---

## Authorization & Policies

Example policy:

```csharp
options.AddPolicy("AdminOnly", policy =>
{
    policy.RequireRole("admin");
});
```

Usage:

```csharp
app.MapGroup("/admin")
   .RequireAuthorization("AdminOnly");
```

---

## Configuration

### Development

Use local settings:

```json
"Jwt": {
  "Issuer": "LumaCore",
  "Audience": "LumaCore-AdminUi",
  "SigningKey": "DEV-ONLY-CHANGE-THIS",
  "AccessTokenLifetimeMinutes": 60
}
```

### Production

Use environment variables:

- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `Jwt__AccessTokenLifetimeMinutes`

Missing configuration → fails at startup with detailed exceptions.

---

## Security Notes

- SigningKey is sensitive → never commit to source control.
- Changing signing settings invalidates all existing tokens.
- Hard-coded admin is temporary and must be replaced.

---

## Future Work

- DB-backed user store
- Multiple roles & policies
- Refresh tokens
- External IdP (OIDC)
- Token introspection endpoint for dev

---

**LumaCore – A home for AI personas.** 