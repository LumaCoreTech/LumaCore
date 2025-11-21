# LumaCore API – Admin Feature

This document describes the purpose, design, and behavior of the **Admin Feature**
(`Features/Admin/AdminFeature.cs`) of the LumaCore API.  
It explains what the `/admin` endpoints do, what information they expose, why they
exist, and how they integrate with JWT-based authentication.

The Admin Feature is intended for developers and operators who need insight into
a running LumaCore instance.

---

## Purpose

The Admin Feature provides authenticated users (typically administrators or operators)
with introspection and diagnostics endpoints, such as:

- `/admin/whoami` – shows the current authenticated principal  
- `/admin/status` – shows environment, version, runtime status, masked JWT info

Future expansions may include:

- model reload triggers  
- system health details  
- storage and cache stats  
- configuration inspection  
- job scheduling controls  

Admin endpoints are **never available anonymously**.

---

## Endpoint Group `/admin`

The AdminFeature registers:

```csharp
var admin = app.MapGroup("/admin")
    .RequireAuthorization("AdminOnly")
    .WithTags("Admin");
```

This ensures:

- all `/admin/*` routes require a valid JWT  
- the user must satisfy the `AdminOnly` policy  
- Swagger groups endpoints under an “Admin” tag  

The `AdminOnly` policy is defined in `AuthFeature`:

```csharp
options.AddPolicy("AdminOnly", policy =>
{
    policy.RequireRole("admin");
});
```

Only users with role `"admin"` can access admin endpoints.

---

## `/admin/whoami`

### Purpose

Provides introspection of the **current authenticated JWT principal**.

Useful for:

- verifying authentication  
- checking roles  
- debugging claims  
- confirming correct token issuance  
- development & testing  

### Response Model

```csharp
public sealed record AdminWhoAmIResponse(
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AdminClaimItem> Claims);

public sealed record AdminClaimItem(string Type, string Value);
```

### Example Response

```json
{
  "name": "admin",
  "roles": ["admin"],
  "claims": [
    { "type": "sub", "value": "admin" },
    { "type": "name", "value": "admin" },
    { "type": "role", "value": "admin" },
    ...
  ]
}
```

---

## `/admin/status`

### Purpose

Returns non-sensitive diagnostic data about the running LumaCore API.

Includes:

- configured environment  
- API version  
- machine name  
- current UTC time  
- JWT configuration state (issuer, audience, masked signing key)

Sensitive values such as `SigningKey` are **never exposed**.

### Response Model

```csharp
public sealed record AdminStatusResponse(
    string? Environment,
    string? ApiVersion,
    string MachineName,
    DateTime UtcNow,
    AdminJwtStatusInfo Jwt);

public sealed record AdminJwtStatusInfo(
    bool Configured,
    string? Issuer,
    string? Audience,
    string? SigningKey);
```

### Example

```json
{
  "environment": "Development",
  "apiVersion": "1.0.0",
  "machineName": "LUMACORE-DEV",
  "utcNow": "2025-11-20T12:34:56Z",
  "jwt": {
    "configured": true,
    "issuer": "LumaCore",
    "audience": "LumaCore-AdminUi",
    "signingKey": "*** (length 64)"
  }
}
```

### Masking Behavior

The signing key is represented as:

```
*** (length 64)
```

This confirms:

- a key is configured  
- its approximate length  
- without leaking the raw key  

---

## Integration With Authentication

The `/admin` feature depends on:

- JWT authentication configured in `AuthFeature`
- Authorization policies defined in `AuthFeature`
- `IJwtTokenFactory` for issuing tokens

Tokens must include:

- a valid subject (`sub`)
- `role: admin` to satisfy the `AdminOnly` policy

---

## Design Principles

1. **Security-first**  
   Admin endpoints are protected by both authentication and role-based authorization.

2. **Zero sensitive data leakage**  
   Secrets (like SigningKey) are masked, never returned.

3. **Developer-friendly diagnostics**  
   `/admin/whoami` is invaluable for debugging token flow.  
   `/admin/status` helps verify configuration and runtime state.

4. **Extensible architecture**  
   Additional admin capabilities can be added cleanly within this feature.

---

## Future Extensions

Potential enhancements include:

- `/admin/reload-models` – trigger AI model reload  
- `/admin/memory/stats` – view embedded memory engine metrics  
- `/admin/vector/status` – expose vector DB state  
- `/admin/logs/tail` – stream logs  
- `/admin/system/restart` – graceful system restart hooks  
- `/admin/users/*` – user management once DB exists

This feature is intentionally lightweight now, but will grow as LumaCore matures.

---

## Notes

- `/admin` endpoints must remain authenticated **always**.  
- Swagger will only include them when authorized via the “Authorize” button.  
- A missing or malformed JWT results in `401 Unauthorized`.  
- Insufficient role results in `403 Forbidden`.

---

**LumaCore – A home for AI personas.**  
