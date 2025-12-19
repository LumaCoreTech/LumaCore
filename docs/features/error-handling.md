# Error Handling Feature (ProblemDetails & Exception Handling)

The *Error Handling* feature provides centralized, RFC 7807-compliant error responses across the LumaCore API. It defines URN-based error type identifiers for machine-readable error categorization and a custom exception handler that ensures all unhandled exceptions are converted into structured responses with trace correlation.

> [!NOTE]
> **Cross-Cutting Feature:** Unlike endpoint features (*Auth*, *Admin*, etc.), the *Error Handling* feature provides infrastructure that affects all API responses. It integrates with ASP.NET Core's *ProblemDetails* services and exception handling middleware.
>
> This document deviates from the standard feature documentation structure because it defines API-wide response contracts that clients need to understand — including error type URNs and response formats.

---

## Components

### ErrorTypes

A static class containing URN-based error type identifiers for use in *ProblemDetails* responses. Using URNs (Uniform Resource Names) instead of URLs provides stable, machine-readable identifiers that don't depend on server availability.

| Constant | URN | Description |
|----------|-----|-------------|
| `Validation` | `urn:lumacore:error:validation` | Request body or parameters failed validation. |
| `NotFound` | `urn:lumacore:error:not-found` | Requested resource does not exist. |
| `Unauthorized` | `urn:lumacore:error:unauthorized` | Missing or invalid authentication credentials. |
| `Forbidden` | `urn:lumacore:error:forbidden` | Authenticated user lacks required permissions. |
| `Internal` | `urn:lumacore:error:internal` | Unexpected server error. |
| `Conflict` | `urn:lumacore:error:conflict` | Request conflicts with resource state. |
| `RateLimited` | `urn:lumacore:error:rate-limited` | Too many requests in time window. |

**Why URNs instead of URLs?**

- **Stability** — URNs are persistent identifiers that don't depend on server availability or URL structure changes.
- **No HTTP expectation** — Unlike URLs, URNs don't imply that the identifier should be dereferenceable via HTTP.
- **Namespace clarity** — The `urn:lumacore:error:` prefix clearly identifies these as LumaCore-specific error types.

---

### LumaCoreExceptionHandler

An `IExceptionHandler` implementation that converts unhandled exceptions into RFC 7807 *ProblemDetails* responses with trace correlation.

**Response format:**

```json
{
  "type": "urn:lumacore:error:internal",
  "title": "An unexpected error occurred",
  "status": 500,
  "instance": "/api/v1/some/endpoint",
  "traceId": "00-abc123def456..."
}
```

**Security considerations:**

- Exception details (message, stack trace) are **never** included in production responses.
- The `traceId` allows support teams to correlate client errors with server logs.
- Full exception information is logged server-side via Serilog.

---

## Integration Points

The *Error Handling* feature integrates with several other components:

| Component | Integration |
|-----------|-------------|
| *ValidationFilter* | Uses `ErrorTypes.Validation` for validation error responses. |
| *UseErrorHandlingFeature* | Maps HTTP status codes to appropriate *ErrorTypes* URNs. |
| *UseExceptionHandler* | Invokes *LumaCoreExceptionHandler* for unhandled exceptions. |

---

## Configuration

The *Error Handling* feature does not require configuration. It is registered in two places:

**Services** (`Program.Services.cs`):
```csharp
builder.Services.AddProblemDetails();
builder.AddErrorHandlingFeature();  // After AddProblemDetails()
```

**Pipeline** (`Program.Pipeline.cs`):
```csharp
app.UseExceptionHandler();
app.UseErrorHandlingFeature();  // After UseExceptionHandler()
```

---

## Registered Services

The *Error Handling* feature does not register any injectable services. It configures ASP.NET Core's built-in *ProblemDetails* services and exception handling middleware.

---

## Pipeline Order

The *Error Handling* feature registers middleware via `UseErrorHandlingFeature()`. It should be called early in the pipeline, after `UseExceptionHandler()` but before routing and authentication.

```
Request → ProxyHeaders → ExceptionHandler → ErrorHandling → HTTPS → ...
```

---

## Response Examples

### Validation Error (400)

```json
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

### Unauthorized (401)

```json
{
  "type": "urn:lumacore:error:unauthorized",
  "title": "Unauthorized",
  "status": 401
}
```

### Not Found (404)

```json
{
  "type": "urn:lumacore:error:not-found",
  "title": "Not Found",
  "status": 404
}
```

### Internal Server Error (500)

```json
{
  "type": "urn:lumacore:error:internal",
  "title": "An unexpected error occurred",
  "status": 500,
  "instance": "/api/v1/auth/login",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

---

## Typical Usage

Clients can use the `type` field to programmatically identify error categories:

```javascript
async function callApi(url) {
  const response = await fetch(url);
  
  if (!response.ok) {
    const problem = await response.json();
    
    switch (problem.type) {
      case 'urn:lumacore:error:validation':
        // Show field-level errors from problem.errors
        break;
      case 'urn:lumacore:error:unauthorized':
        // Redirect to login
        break;
      case 'urn:lumacore:error:forbidden':
        // Show "access denied" message
        break;
      default:
        // Show generic error with traceId for support
        console.error('Error traceId:', problem.traceId);
    }
  }
}
```

---

## Extending Error Types

To add new error types for domain-specific scenarios:

1. Add a new constant to `ErrorTypes`:

```csharp
/// <summary>
/// Indicates that a payment processing error occurred.
/// </summary>
public const string PaymentFailed = $"{Base}:payment-failed";
```

2. Use the type in endpoint handlers:

```csharp
return Results.Problem(
    type: ErrorTypes.PaymentFailed,
    title: "Payment processing failed",
    statusCode: StatusCodes.Status402PaymentRequired,
    detail: "The payment provider declined the transaction.");
```

---

© 2025 LumaCoreTech • MIT License
