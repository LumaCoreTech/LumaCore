# Auth Integration

How the Blazor WASM client (`LumaCore.Ui.Web`) authenticates against the LumaCore API using `HttpOnly` cookie transport.

> For server-side configuration (JWT options, cookie settings, token revocation) see [Auth Feature](../../features/auth.md).

---

## Overview

The client never handles JWTs directly — the browser manages the `HttpOnly` authentication cookie automatically. Three services coordinate the client-side authentication flow:

| Component | Purpose |
|-----------|---------|
| `AuthService` | Sends login and logout HTTP requests to the API. Returns a `bool` on logout to indicate success or failure. |
| `CookieCredentialHandler` | `DelegatingHandler` that sets `credentials: 'include'` on every outgoing request so the browser sends the `HttpOnly` cookie — required for cross-origin deployments (e.g., CDN-hosted SPA calling an API on a different subdomain). |
| `CookieAuthenticationStateProvider` | Blazor `AuthenticationStateProvider` that queries `GET /api/v1/auth/whoami` and caches the result in memory. Call `NotifyStateChanged()` after login or logout to invalidate the cache and update all `AuthorizeView` components. |

> [!NOTE]
> These components replace the former `JwtAuthorizationHandler` and `JwtAuthenticationStateProvider` that parsed the JWT client-side from JavaScript-accessible browser storage — an approach vulnerable to XSS-based token theft.

---

## Authentication Flow

1. `AuthService` sends credentials (with optional `rememberMe` flag) to `POST /api/v1/auth/login`.
2. On success, the API sets an `HttpOnly` cookie and returns the token in the response body.
3. `CookieCredentialHandler` sets `credentials: 'include'` on every outgoing `fetch` request, ensuring the browser sends the cookie — including in cross-origin deployments.
4. `CookieAuthenticationStateProvider` calls `GET /api/v1/auth/whoami` to determine the current identity and caches the result in memory. After login or logout, `NotifyStateChanged()` invalidates the cache and triggers a re-fetch.
5. To log out, `AuthService` calls `POST /api/v1/auth/logout`. On success, the token is revoked, the cookie is cleared, and the auth state provider is notified. On failure (server unreachable), the user remains authenticated and an error toast is shown — the `HttpOnly` cookie cannot be cleared client-side.

---

## Design Decisions

- **No client-side token storage.** The JWT is never exposed to JavaScript. The browser manages the `HttpOnly` cookie automatically.
- **Server-first logout.** If the logout endpoint is unreachable, the client does not fake a logout — the user remains authenticated and sees an error toast. This is necessary because the `HttpOnly` cookie cannot be cleared by client-side code.
- **Cross-origin support.** `CookieCredentialHandler` sets `credentials: 'include'` for all requests, supporting both same-origin and cross-origin deployments without configuration changes.

---

## DI Registration

All three services are registered in `Program.cs`:

```csharp
// CookieCredentialHandler ensures the browser sends the HttpOnly cookie on every request.
// Transient because IHttpClientFactory controls handler lifetimes — it creates fresh outer
// handler chains but pools the inner HttpClientHandler. Scoped/Singleton would conflict
// with the factory's disposal during handler rotation.
builder.Services.AddTransient<CookieCredentialHandler>();

// AuthService and CookieAuthenticationStateProvider are scoped to the user session.
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CookieAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CookieAuthenticationStateProvider>());
```

`CookieCredentialHandler` is added to the `HttpClient` pipeline alongside other handlers (e.g., `HealthTrackingHandler`).

---

## Related

- [Auth Feature](../../features/auth.md) — Server-side JWT configuration, endpoints, and token revocation
- [Blazor Guide](blazor-guide.md) — General Blazor patterns and JavaScript interop

---

© 2025-2026 LumaCoreTech • MIT License
