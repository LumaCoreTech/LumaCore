# Security Architecture

**Audience:** Architects and Developers seeking to understand LumaCore's design

This document explains how LumaCore approaches security - not just what exists today, but why it's designed the way it is, and how it will evolve from a development bootstrap into a production-grade system.

## Table of Contents

- [Understanding LumaCore's Security Architecture](#understanding-lumacores-security-architecture)
- [Why JWT? Understanding the Authentication Strategy](#why-jwt-understanding-the-authentication-strategy)
- [The Authentication Flow: How Tokens Actually Work](#the-authentication-flow-how-tokens-actually-work)
- [Authorization: From Authentication to Access Control](#authorization-from-authentication-to-access-control)
- [JWT Token Anatomy: What's Actually in There?](#jwt-token-anatomy-whats-actually-in-there)
- [Cookie Transport: Tokens for Browser Clients](#cookie-transport-tokens-for-browser-clients)
- [Token Revocation: Invalidating Active Tokens](#token-revocation-invalidating-active-tokens)
- [Configuration Security: Getting the Foundation Right](#configuration-security-getting-the-foundation-right)
- [HTTPS: Protecting Tokens in Transit](#https-protecting-tokens-in-transit)
- [Understanding the Threat Model](#understanding-the-threat-model)

---

## Understanding LumaCore's Security Architecture

LumaCore's security follows a **defense-in-depth philosophy**: multiple layers of protection that work together to secure the system. The architecture is designed to be production-ready with proper credential handling, persistent user storage, and all safeguards needed for untrusted networks.

> 📊 **For current implementation status**, see **[Status & Roadmap](../status.md)**
> 
> ⚠️ **Development Note:** During early development, LumaCore may use a simplified bootstrap authentication (hardcoded credentials) to enable rapid iteration. This is documented in status.md and will be replaced with production-grade security.

### Security Scope

LumaCore's security architecture covers:

- **JWT-based authentication** — tokens contain all user info, enabling stateless API servers that can scale across multiple instances without shared session storage
- **Role-based authorization** — users have roles (like `admin`), and endpoints verify roles before granting access
- **HTTPS enforcement** — all traffic encrypted, preventing tokens and credentials from being intercepted on the network
- **Fail-fast configuration** — application refuses to start if JWT settings are missing or invalid, catching security misconfigurations during deployment
- **Secure token signing** — tokens are cryptographically signed using HMAC SHA256 with a strong secret key (minimum 32 characters enforced)
- **Persistent user store** — database-backed user accounts with individual credentials
- **Password hashing** — passwords stored using bcrypt or Argon2
- **User management** — admin-controlled account creation and management
- **Refresh tokens** — long-lived tokens for session persistence with short-lived access tokens for security
- **Rate limiting** — restrict login attempts per IP address to prevent brute-force attacks
- **Audit logging** — track all security events for compliance and incident investigation

> 📊 **For current implementation status of each capability**, see **[Status & Roadmap](../status.md)**

---

## Why JWT? Understanding the Authentication Strategy

LumaCore uses **JSON Web Tokens (JWT)** for authentication. This isn't arbitrary - it's a deliberate choice based on how the system needs to scale and deploy.

### The Alternative: Session-Based Authentication

Traditional web applications use **server-side sessions**: when you log in, the server creates a session record in memory or a database, stores your identity there, and gives you a session ID cookie. Every request requires the server to look up your session to know who you are.

This works fine for monolithic applications running on a single server. But it creates problems when you want to:
- **Scale horizontally** - Multiple API servers need shared session storage
- **Deploy without state** - Containers and serverless platforms prefer stateless apps
- **Separate API and UI** - Sessions are cookie-based, which gets complicated with CORS

### The JWT Approach: Stateless Authentication

JWT takes a different approach: **your identity is encoded in the token itself**. When you log in, the server creates a signed token containing your identity (username, roles, etc.) and gives it to you. On every subsequent request, you send that token, and the server validates the signature to prove it's legitimate.

**Key advantage:** The server doesn't need to remember anything for *most* requests. No session storage, no per-request database lookups for identity. Just validate the signature and trust the claims inside.

**Key trade-off:** A JWT is self-contained — the signature alone proves it is valid. To invalidate one before its natural expiration, the server has to maintain a *negative* list ("this token has been revoked") and consult it on every request. LumaCore does exactly that (see [Token Revocation](#token-revocation-invalidating-active-tokens), below); the cost is one cache lookup per request, not a full session rehydration. This is why JWTs are still designed to be short-lived: revocation is a safety net, not the primary expiration mechanism.

### Why HS256 (HMAC with SHA256)?

JWT supports multiple signing algorithms. LumaCore uses **HS256** (HMAC with SHA-256) because:

1. **Symmetric signing is fast** - Same secret key for signing and validation, no public/private key math
2. **Simpler key management** - One secret instead of a key pair
3. **Perfect for API-only authentication** - We're not distributing tokens to third parties who need to verify them independently

**When to use RSA instead:** If external systems need to validate tokens without sharing the signing secret (like OAuth providers do), RS256 (RSA with SHA-256) would be used. But for an API authenticating its own clients, HS256 is simpler and faster.

### Token Lifetime: The Security/Convenience Trade-off

A JWT is valid until it expires *or* until it is explicitly revoked. The default access token lifetime is 60 minutes; after that, the user must re-authenticate.

**The convenience problem:** Re-authenticating every hour is annoying for users.

**The production solution:** Pair short-lived access tokens with **refresh tokens** — users receive a long-lived refresh token (stored securely, server-side revocable) that allows obtaining new short-lived access tokens without re-entering credentials. This provides both security (short-lived access tokens limit exposure) and convenience (infrequent re-authentication). Refresh tokens are not yet implemented in LumaCore; see [Status & Roadmap](../status.md).

In the meantime, the combination of a 60-minute lifetime and explicit revocation on logout (see [Token Revocation](#token-revocation-invalidating-active-tokens)) is the security model: stolen tokens expire quickly *and* a user who logs out invalidates their token immediately.

---

## The Authentication Flow: How Tokens Actually Work

Let's walk through what happens when someone logs in and then makes API calls. Understanding this flow is crucial because every security decision builds on these steps.

### Step 1: Login (Token Issuance)

A client (browser, mobile app, or CLI tool) wants to access protected endpoints. First, it needs to prove who it is.

**What happens:**

```
              Client                                    LumaCore API
                |                                            |
                |  POST /api/v1/auth/login                   |
                |  {                                         |
                |    "username": "admin",                    |
                |    "password": "changeme"                  |
                |  }                                         |
                |------------------------------------------->|
                |                                            |
                |                                            | 1. Validate credentials
                |                                            |    (currently: hardcoded)
                |                                            |    (future: database with password hash)
                |                                            |
                |                                            | 2. If valid, build claims:
                |                                            |    - sub: "admin"
                |                                            |    - name: "admin"
                |                                            |    - role: "admin"
                |                                            |
                |  200 OK                                    | 3. Create JWT token:
                |  {                                         |    - Encode claims
                |    "accessToken": "eyJhbGc..."             |    - Sign with HMAC
                |  }                                         |    - Set expiration
                |<-------------------------------------------|
Store token for |                                            |
future requests |                                            |
```

**What the client does next:** Browser clients receive the token in two forms — the JSON response body *and* an `HttpOnly` cookie set by the API. The cookie is sent automatically on subsequent requests, so the browser never has to handle the token from JavaScript. Non-browser clients (CLI tools, companion AIs) ignore the cookie and store the token themselves, sending it on each request as `Authorization: Bearer eyJhbGc...`. See [Cookie Transport](#cookie-transport-tokens-for-browser-clients) below for why browser and non-browser clients are treated differently.

**Security considerations:**
- Failed login returns generic `401 Unauthorized` without details - we don't leak whether the username exists or the password was wrong
- Successful login is logged for audit purposes
- Rate limiting prevents brute-force attacks

### Step 2: Token Validation (Every Protected Request)

Now the client has a token and wants to access protected endpoints. Every request goes through a validation pipeline.

**What happens:**

```
Client                                    LumaCore API
  |                                            |
  |  GET /api/v1/admin/status                  |
  |  Authorization: Bearer eyJhbGc...          |
  |------------------------------------------->|
  |                                            |
  |                                            | JWT Authentication Middleware:
  |                                            |
  |                                            | 1. Extract token
  |                                            |    - Parse Bearer token
  |                                            |    - Decode Base64
  |                                            |
  |                                            | 2. Validate structure
  |                                            |    - Check JWT format
  |                                            |    - Parse header/payload
  |                                            |
  |                                            | 3. Verify signature
  |                                            |    - Recompute HMAC
  |                                            |    - Compare signatures
  |                                            |    - If mismatch → 401
  |                                            |
  |                                            | 4. Validate claims
  |                                            |    - iss: correct issuer
  |                                            |    - aud: correct audience
  |                                            |    - exp: not expired
  |                                            |    - nbf: valid time
  |                                            |    - If any fail → 401
  |                                            |
  |                                            | 5. Build ClaimsPrincipal
  |                                            |    - Extract claims
  |                                            |    - Create identity
  |                                            |    - Attach to request
  |                                            |
  |                                            | Authorization Middleware:
  |                                            |
  |                                            | 6. Check requirements
  |                                            |    - AllowAnonymous?
  |                                            |    - RequireAuth?
  |                                            |    - RequireRole?
  |                                            |
  |                                            | 7. Execute handler
  |                                            |    - User authenticated
  |  200 OK                                    |    - Access granted
  |  { ... response data ... }                 |
  |<-------------------------------------------|
  |                                            |
```

**Why this multi-step validation matters:**

This entire validation pipeline is handled by **ASP.NET Core's authentication middleware** - it's not custom code in LumaCore. When the application calls `app.UseAuthentication()` during startup, ASP.NET automatically performs all these checks on every request. LumaCore's role is configuration: the `JwtOptions` class specifies which issuer to accept, which audience to require, and which signing key to use for verification. The framework then enforces these requirements on every protected endpoint.

Each step builds on the previous one, creating a defense-in-depth approach:

**1. Token extraction** - Before validation can proceed, the token must be located and parsed from the `Authorization: Bearer <token>` header. This step ensures the request even contains a token and that it's in the expected format. If extraction fails (malformed header, missing token), the request is immediately rejected - there's no point continuing to later steps.

**2. Structure validation** - A JWT has a specific format: three Base64-encoded parts separated by dots (header.payload.signature). This step ensures the token is well-formed before attempting to parse its contents. Malformed tokens could potentially exploit parsing bugs, so validating structure first is a security measure. This catches corrupted tokens, truncated tokens, or tokens that aren't JWTs at all.

**3. Signature verification** - **This is the critical security step.** The signature mathematically proves that someone with the `SigningKey` created this token. ASP.NET recomputes the signature using LumaCore's configured signing key and compares it to the token's signature. If they don't match exactly, the token has either been tampered with or was created by someone who doesn't have the legitimate signing key. This is why protecting the `SigningKey` is so important - it's the foundation of trust. Without signature verification, anyone could create a token claiming to be an admin.

**4. Claims validation** - Even with a valid signature, the token might not be intended for LumaCore. The `iss` (issuer) claim is checked to ensure the token was issued by LumaCore, not some other system. The `aud` (audience) claim ensures the token was intended for this API. The `exp` (expiration) and `nbf` (not before) claims enforce time-based validity - tokens can't be used before their "not before" time or after their expiration. This prevents replay attacks with old tokens and ensures stolen tokens eventually become useless.

**5. Principal creation** - If all validations pass, ASP.NET extracts the claims from the token and builds a `ClaimsPrincipal` object. This becomes the authenticated user for this request. The ClaimsPrincipal is made available to endpoint handlers through ASP.NET's `HttpContext`, allowing LumaCore's handlers to check `user.IsInRole("admin")` or read claims like `user.FindFirst("sub")?.Value`. This step transforms a validated token into a usable identity that application code can work with.

**6. Authorization checks** - Authentication proved who you are; authorization decides what you're allowed to do. ASP.NET checks the endpoint's requirements (`[Authorize]`, role requirements, policy requirements) against the authenticated principal. If you're accessing an admin-only endpoint but don't have the admin role, you're rejected even though you're authenticated. This separation of "who you are" (authentication) and "what you can do" (authorization) is a fundamental security principle.

**What happens if any step fails:** The request immediately returns `401 Unauthorized` without executing the endpoint handler. The user never gets to see protected data.

**Why signature verification is critical:**

Without signature verification, anyone could create a JWT with `"role": "admin"` and access everything. The signature proves that **only someone with the SigningKey** (us) could have created this token. This is why protecting the SigningKey is so important - it's the root of all security.

### Step 3: Token Introspection (Understanding Your Identity)

LumaCore provides two endpoints that help clients understand their authentication state:

**`GET /api/v1/auth/whoami`** - Returns basic identity information:
```json
{
  "name": "John Doe",
  "roles": ["user"],
  "claims": [
    { "type": "sub", "value": "550e8400-e29b-41d4-a716-446655440000" },
    { "type": "name", "value": "John Doe" },
    { "type": "role", "value": "user" }
  ]
}
```

This is useful for UI applications that want to display "Logged in as: John Doe" or show/hide UI elements based on roles.

**`GET /api/v1/auth/introspect`** - Returns detailed token information:
```json
{
  "subject": "550e8400-e29b-41d4-a716-446655440000",
  "name": "John Doe",
  "roles": ["user"],
  "notBeforeUtc": "2025-01-01T12:00:00Z",
  "expiresUtc": "2025-01-01T13:00:00Z",
  "expiresIn": "00:45:23",
  "jwtId": "unique-token-id",
  "issuer": "LumaCore",
  "audience": "LumaCore",
  "configuredAccessTokenLifetimeMinutes": 60
}
```

This is primarily for debugging - it tells you exactly when your token will expire and lets you troubleshoot authentication issues.

---

## Authorization: From Authentication to Access Control

**Authentication** answers "Who are you?" - that's what JWT does.
**Authorization** answers "What are you allowed to do?" - that's what roles and policies do.

### The Role-Based Model

LumaCore uses **role-based access control (RBAC)** - the simplest and most common authorization model. Users have roles, endpoints require roles, and the middleware checks if there's a match.

**How roles work:**

During login, role information is embedded as claims in the JWT token. When a user logs in as an administrator, an `admin` role claim is added. When the token is validated on subsequent requests, ASP.NET Core's middleware extracts these role claims and makes them available for authorization decisions.

This means **every request carries its own authorization context** - there's no server-side session lookup. The token itself declares "this user has these roles."

**Authorization levels:**

Endpoints can declare different protection levels:

```csharp
.AllowAnonymous()  // No authentication required
.RequireAuthorization()  // Any valid token
.RequireAuthorization(policy => policy.RequireRole("admin"))  // Specific role required
```

> [!IMPORTANT]
> Every versioned API endpoint **must** explicitly declare its authorization level. LumaCore validates this at startup and will fail to start if any endpoint is missing `RequireAuthorization()` or `AllowAnonymous()`. This prevents accidental exposure of unprotected endpoints.

The authorization middleware checks these requirements against the authenticated principal's role claims and allows or denies access accordingly.

**Target roles:**

LumaCore implements multiple roles with different permission levels:
- `admin` - Full system access (user management, system configuration)
- `user` - Regular user access (interact with personas, manage own data)

Beyond simple roles, the system supports **fine-grained permissions** for more granular access control: instead of checking "is this user an admin?", the system can check "does this user have permission to delete this specific resource?".

---

## JWT Token Anatomy: What's Actually in There?

Understanding what's inside a JWT helps you understand how it provides security. Let's dissect one.

### Token Structure

A JWT is three Base64-encoded chunks separated by dots:

```
[header].[payload].[signature]
```

Here's a real token based on the example data shown below:
```
[header]    = eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9
[payload]   = <Base64URL encoding of the JSON shown in "Part 2" below>
[signature] = computed from header + payload using SigningKey
```

Each part serves a specific purpose.

### Part 1: Header (Algorithm Declaration)

```json
{
  "alg": "HS256",
  "typ": "JWT"
}
```

The header tells validators **how to verify the signature**. `HS256` means "HMAC with SHA-256" - a symmetric signing algorithm. `typ` just says "this is a JWT" (as opposed to other token formats).

**Why this matters:** If someone could change the algorithm to `none` (which exists for debugging), they could bypass signature verification entirely. ASP.NET Core's JWT middleware rejects `none` by default, which is why implementing custom JWT validation is strongly discouraged - there are subtle security traps that the framework handles correctly.

### Part 2: Payload (The Claims)

```json
{
  "sub": "550e8400-e29b-41d4-a716-446655440000",  // Subject - unique user identifier
  "iss": "LumaCore",                              // Issuer - who created this token
  "aud": "LumaCore",                              // Audience - who should accept this token
  "exp": 1735574400,                              // Expiration - latest valid time (Unix timestamp)
  "nbf": 1735570800,                              // Not Before - earliest valid time (Unix timestamp)
  "iat": 1735570800,                              // Issued At - when token was created (Unix timestamp)
  "jti": "8f3e2a1b-...",                          // JWT ID - unique identifier for this token (used for revocation)
  "name": "John Doe",                             // Display name (custom claim)
  "role": "user"                                  // User's role(s) (custom claim)
}
```

These are called **claims** - statements about the user. Some are standard JWT claims (`sub`, `iss`, `aud`, `exp`, `nbf`, `iat`, `jti`), and some are application-specific (`name`, `role`).

**Standard claims explained:**

- **`sub` (subject)** - Uniquely identifies the user. Usually the username or user ID. This is the primary "who is this?" field.
- **`iss` (issuer)** - Says who issued the token. LumaCore sets this based on the configured `Jwt:Issuer` value (typically `"LumaCore"`). Validation checks that incoming tokens claim to be from the configured issuer.
- **`aud` (audience)** - Says who should accept the token. Prevents tokens meant for one system from being used on another.
- **`exp` (expiration)** - Unix timestamp when token becomes invalid. After this time, validators reject it.
- **`nbf` (not before)** - Unix timestamp before which token is invalid. Prevents time-travel attacks (using a token before it's supposed to be active).
- **`iat` (issued at)** - Unix timestamp when the token was created. Useful for logging and debugging.
- **`jti` (JWT ID)** - Unique identifier for this specific token. LumaCore stores revoked `jti` values in the `RevokedJwts` table; see [Token Revocation](#token-revocation-invalidating-active-tokens).

**Custom claims:**

`name` and `role` are LumaCore's custom claims - application-specific data beyond the standard JWT claims. The JWT format allows adding any claims needed for authorization and identity.

**Why claims matter:**

Claims are the foundation of authorization. When the middleware validates a token, it extracts these claims and makes them available to application code through ASP.NET's `HttpContext`. Endpoints can check `user.IsInRole("admin")` because the `role` claim is present. This is also why sensitive data shouldn't be placed in JWTs - claims are encoded, not encrypted. Anyone with the token can decode it and read the claims (they just can't modify them without breaking the signature).

### Part 3: Signature (The Security Guarantee)

```
HMACSHA256(
  base64UrlEncode(header) + "." + base64UrlEncode(payload),
  secret: "your-signing-key-here"
)
```

The signature is a cryptographic hash of the header and payload, computed using the configured SigningKey. This is the security magic.

**How signature verification works:**

1. Receive token with signature
2. Take the header and payload from the token
3. Recompute the signature using LumaCore's configured SigningKey
4. Compare the computed signature with the token's signature
5. If they match exactly → token is valid and unmodified
6. If they don't match → token is forged or tampered with → reject

**Why this works:**

Without the SigningKey, an attacker can't produce the correct signature. They could modify the claims arbitrarily (`"role": "super-admin"`), but the signature wouldn't match anymore, and validation would fail.

**Why the SigningKey is critical:**

Anyone with the SigningKey can create valid tokens with any claims they want. This is why:
- SigningKey must be strong (minimum 32 characters, random)
- SigningKey must be secret (never commit to Git, never log it)
- SigningKey must be protected (environment variables, secret managers, not config files)

If the SigningKey leaks, **every token ever issued becomes forgeable**. The key would need to be rotated (invalidating all existing tokens) and the breach investigated.

### Part 4: Token Lifecycle

Now that we understand what's inside a token and how it's secured, let's look at its lifecycle - from creation to expiration.

1. **Issuance** - Token created during `/auth/login`
2. **Active Period** - Token valid for configured lifetime (default: 60 minutes)
3. **Revocation (optional)** - Token's `jti` recorded in the `RevokedJwts` table on logout; further requests with this token are rejected. See [Token Revocation](#token-revocation-invalidating-active-tokens).
4. **Expiration** - Token becomes invalid after `exp` timestamp; expired entries can be pruned from the revocation table.

**Security Note:** Short-lived tokens reduce the impact of token theft. Stolen tokens become useless after expiration even if they were never revoked. The `exp` claim is the floor; explicit revocation is the ceiling.

---

## Cookie Transport: Tokens for Browser Clients

The authentication flow above describes the canonical case: clients send `Authorization: Bearer <token>` on every request. That works perfectly for CLI tools, server-to-server calls, and companion AI agents — they have full control over their HTTP layer and can store the token wherever it makes sense.

For **browser-based clients** (LumaCore's Blazor WebAssembly UI), this same pattern is a security liability: any code that can read the token can also leak it. A single cross-site scripting (XSS) bug anywhere in the UI — a third-party dependency, a vulnerable component, an unsanitized rendering path — turns into total account compromise the moment that script reads `localStorage` or `sessionStorage`.

LumaCore solves this by **shipping the same token over two transports** at the same time:

1. **Response body** — The login response still contains `{ "accessToken": "..." }`, exactly as before. Non-browser clients consume this and ignore everything else.
2. **`HttpOnly` cookie** — The login endpoint additionally sets a cookie containing the token. The cookie is marked `HttpOnly`, so JavaScript cannot read it; `Secure`, so it is only sent over HTTPS; and `SameSite=Strict`, so the browser refuses to send it on cross-origin navigations or sub-requests.

On subsequent requests, the browser attaches the cookie automatically. The Blazor UI never touches the token — there is no value in JavaScript scope to steal.

**Why both transports at once?**

A single endpoint serving two client classes (browsers and APIs) needs to satisfy both without forking the auth flow. The dual transport keeps the API surface uniform; browsers simply *also* get a cookie they cannot read, while non-browsers see exactly the JSON they expected.

**What about CSRF?**

Cookies are notoriously vulnerable to cross-site request forgery: if any other site can convince the browser to issue a request to the API, the cookie rides along. `SameSite=Strict` is the primary mitigation — the browser refuses to attach the cookie to any request that did not originate from the same site. Combined with the existing CORS policy (which restricts which origins may call the API in the first place), this closes the standard CSRF attack vectors without needing a separate anti-forgery token.

**Bearer header still wins.** If a request arrives with both an `Authorization: Bearer` header *and* the cookie, the Bearer header takes priority. This keeps API testing and explicit token use deterministic: a manually supplied token is never silently overridden by a stale cookie.

The transport is controlled by `AuthCookieOptions` (configuration section `Jwt:Cookie`). It is enabled by default; deployments that only serve non-browser clients can disable it by setting `Jwt:Cookie:Enabled` to `false`.

---

## Token Revocation: Invalidating Active Tokens

A stateless JWT cannot be "unissued". Once signed, it is valid until its `exp` timestamp — unless the server keeps a record that this specific token must no longer be accepted. LumaCore maintains exactly such a record so that **logout, account suspension, and emergency response** all have an immediate effect, instead of waiting up to 60 minutes for the token to expire on its own.

### How Revocation Works

Each token carries a unique `jti` (JWT ID) claim. When the user calls `POST /api/v1/auth/logout`:

1. The API extracts `jti` and `exp` from the presented token.
2. A row is inserted into the `RevokedJwts` table containing the `jti`, the original `exp` (so expired entries can be pruned), and the time of revocation.
3. The HTTP response also clears the auth cookie, so browser clients stop sending the revoked token immediately.

From that moment on, the authentication pipeline rejects any request bearing the same `jti` with `401 Unauthorized`, even though the signature is still mathematically valid.

### Avoiding a Database Hit per Request

Checking the `RevokedJwts` table on every authenticated request would add a round trip to the hot path. LumaCore's `TokenRevocationOptions` introduces a small in-memory cache with a deliberately asymmetric policy:

- **Negative results are cached.** "This `jti` is not revoked" is the overwhelmingly common case and is safe to cache for a few seconds.
- **Positive results are never cached.** Once a token is revoked, it stays revoked — there is no need to remember that fact in a TTL cache.
- **Cache is invalidated on revoke.** When a new revocation is recorded, the cache is evicted, so freshly revoked tokens are rejected immediately on the same instance.

The cache duration is governed by `Jwt:TokenRevocation:CacheDurationSeconds` (default: `5`, range: `0`–`60`). Setting it to `0` disables caching entirely and queries the database on every request — the strongest consistency guarantee, at the cost of one extra database round trip per authenticated call.

### What Revocation Does *Not* Solve

Revocation is bounded by the `RevokedJwts` table, which only sees tokens that pass through `/auth/logout` (or a future administrative revoke endpoint). It does **not** retroactively invalidate tokens issued before a credential change, and it does **not** replace the need for short token lifetimes — a token that was stolen and never logged out will still be valid until its `exp`. The roadmap entries for refresh tokens and "last password change" timestamp validation (see [Status & Roadmap](../status.md)) close those remaining gaps.

---

## Configuration Security: Getting the Foundation Right

JWT security depends entirely on configuration. Get the config wrong, and signature verification means nothing. LumaCore uses ASP.NET Core's Options pattern with validation to ensure configuration is correct before the application starts.

### The JwtOptions Configuration Class

The core JWT settings live in a single configuration class:

```csharp
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
```

The validation attributes — `[Required]`, `[MinLength(32)]`, `[Range(1, 1440)]` — enforce security requirements at the type level.

Two related option classes bind to **sibling** configuration sections and carry their own defaults:

- **`AuthCookieOptions`** (`Jwt:Cookie`) — controls the `HttpOnly` cookie used to ship the token to browser clients (see [Cookie Transport](#cookie-transport-tokens-for-browser-clients)).
- **`TokenRevocationOptions`** (`Jwt:TokenRevocation`) — controls the `jti`-based revocation cache (see [Token Revocation](#token-revocation-invalidating-active-tokens)).

Both have safe defaults, so the minimum required configuration remains the four `JwtOptions` fields above.

### Configuration Sources

Configuration can come from multiple sources, checked in this priority order:

1. **Command-line arguments** (highest priority)
2. **Environment variables** (recommended for production)
3. **appsettings.{Environment}.json** (good for development)
4. **appsettings.json** (defaults)

**Development configuration** (`appsettings.Development.json`):
```json
{
  "Jwt": {
    "Issuer": "LumaCore",
    "Audience": "LumaCore",
    "SigningKey": "development-key-minimum-32-characters-long-not-for-production",
    "AccessTokenLifetimeMinutes": 60
  }
}
```

This is fine for development because it's never deployed.

**Production configuration** (environment variables):
```bash
Jwt__Issuer=LumaCore
Jwt__Audience=LumaCore
Jwt__SigningKey=<strong-random-secret-from-secret-manager>
Jwt__AccessTokenLifetimeMinutes=15
```

Notice the double underscore (`__`) - that's how ASP.NET Core maps environment variables to nested configuration.

### Fail-Fast Validation

Here's the critical security feature: **validation at startup**.

```csharp
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();  // ← This is the key
```

`.ValidateOnStart()` means the application **will not start** if configuration is invalid. You get a clear error message during deployment, not a mysterious authentication failure in production at 3 AM.

**What gets validated:**

- `Issuer` and `Audience` must be present (not empty)
- `SigningKey` must be at least 32 characters (ensures sufficient entropy for HS256)
- `AccessTokenLifetimeMinutes` must be between 1 and 1440 (1 minute to 1 day)

**Why this matters:**

Security bugs are often configuration bugs. Someone forgets to set `SigningKey` in production, or sets it to something weak like `"secret"`. Fail-fast validation catches these mistakes before they become security incidents.

### Securing the SigningKey

The SigningKey is the crown jewel. If it leaks, your entire authentication system is compromised. Here's how to protect it:

**DO:**
- ✅ Generate cryptographically random keys: `openssl rand -base64 48`
- ✅ Store in environment variables or secret managers (AWS Secrets Manager, Azure Key Vault, Docker secrets)
- ✅ Rotate periodically (forces re-authentication but improves security)
- ✅ Use different keys for different environments (dev/staging/prod)

**DON'T:**
- ❌ Commit to Git (even in private repos - Git history is forever)
- ❌ Log or print (no `Console.WriteLine(signingKey)` for debugging)
- ❌ Share between applications (each app should have its own key)
- ❌ Use weak values like `"secret"`, `"password"`, or `"12345678901234567890123456789012"`

### Token Lifetime: Security vs Usability

`AccessTokenLifetimeMinutes` controls how long tokens are valid. This is a fundamental security trade-off:

**Short lifetime (15 minutes):**
- ✅ Stolen tokens expire quickly, limiting damage
- ✅ Forces regular re-authentication, helps detect compromised accounts
- ❌ Users must log in frequently (annoying)

**Long lifetime (24 hours):**
- ✅ Users rarely need to log in (convenient)
- ❌ Stolen tokens valid for a full day
- ❌ Hard to revoke access (user deleted but token still valid)

**Recommended approach (refresh tokens):**
Short access tokens (15-30 minutes) + long refresh tokens (7 days). Access tokens can't be revoked, but they expire quickly. Refresh tokens can be revoked server-side and are used to get new access tokens. Best of both worlds.

---

## HTTPS: Protecting Tokens in Transit

All the cryptography in the world doesn't help if someone can sniff your token off the network. This is why **HTTPS is mandatory** for any JWT-based system.

### Why HTTPS Matters

Without HTTPS, network traffic is plaintext. Anyone between the client and server can read everything:
- Login requests (username/password in the clear)
- JWT tokens (in Authorization headers)
- Response data (potentially sensitive)

**Attack scenario without HTTPS:**

You're at a coffee shop on public WiFi. You log into LumaCore. An attacker running Wireshark on the same network captures the POST to `/auth/login` and sees:
```
POST /api/v1/auth/login HTTP/1.1
{"username": "admin", "password": "changeme"}
```

Game over. They have your credentials.

Even if they miss the login, they can capture your JWT from subsequent requests:
```
GET /api/v1/admin/status HTTP/1.1
Authorization: Bearer eyJhbGc...
```

Now they have a valid token and can impersonate you until it expires.

**With HTTPS:**

All traffic is encrypted. The attacker sees gibberish. They know you're talking to LumaCore, but can't read the content.

### How LumaCore Enforces HTTPS

**HTTPS Endpoints:**

Kestrel endpoints are configured in `appsettings.json`. By default, only HTTP is enabled for bootstrap simplicity:

```json
"Kestrel": {
    "Endpoints": {
        "Http": { "Url": "http://localhost:5080" }
    }
}
```

HTTPS can be added by configuring an HTTPS endpoint:

```json
"Kestrel": {
    "Endpoints": {
        "Http": { "Url": "http://localhost:5080" },
        "Https": { "Url": "https://localhost:5443" }
    }
}
```

### Certificate Configuration

HTTPS requires a TLS certificate. How you configure this differs between development and production.

**Development (automatic):**

```bash
dotnet dev-certs https --trust
```

This generates a self-signed certificate and installs it in your system's trusted certificate store. Kestrel finds it automatically - no configuration needed:

```json
"Kestrel": {
    "Endpoints": {
        "Https": { "Url": "https://localhost:5443" }
    }
}
```

The certificate is installed in your operating system's certificate store:
- **Windows:** `CurrentUser\My` (Personal) and with `--trust` also in Trusted Root Certification Authorities
- **macOS:** Login Keychain (marked as trusted)
- **Linux:** `~/.dotnet/corefx/cryptography/x509stores/my/` (note: `--trust` doesn't configure system-wide trust on Linux)

Kestrel discovers it automatically when running in Development mode.

**Production (explicit):**

Production requires a real certificate from a Certificate Authority (CA). You must explicitly configure the certificate path:

```json
"Kestrel": {
    "Endpoints": {
        "Https": {
            "Url": "https://localhost:5443",
            "Certificate": {
                "Path": "/etc/ssl/certs/lumacore.pfx",
                "Password": "cert-password"
            }
        }
    }
}
```

**Certificate sources for production:**
- **Let's Encrypt** - Free, automated certificates (recommended)
- **Cloud providers** - AWS ACM, Azure Key Vault, GCP Certificate Manager
- **Commercial CAs** - DigiCert, GlobalSign, etc.
- **Internal CAs** - For private networks

**Important:** Store certificate passwords in environment variables or secret managers, never in source control:
```bash
export KESTREL_CERTIFICATE_PASSWORD="your-secure-password"
```

For detailed certificate management, deployment scenarios, and automation, see the [Deployment Documentation](../deployment/docker.md).

**HTTPS Redirection:**

HTTPS redirection is **opt-in via configuration** through the `Https:RedirectEnabled` setting (default: `false`). When enabled, HTTP requests receive a `308 Permanent Redirect` to their HTTPS equivalent. Clients learn to use HTTPS and cache that decision.

**Configuration:**

```json
{
  "Https": {
    "RedirectEnabled": true,  // Enable HTTP → HTTPS redirection
    "HttpsPort": 5443         // Optional: specify HTTPS port for redirects
  }
}
```

This is opt-in (default: `false`) to support flexible deployment scenarios - reverse proxies often handle HTTPS termination, making application-level redirection unnecessary.

---

## Understanding the Threat Model

Security isn't about preventing all possible attacks - that's impossible. It's about understanding realistic threats and implementing defenses proportional to the risk. This threat model identifies threats and describes how LumaCore's security architecture mitigates them.

> 📊 **For current implementation status**, see **[Status & Roadmap](../status.md)**

### Threat 1: Token Theft via Network

**Attack scenario:** Attacker on shared network (coffee shop WiFi) sniffs traffic and captures JWT from Authorization header.

**Impact:** Attacker can impersonate user until token expires.

**Mitigation:**
- ✅ HTTPS enforced in production (prevents network sniffing)
- ✅ Short token lifetime limits exposure window
- ✅ Refresh tokens allow even shorter access token lifetime
- ✅ Token binding ties tokens to specific clients/devices

### Threat 2: Token Theft via Storage

**Attack scenario:** Attacker gains access to client device (malware, stolen laptop) and extracts stored token.

**Impact:** Attacker can use token until it expires *or* until it is revoked.

**Mitigation:**
- ✅ Browser clients receive the token in an `HttpOnly` cookie — JavaScript (and therefore XSS) cannot read it (see [Cookie Transport](#cookie-transport-tokens-for-browser-clients))
- ✅ `SameSite=Strict` on the cookie blocks cross-site CSRF flows
- ✅ Short token lifetime limits exposure window
- ✅ Explicit logout revokes the token immediately ([Token Revocation](#token-revocation-invalidating-active-tokens))
- 📌 Refresh tokens with server-side revocation (see [Status & Roadmap](../status.md))
- 📌 Device fingerprinting to detect unusual access patterns (planned)

### Threat 3: Brute-Force Login Attempts

**Attack scenario:** Attacker tries thousands of passwords against known usernames.

**Impact:** May eventually guess credentials (especially with weak passwords).

**Mitigation:**
- ✅ Rate limiting per IP address
- ✅ Progressive delays after failures
- ✅ Account lockout after N failures
- ✅ CAPTCHA for suspicious patterns

### Threat 4: SigningKey Compromise

**Attack scenario:** Attacker gains read access to production configuration (compromised server, leaked environment dump, insider threat).

**Impact:** Can forge arbitrary tokens with any claims. Complete authentication bypass.

**Mitigation:**
- ✅ Configuration validation prevents weak keys
- ✅ SigningKey stored in environment variables or secret managers
- ✅ Key never logged or displayed
- ✅ Secret managers (AWS Secrets Manager, Azure Key Vault) for production
- ✅ Key rotation procedures documented
- ✅ Audit logging for configuration access

### Threat 5: Token Revocation

**Attack scenario:** User logs out, account is disabled, or a token is suspected of being compromised, but the token has not yet reached its `exp` timestamp.

**Impact:** Without revocation, the token would remain valid until natural expiration — even minutes after "logout".

**Mitigation:**
- ✅ Token blacklist via `jti` — `POST /api/v1/auth/logout` records the `jti` in `RevokedJwts` and the authentication pipeline rejects revoked tokens (see [Token Revocation](#token-revocation-invalidating-active-tokens))
- ✅ Short token lifetime keeps the worst-case window bounded even if a logout is missed
- 📌 Refresh token revocation (planned — invalidate the refresh token, access tokens expire naturally)
- 📌 "Last password change" timestamp validation (planned)

---

## Next Steps

For implementation status of security features and the current development phase:

📊 **[Status & Roadmap](../status.md)** - Current implementation status and phase details

---

© 2025-2026 LumaCoreTech • MIT License
