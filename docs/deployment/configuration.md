# Configuration Guide

LumaCore uses the standard ASP.NET Core configuration model: JSON files, environment variables, and strongly-typed options classes with validation at startup.

---

## Why We Document in `appsettings.json`

Instead of maintaining separate documentation that inevitably drifts out of sync, **all configuration options are documented directly in [`appsettings.json`](../../src/LumaCore.Api/appsettings.json)** with inline comments.

This approach ensures:
- **One source of truth** — The config file *is* the documentation
- **Always accurate** — Comments live next to the code they describe
- **Easy to find** — No jumping between files to understand an option

Open the file, read the comments, adjust the values.

---

## Quick Reference

→ **[`appsettings.json`](../../src/LumaCore.Api/appsettings.json)** — All options with inline documentation

→ **[`appsettings.Development.json`](../../src/LumaCore.Api/appsettings.Development.json)** — Development overrides

→ **[`appsettings.Production.json`](../../src/LumaCore.Api/appsettings.Production.json)** — Production overrides

---

## Configuration Sections at a Glance

| Section | Purpose |
|---------|---------|
| `Jwt` | Token authentication (issuer, audience, lifetime) |
| `Kestrel` | Server endpoints and limits |
| `LumaCore` | Application metadata (environment, version, Swagger) |
| `HttpsRedirection` | HTTPS redirection settings |
| `ProxyHeaders` | Reverse proxy support (X-Forwarded-*) |
| `Cors` | Cross-origin resource sharing |
| `SecurityHeaders` | HTTP security headers (HSTS, CSP, X-Frame-Options) |
| `Serilog` | Structured logging configuration |

---

## Key Points

### Secrets via Environment Variables

**Never commit secrets to source control.** The JWT signing key must be provided via environment variable:

```bash
export Jwt__SigningKey="your-long-random-secret-min-32-chars"
```

Generate a secure key:
```bash
openssl rand -base64 32
```

All other JWT settings (Issuer, Audience, Lifetime) can safely live in `appsettings.json`.

### Environment Selection

LumaCore uses the standard ASP.NET Core environment mechanism:

```bash
# Development (verbose logging, Swagger enabled, relaxed CORS)
export ASPNETCORE_ENVIRONMENT=Development

# Production (minimal logging, Swagger disabled)
export ASPNETCORE_ENVIRONMENT=Production
```

The environment determines which `appsettings.{Environment}.json` file is loaded.

### Development vs Production

| Aspect | Development | Production |
|--------|-------------|------------|
| Logging | Debug level | Information level |
| Swagger | Enabled | Disabled |
| CORS | Open (`*`) | Restricted |
| HSTS | Disabled | Enable when HTTPS ready |
| JWT SigningKey | Dev key in config | **Must use env variable** |

---

## Configuration Merging

ASP.NET Core merges configuration from multiple sources (later overrides earlier):

1. `appsettings.json` — Base configuration
2. `appsettings.{Environment}.json` — Environment-specific overrides
3. Environment variables — Secrets and deployment-specific values
4. Command-line arguments — Runtime overrides

Environment variables use `__` as separator: `Jwt__SigningKey`, `Kestrel__Endpoints__Http__Url`

---

## Validation at Startup

LumaCore validates configuration at startup. If required values are missing or invalid, the application fails fast with a clear error message:

```
Jwt:SigningKey must be at least 32 characters long.
```

This prevents runtime surprises from misconfiguration.

---

## Related Documentation

- [Docker Deployment](docker.md) — Container setup and deployment
- [Status & Roadmap](../status.md) — Current implementation status