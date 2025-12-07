# ProxyHeaders Feature (Reverse Proxy Integration)

The *ProxyHeaders* feature handles `X-Forwarded-*` headers from reverse proxies and load balancers to ensure LumaCore correctly identifies client IP addresses, protocols (HTTP/HTTPS), and hostnames when deployed behind a proxy. Without this feature, LumaCore would see the proxy's IP address instead of the actual client's IP and might generate incorrect URLs in responses. The feature is disabled by default for security — enable it only when running behind a reverse proxy.

---

## Configuration

All settings are configured in `appsettings.json` (or via environment variables) under the `ProxyHeaders` section. The `Mode` option determines how forwarded headers are processed.

### Modes

The feature supports three modes to match different deployment scenarios:

**Disabled** (default) — No forwarded headers are processed. Use when LumaCore is accessed directly without any reverse proxy, or during local development.

**Cloud** — Trust all forwarded headers. Use when deploying to managed cloud platforms (Azure App Service, AWS Elastic Beanstalk, Google Cloud Run) where the platform controls the reverse proxy infrastructure and guarantees that only its managed proxies can set these headers.

**SelfManaged** — Only trust headers from explicitly configured proxy IPs or networks. Use when running behind self-hosted reverse proxies (nginx, Caddy, Traefik) where you control the proxy infrastructure.

> [!WARNING]
> Only use `Cloud` mode if your application is guaranteed to receive traffic exclusively through the platform's managed proxy. If direct public access is possible, use `SelfManaged` mode instead to prevent header spoofing attacks.

### Options

| Option | Required | Default | Validation | Description |
|--------|----------|---------|------------|-------------|
| `Mode` | No | `Disabled` | `Disabled`, `Cloud`, `SelfManaged` | How forwarded headers are processed |
| `ForwardLimit` | No | `1` | ≥ 1 | Maximum proxy hops to consider |
| `TrustedProxies` | If SelfManaged | `[]` | Valid IP addresses | Proxy IPs allowed to set headers |
| `TrustedNetworks` | If SelfManaged | `[]` | Valid CIDR notation | Network ranges allowed to set headers |

> [!WARNING]
> In `SelfManaged` mode, at least one `TrustedProxies` or `TrustedNetworks` entry is required. Without explicit trust configuration, LumaCore refuses to start to prevent security vulnerabilities.

> [!WARNING]
> Set `ForwardLimit` to the actual number of proxies in your infrastructure. Setting it too high allows header spoofing attacks where attackers prepend fake IP addresses.

### Example: `appsettings.json`

```json
{
  "ProxyHeaders": {
    "Mode": "SelfManaged",
    "ForwardLimit": 1,
    "TrustedProxies": ["10.0.0.100"],
    "TrustedNetworks": ["172.20.0.0/16"]
  }
}
```

### Environment Variables

Options can also be set via environment variables with the `ProxyHeaders__` prefix:

```text
ProxyHeaders__Mode=SelfManaged
ProxyHeaders__ForwardLimit=1
ProxyHeaders__TrustedProxies__0=10.0.0.100
ProxyHeaders__TrustedNetworks__0=172.20.0.0/16
```

The feature is registered via `builder.AddProxyHeadersFeature()` and `app.UseProxyHeadersFeature()` in `Program.cs`.

---

## Registered Services

The *ProxyHeaders* feature does not register any injectable services. It configures the ASP.NET Core forwarded headers middleware internally.

---

## Pipeline Order

The *ProxyHeaders* feature registers middleware only — no endpoints. The order of `UseProxyHeadersFeature()` is critical: it must be the **first** middleware in the pipeline, before authentication, authorization, HTTPS redirection, and any other middleware that depends on client information. If placed later, security decisions would be made using the proxy's IP instead of the actual client's IP.

---

## Typical Usage

A typical proxy headers setup works as follows:

1. Determine your deployment scenario — direct access, cloud platform, or self-hosted proxy.
2. Set `Mode` accordingly (`Disabled`, `Cloud`, or `SelfManaged`).
3. For `SelfManaged` mode, add all proxy IPs to `TrustedProxies` or use `TrustedNetworks` for dynamic environments like Docker.
4. Set `ForwardLimit` to match the number of proxy layers (usually `1`).
5. Verify that logging shows the actual client IP, not the proxy IP.

> [!NOTE]
> When headers from a trusted proxy are processed, LumaCore updates `RemoteIpAddress`, `Request.Scheme`, `Request.Host`, and `Request.PathBase` to reflect the actual client information.

---

## Related Features

- [*HttpsRedirection*](https-redirection.md) — Enforce HTTPS (uses scheme from forwarded headers)
- [*CORS*](cors.md) — Cross-origin policies (may depend on correct host information)

---

© 2025 LumaCoreTech • MIT License
