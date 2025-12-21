# Features Documentation

This section documents all implemented LumaCore features.

Each feature has its own documentation explaining:
- What it does and why it exists
- Configuration options
- Registered services and pipeline order
- Typical usage

---

## Feature Overview

The following sections list all implemented features by category:

- **Foundation** – Core capabilities that other features depend on (versioning, validation, documentation)
- **Infrastructure** – Middleware that runs for every request (security headers, CORS, proxies)
- **API** – Features that expose HTTP endpoints

**Request Pipeline:**

```
  Request
→ ProxyHeaders
→ ExceptionHandler¹
→ ErrorHandling
→ HTTPS
→ SecurityHeaders
→ Logging²
→ CORS
→ StaticFiles
→ Routing
→ Auth
→ Endpoint
```

¹ Production only (Development uses DeveloperExceptionPage instead)  
² Development adds OpenAPI + SwaggerUI after Logging

### Foundation

| Feature | Purpose |
|---------|---------|
| [ApiVersioning](api-versioning.md) | URL segment versioning with startup validation |
| [OpenAPI](openapi.md) | OpenAPI 3.1 specification generation per API version |
| [Validation](validation.md) | DataAnnotations request validation with ProblemDetails |

### Infrastructure

| Feature | Purpose |
|---------|---------|
| [CORS](cors.md) | Cross-origin request policies with credential support |
| [ErrorHandling](error-handling.md) | RFC 7807 ProblemDetails with URN-based error types |
| [HttpsRedirection](https-redirection.md) | Automatic HTTP to HTTPS redirect |
| [ProxyHeaders](proxy-headers.md) | X-Forwarded-* header processing for reverse proxy setups |
| [SecurityHeaders](security-headers.md) | HSTS, CSP, X-Frame-Options, and other security headers |

### API

| Feature | Purpose |
|---------|---------|
| [Admin](admin.md) | Administrative status endpoint with runtime diagnostics |
| [Auth](auth.md) | JWT authentication with login, introspection, and identity endpoints |
| [Health](health.md) | Liveness and readiness probes for orchestrators |

---

## Next Steps

👉 **[Architecture Overview](../architecture/README.md)** — Understand the feature-based architecture  
👉 **[Feature Pattern](../architecture/feature-pattern.md)** — Learn how features are structured

---

© 2025 LumaCoreTech • MIT License