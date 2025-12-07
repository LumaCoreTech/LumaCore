# CORS Feature (Cross-Origin Resource Sharing)

The *CORS* feature controls which external origins can access the LumaCore API from web browsers. This is essential when the Blazor UI or other web clients are served from a different domain than the API. If LumaCore serves the Blazor UI itself (default setup), CORS is not required because everything runs on the same origin — enable this feature only when clients access the API from a different domain or port.

> [!NOTE]
> CORS is a browser constraint, not a security feature. It only affects requests from web browsers — command-line tools like `curl` or server-to-server calls bypass CORS entirely. Do not rely on CORS for API security.

---

## Configuration

All CORS settings are configured in `appsettings.json` (or via environment variables) under the `Cors` section. When `Enabled` is `true`, at least one origin must be specified — if validation fails, LumaCore refuses to start.

### Options

| Option | Required | Default | Validation | Description |
|--------|----------|---------|------------|-------------|
| `Enabled` | No | `false` | — | Whether CORS is active |
| `AllowedOrigins` | If enabled | `[]` | Non-empty when enabled | Origins allowed to make requests (e.g., `https://app.example.com`) |
| `AllowCredentials` | No | `false` | Cannot be `true` with wildcard origin | Whether cookies and auth headers are allowed |
| `AllowedMethods` | No | `[]` | — | Allowed HTTP methods (empty = all) |
| `AllowedHeaders` | No | `[]` | — | Allowed request headers (empty = all) |
| `ExposedHeaders` | No | `[]` | — | Additional response headers exposed to browser (empty = simple headers only) |
| `PreflightMaxAge` | No | `null` | ≥ 0 | Preflight cache duration in seconds |

> [!WARNING]
> Never use `"*"` (wildcard) for `AllowedOrigins` in production — any website could call your API. Specify exact origins instead.

> [!WARNING]
> `AllowCredentials` cannot be `true` when `AllowedOrigins` contains `"*"`. This combination is a security vulnerability and will fail validation at startup.

### Example: `appsettings.json`

```json
{
  "Cors": {
    "Enabled": true,
    "AllowedOrigins": [
      "https://app.example.com",
      "https://admin.example.com"
    ],
    "AllowCredentials": true,
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
    "AllowedHeaders": ["Content-Type", "Authorization"],
    "PreflightMaxAge": 3600
  }
}
```

### Environment Variables

Options can also be set via environment variables with the `Cors__` prefix:

```text
Cors__Enabled=true
Cors__AllowedOrigins__0=https://app.example.com
Cors__AllowedOrigins__1=https://admin.example.com
Cors__AllowCredentials=true
Cors__AllowedMethods__0=GET
Cors__AllowedMethods__1=POST
Cors__PreflightMaxAge=3600
```

The feature is registered via `builder.AddCorsFeature()` and `app.UseCorsFeature()` in `Program.cs`.

---

## Registered Services

The *CORS* feature does not register any injectable services. It configures the ASP.NET Core CORS middleware internally.

---

## Pipeline Order

The *CORS* feature registers middleware only — no endpoints. The order of `UseCorsFeature()` matters: it must be placed before routing and authentication so that preflight (OPTIONS) requests are handled correctly.

---

## Typical Usage

A typical CORS setup works as follows:

1. Determine if CORS is needed — only if clients access the API from a different origin.
2. Set `Enabled` to `true` in configuration.
3. Add all legitimate client origins to `AllowedOrigins`.
4. Enable `AllowCredentials` if clients need to send cookies or authorization headers.
5. Optionally restrict `AllowedMethods` and `AllowedHeaders` to only what clients actually use.
6. Set `PreflightMaxAge` to reduce OPTIONS request overhead.

> [!NOTE]
> If browser dev tools show CORS errors, verify that the requesting origin matches exactly (including scheme and port) and that the HTTP method and headers are allowed.

---

## Related Features

- [*SecurityHeaders*](security-headers.md) — Additional security headers
- [*ProxyHeaders*](proxy-headers.md) — Extracting original client info behind reverse proxies

---

© 2025 LumaCoreTech • MIT License
