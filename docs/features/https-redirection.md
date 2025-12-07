# HttpsRedirection Feature (HTTP to HTTPS Redirect)

The *HttpsRedirection* feature automatically redirects HTTP requests to HTTPS, ensuring all traffic uses encrypted connections. This is useful when LumaCore is directly exposed to the internet without a reverse proxy. Most production deployments use a reverse proxy for TLS termination, so this feature is disabled by default — enable it only when LumaCore handles HTTPS directly.

---

## Configuration

All settings are configured in `appsettings.json` (or via environment variables) under the `HttpsRedirection` section.

### Options

| Option | Required | Default | Validation | Description |
|--------|----------|---------|------------|-------------|
| `Enabled` | No | `false` | — | Whether HTTP requests are redirected to HTTPS |
| `HttpsPort` | No | `null` | 1–65535 | HTTPS port to redirect to (auto-detect if null) |

### Example: `appsettings.json`

```json
{
  "HttpsRedirection": {
    "Enabled": true,
    "HttpsPort": 443
  }
}
```

### Environment Variables

Options can also be set via environment variables with the `HttpsRedirection__` prefix:

```text
HttpsRedirection__Enabled=true
HttpsRedirection__HttpsPort=443
```

The feature is registered via `builder.AddHttpsRedirectionFeature()` and `app.UseHttpsRedirectionFeature()` in `Program.cs`.

---

## Registered Services

The *HttpsRedirection* feature does not register any injectable services. It configures the ASP.NET Core HTTPS redirection middleware internally.

---

## Pipeline Order

The *HttpsRedirection* feature registers middleware only — no endpoints. The order of `UseHttpsRedirectionFeature()` matters: it should run early in the pipeline, but **after** proxy headers. When running behind a reverse proxy, the `X-Forwarded-Proto` header indicates the original protocol — proxy headers must be processed first so HTTPS redirection knows whether the original request was already HTTPS.

---

## Typical Usage

A typical HTTPS redirection setup works as follows:

1. Determine if HTTPS redirection is needed — only if LumaCore handles TLS directly.
2. If running behind a reverse proxy (Caddy, nginx, Traefik), keep `Enabled` as `false` and let the proxy handle redirection.
3. If LumaCore is directly exposed, set `Enabled` to `true`.
4. Set `HttpsPort` only if using a non-standard HTTPS port.

> [!NOTE]
> When running behind a reverse proxy that terminates TLS, configure the *ProxyHeaders* feature so LumaCore correctly identifies the original protocol. The proxy should handle HTTP-to-HTTPS redirection, not LumaCore.

---

## Related Features

- [*ProxyHeaders*](proxy-headers.md) — Must run before HTTPS redirection to detect original protocol behind proxies
- [*SecurityHeaders*](security-headers.md) — HSTS headers (complements HTTPS redirection)

---

© 2025 LumaCoreTech • MIT License
