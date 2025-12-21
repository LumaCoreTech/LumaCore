# Validation Feature (Request Validation)

The *Validation* feature provides automatic request validation using DataAnnotations, returning RFC 7807 compliant error responses for invalid requests.

> [!NOTE]
> This is a **foundation feature** that provides infrastructure for other features. It does not expose API endpoints itself.

---

## Overview

LumaCore validates incoming requests using standard .NET DataAnnotation attributes (`[Required]`, `[Range]`, `[StringLength]`, etc.). When validation fails, the API returns a `400 Bad Request` with a detailed `ProblemDetails` response.

Key behaviors:

- **Automatic validation** – All endpoints in the versioned API group are validated automatically.
- **RFC 7807 compliant** – Validation errors are returned as `ProblemDetails` with the `urn:lumacore:error:validation` type.
- **Property-level errors** – Each invalid property is listed with its specific error message.

---

## How It Works

The `ValidationFilter` inspects all endpoint arguments for DataAnnotation attributes and validates them before the handler executes. If validation fails, the request is short-circuited with a validation error response.

### Example Request

```http
POST /api/v1/auth/login HTTP/1.1
Content-Type: application/json

{
  "username": "",
  "password": "short"
}
```

### Example Response

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "urn:lumacore:error:validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Username": ["Username is required."],
    "Password": ["Password must be at least 8 characters."]
  }
}
```

---

## Supported Attributes

The validation filter supports all standard DataAnnotation attributes:

| Attribute | Purpose |
|-----------|---------|
| `[Required]` | Field must not be null or empty |
| `[StringLength]` | String length constraints |
| `[Range]` | Numeric range constraints |
| `[RegularExpression]` | Pattern matching |
| `[EmailAddress]` | Valid email format |
| `[MinLength]` / `[MaxLength]` | Collection or string length |
| `[Compare]` | Cross-property comparison |

Custom validation attributes implementing `ValidationAttribute` are also supported.

---

## Defining Validated Requests

Use record types with validation attributes for request models:

```csharp
public sealed record CreateItemRequest(
    [Required, StringLength(100)] string Name,
    [Range(0, 10000)] decimal Price,
    [StringLength(500)] string? Description);
```

The validation filter automatically validates these when used as endpoint parameters.

---

## Configuration

The *Validation* feature does not introduce additional configuration options. Validation is applied automatically to all endpoints in the versioned API group.

The feature is integrated via `WithValidation()` in the `MapVersionedApiGroup()` call in `Program.cs`.

---

## Registered Services

The *Validation* feature does not register injectable services. It operates as an endpoint filter applied to route groups.

---

## Related Features

- [*API Versioning*](api-versioning.md) — Validation is applied to the versioned API group
- [*Error Handling*](error-handling.md) — Provides the `ProblemDetails` infrastructure and error type URNs

---

© 2025 LumaCoreTech • MIT License