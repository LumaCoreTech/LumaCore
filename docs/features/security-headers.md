# SecurityHeaders Feature (HTTP Security Headers)

The *SecurityHeaders* feature adds HTTP security headers to all responses, protecting against common web vulnerabilities like clickjacking, MIME-type sniffing, and cross-site scripting (XSS). These headers are a defense-in-depth measure — even if the application has vulnerabilities, browsers enforce security policies that can prevent or limit attacks. The feature is enabled by default with production-ready settings.

---

## Configuration

All settings are configured in `appsettings.json` (or via environment variables) under the `SecurityHeaders` section. The defaults are production-ready — most deployments only need to verify HSTS is appropriate for their environment.

### Options

| Option | Default | Validation | Description |
|--------|---------|------------|-------------|
| `Enabled` | `true` | — | Master switch for all security headers |
| `EnableHsts` | `true` | — | Send `Strict-Transport-Security` header |
| `HstsMaxAgeSeconds` | `31536000` | ≥ 0 | HSTS duration in seconds (default: 1 year) |
| `HstsIncludeSubDomains` | `false` | — | Apply HSTS to all subdomains |
| `XFrameOptions` | `"DENY"` | `DENY`, `SAMEORIGIN`, or `null` | Controls iframe embedding |
| `EnableNoSniff` | `true` | — | Send `X-Content-Type-Options: nosniff` |
| `ReferrerPolicy` | `"strict-origin-when-cross-origin"` | Valid policy or `null` | Controls referrer information |
| `ContentSecurityPolicy` | See below | Non-empty or `null` | Approved content sources |

> [!WARNING]
> Only enable HSTS when your site fully supports HTTPS. Once enabled, browsers will refuse HTTP connections for the specified duration. Consider disabling HSTS in development to avoid localhost issues.

### Default Content-Security-Policy

The default CSP is optimized for Blazor WebAssembly:

```
default-src 'self'; script-src 'self' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'
```

> [!NOTE]
> Blazor WASM requires `'unsafe-eval'` for its .NET runtime and `'unsafe-inline'` for dynamic styles. These weaken CSP somewhat but are unavoidable. A weak CSP is still better than no CSP — it blocks external script injection while allowing Blazor to function.

> [!WARNING]
> Do not blindly copy CSP policies from tutorials. The correct policy depends on your specific UI, external resources, and deployment scenario. Test thoroughly in staging before deploying to production.

### Example: `appsettings.json`

```json
{
  "SecurityHeaders": {
    "Enabled": true,
    "EnableHsts": true,
    "HstsMaxAgeSeconds": 31536000,
    "XFrameOptions": "DENY",
    "ReferrerPolicy": "strict-origin-when-cross-origin"
  }
}
```

### Environment Variables

Options can also be set via environment variables with the `SecurityHeaders__` prefix:

```text
SecurityHeaders__Enabled=true
SecurityHeaders__EnableHsts=true
SecurityHeaders__HstsMaxAgeSeconds=31536000
SecurityHeaders__XFrameOptions=DENY
SecurityHeaders__ReferrerPolicy=strict-origin-when-cross-origin
```

The feature is registered via `builder.AddSecurityHeadersFeature()` and `app.UseSecurityHeadersFeature()` in `Program.cs`.

---

## Registered Services

The *SecurityHeaders* feature does not register any injectable services. It configures response headers via middleware.

---

## Pipeline Order

The *SecurityHeaders* feature registers middleware only — no endpoints. The order of `UseSecurityHeadersFeature()` matters: it should be placed early in the pipeline, after proxy headers and HTTPS redirection, but before CORS and routing.

---

## Typical Usage

A typical security headers setup works as follows:

1. Keep `Enabled` as `true` (the default).
2. Verify HSTS is appropriate — disable in development if localhost HTTPS causes issues.
3. Adjust `ContentSecurityPolicy` if external resources (CDNs, APIs) are needed.
4. Set `XFrameOptions` to `"SAMEORIGIN"` if the application needs to be embedded in iframes on the same domain.
5. Use browser developer tools to check for CSP violations during testing.

---

## Related Features

- [*HttpsRedirection*](https-redirection.md) — Enforce HTTPS (required for HSTS to be effective)
- [*CORS*](cors.md) — Cross-origin request policies

---

© 2025 LumaCoreTech • MIT License
