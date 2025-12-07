# Features Documentation

This section documents all implemented LumaCore features.

Each feature has its own documentation explaining:
- What it does and why it exists
- Configuration options
- Registered services and pipeline order
- Typical usage

---

## Feature Overview

The following table lists all implemented features, grouped by category and sorted alphabetically within each group. Infrastructure features run as middleware for every request; API features expose endpoints.

**Request Pipeline:**

```
Request → ProxyHeaders → HTTPS → SecurityHeaders → CORS → Auth → Routing → Endpoint
```

| Feature | Category | Purpose |
|---------|----------|---------|
| [CORS](cors.md) | Infrastructure | Cross-origin request policies with credential support |
| [HttpsRedirection](https-redirection.md) | Infrastructure | Automatic HTTP to HTTPS redirect |
| [ProxyHeaders](proxy-headers.md) | Infrastructure | X-Forwarded-* header processing for reverse proxy setups |
| [SecurityHeaders](security-headers.md) | Infrastructure | HSTS, CSP, X-Frame-Options, and other security headers |
| [Admin](admin.md) | API | Administrative status endpoint with runtime diagnostics |
| [Auth](auth.md) | API | JWT authentication with login, introspection, and identity endpoints |
| [Health](health.md) | API | Liveness and readiness probes for orchestrators |

---

## Next Steps

👉 **[Architecture Overview](../architecture/README.md)** - Understand the feature-based architecture  
👉 **[Feature Pattern](../architecture/feature-pattern.md)** - Learn how features are structured

---

© 2025 LumaCoreTech • MIT License
