# LumaCore API

API surface of the LumaCore server (self-hosted, persona-focused AI runtime).

**Version:** `v1`

---

## Table of Contents

- [Authentication](#authentication)
- [Endpoints](#endpoints)
  - [Quick Reference](#quick-reference)
  - [Admin](#admin)
  - [Auth](#auth)
  - [Health](#health)
- [Schemas](#schemas)

## Authentication

### Bearer

- **Type:** `Http`
- **Scheme:** `bearer`
- **Bearer Format:** `JWT`

Enter your JWT token

## Endpoints

### Quick Reference

| Method | Endpoint | Description |
|--------|----------|-------------|
| 🟢 GET | [`/api/admin/status`](#get-apiadminstatus) | Returns high-level status information about the API. |
| 🟢 GET | [`/api/auth/introspect`](#get-apiauthintrospect) | Introspects the current JWT and returns details about the token. |
| 🔵 POST | [`/api/auth/login`](#post-apiauthlogin) | Authenticates the built-in admin and issues a JWT access token. |
| 🟢 GET | [`/api/auth/whoami`](#get-apiauthwhoami) | Returns the current authenticated user. |
| 🟢 GET | [`/api/health/live`](#get-apihealthlive) | Returns a simple JSON-based liveness indicator for the backend. |

### Admin

<a id="get-apiadminstatus"></a>
#### 🟢 GET `/api/admin/status`

Returns high-level status information about the API.

Returns a small, non-sensitive snapshot of the running LumaCore instance, including environment, API version, machine name, server time and JWT configuration status. Secrets such as the signing key are never exposed; only a masked representation of the key and basic configuration flags are returned.

**Responses**

| Status | Description |
|--------|-------------|
| ✅ 200 | OK |
| ⚠️ 401 | Authentication is required to access this endpoint. |
| ⚠️ 403 | The authenticated user does not have permission to access this resource. |

**200 Response:** 
Schema: [`AdminStatusResponse`](#schema-adminstatusresponse)

<details>
<summary><strong>Code Samples</strong></summary>

**Shell (curl)**

```bash
curl -X GET "{BASE_URL}/api/admin/status" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json"
```

**C# (HttpClient)**

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

var response = await client.GetAsync(
    $"{baseUrl}/api/admin/status");
```

</details>


[↑ Quick Reference](#quick-reference)

---

### Auth

<a id="get-apiauthintrospect"></a>
#### 🟢 GET `/api/auth/introspect`

Introspects the current JWT and returns details about the token.

Provides diagnostic information about the currently used JWT access token, including subject, roles, expiry and configured lifetime. This endpoint is intended primarily for debugging and support scenarios.

**Responses**

| Status | Description |
|--------|-------------|
| ✅ 200 | OK |
| ⚠️ 401 | Authentication is required to access this endpoint. |

**200 Response:** 
Schema: [`AuthIntrospectResponse`](#schema-authintrospectresponse)

<details>
<summary><strong>Code Samples</strong></summary>

**Shell (curl)**

```bash
curl -X GET "{BASE_URL}/api/auth/introspect" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json"
```

**C# (HttpClient)**

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

var response = await client.GetAsync(
    $"{baseUrl}/api/auth/introspect");
```

</details>


[↑ Quick Reference](#quick-reference)

---

<a id="post-apiauthlogin"></a>
#### 🔵 POST `/api/auth/login`

Authenticates the built-in admin and issues a JWT access token.

Authenticates the temporary, built-in administrator account and returns a short-lived JWT access token. This endpoint is intended for development and bootstrap scenarios only and will be replaced by a proper, database-backed authentication flow once persistent user management is available.

**Request Body**

**Content-Type:** `application/json`

Schema: [`LoginRequest`](#schema-loginrequest)

**Responses**

| Status | Description |
|--------|-------------|
| ✅ 200 | OK |
| ⚠️ 400 | The request body is invalid or failed validation. |

<details>
<summary><strong>Code Samples</strong></summary>

**Shell (curl)**

```bash
curl -X POST "{BASE_URL}/api/auth/login" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"username":"string","password":"string"}'
```

**C# (HttpClient)**

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

var content = new StringContent(
    JsonSerializer.Serialize(requestBody),
    Encoding.UTF8,
    "application/json");

var response = await client.PostAsync(
    $"{baseUrl}/api/auth/login",
    content);
```

</details>


[↑ Quick Reference](#quick-reference)

---

<a id="get-apiauthwhoami"></a>
#### 🟢 GET `/api/auth/whoami`

Returns the current authenticated user.

Returns basic identity information for the current authenticated principal, including effective name, roles, and raw claims. Intended for any authenticated user and typically used by client applications to show “who am I?” within the UI.

**Responses**

| Status | Description |
|--------|-------------|
| ✅ 200 | OK |
| ⚠️ 401 | Authentication is required to access this endpoint. |

**200 Response:** 
Schema: [`AuthWhoAmIResponse`](#schema-authwhoamiresponse)

<details>
<summary><strong>Code Samples</strong></summary>

**Shell (curl)**

```bash
curl -X GET "{BASE_URL}/api/auth/whoami" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json"
```

**C# (HttpClient)**

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

var response = await client.GetAsync(
    $"{baseUrl}/api/auth/whoami");
```

</details>


[↑ Quick Reference](#quick-reference)

---

### Health

<a id="get-apihealthlive"></a>
#### 🟢 GET `/api/health/live`

Returns a simple JSON-based liveness indicator for the backend.

Returns a minimal JSON payload that indicates whether the backend is currently reachable. This endpoint is primarily intended for use by the LumaCore Web UI and by external monitoring systems as a lightweight liveness probe.

**Responses**

| Status | Description |
|--------|-------------|
| ✅ 200 | OK |

<details>
<summary><strong>Code Samples</strong></summary>

**Shell (curl)**

```bash
curl -X GET "{BASE_URL}/api/health/live" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json"
```

**C# (HttpClient)**

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

var response = await client.GetAsync(
    $"{baseUrl}/api/health/live");
```

</details>


[↑ Quick Reference](#quick-reference)

---

## Schemas

### <a id="schema-adminjwtstatusinfo"></a>`AdminJwtStatusInfo`

Represents diagnostic information about the JWT configuration used by the LumaCore API.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `configured` | `boolean` | ✓ | `true` if a JWT issuer, audience, and signing key are all configured; otherwise, `false`. |
| `issuer` | `string` | ✓ | The configured JWT issuer, if any. |
| `audience` | `string` | ✓ | The configured JWT audience, if any. |
| `signingKey` | `string` | ✓ | A masked representation of the configured signing key, or `null` if no key is configured. The raw signing key is never exposed by this endpoint. |
| `accessTokenLifetimeMinutes` | `string (int32)` | ✓ | The configured access token lifetime in minutes, if available; otherwise `null`. |

<details>
<summary><strong>Example</strong></summary>

```json
{
  "configured": true,
  "issuer": "string",
  "audience": "string",
  "signingKey": "string",
  "accessTokenLifetimeMinutes": "string"
}
```

</details>

### <a id="schema-adminstatusresponse"></a>`AdminStatusResponse`

Represents the high-level status of the running LumaCore instance as
returned by the `/api/admin/status` endpoint.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `environment` | `string` | ✓ | The logical environment of the application (for example 'Development' or 'Production'). |
| `apiVersion` | `string` | ✓ | The advertised API version. |
| `machineName` | `string` | ✓ | The operating system machine name of the host. |
| `utcNow` | `string (date-time)` | ✓ | The current UTC time on the server. |
| `jwt` | `AdminJwtStatusInfo` | ✓ |  |

<details>
<summary><strong>Example</strong></summary>

```json
{
  "environment": "string",
  "apiVersion": "string",
  "machineName": "string",
  "utcNow": "2025-01-01T00:00:00Z",
  "jwt": null
}
```

</details>

### <a id="schema-authclaimitem"></a>`AuthClaimItem`

Represents a single claim in the authentication principal as exposed
by the `/auth/whoami` endpoint.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `type` | `string` | ✓ | The claim type (for example a URI or well-known claim name). |
| `value` | `string` | ✓ | The claim value. |

<details>
<summary><strong>Example</strong></summary>

```json
{
  "type": "string",
  "value": "string"
}
```

</details>

### <a id="schema-authintrospectresponse"></a>`AuthIntrospectResponse`

Represents the response returned by the `/auth/introspect` endpoint.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `subject` | `string` | ✓ | The logical subject of the token (usually the user identifier). |
| `name` | `string` | ✓ | The display name associated with the principal, if available. |
| `roles` | `string[]` | ✓ | The roles associated with the principal. |
| `notBeforeUtc` | `string (date-time)` | ✓ | The UTC time before which the token is not considered valid, if present. |
| `expiresUtc` | `string (date-time)` | ✓ | The UTC time at which the token expires, if present. |
| `expiresIn` | `string` | ✓ | The remaining lifetime of the token at the time of the request, or `null` if the expiry could not be determined. |
| `jwtId` | `string` | ✓ | The unique token identifier (jti claim), if present. |
| `issuer` | `string` | ✓ | The token issuer as read from the claims, if present. |
| `audience` | `string` | ✓ | The token audience as read from the claims, if present. |
| `configuredAccessTokenLifetimeMinutes` | `string (int32)` | ✓ | The configured access token lifetime in minutes as specified in JwtOptions. |

<details>
<summary><strong>Example</strong></summary>

```json
{
  "subject": "string",
  "name": "string",
  "roles": [
    "string"
  ],
  "notBeforeUtc": "2025-01-01T00:00:00Z",
  "expiresUtc": "2025-01-01T00:00:00Z",
  "expiresIn": "string",
  "jwtId": "string",
  "issuer": "string",
  "audience": "string",
  "configuredAccessTokenLifetimeMinutes": "string"
}
```

</details>

### <a id="schema-authwhoamiresponse"></a>`AuthWhoAmIResponse`

Represents the response returned by the `/auth/whoami` endpoint.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | `string` | ✓ | The logical name of the authenticated user. |
| `roles` | `string[]` | ✓ | The set of roles associated with the user. |
| `claims` | `AuthClaimItem[]` | ✓ | The raw claims associated with the user principal. |

<details>
<summary><strong>Example</strong></summary>

```json
{
  "name": "string",
  "roles": [
    "string"
  ],
  "claims": []
}
```

</details>

### <a id="schema-loginrequest"></a>`LoginRequest`

Represents login credentials submitted by a client in order to obtain a JWT.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `username` | `string` | ✓ | The username for authentication. Must be between 1 and 100 characters. |
| `password` | `string` | ✓ | The password for authentication. Must be at least 8 characters. |

<details>
<summary><strong>Example</strong></summary>

```json
{
  "username": "string",
  "password": "string"
}
```

</details>

---

*Generated by LumaCore.OpenApiGen on 2025-12-17 21:14:21 UTC*
