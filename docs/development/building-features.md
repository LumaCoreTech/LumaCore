# Building Features: Developer Guide

**Audience:** Developers implementing features.

**Prerequisites:**  
Before using this guide, you should:

- Read the [Feature Pattern](../architecture/feature-pattern.md) to understand how features fit into the overall architecture.
- Be familiar with the [Engineering Guidelines](engineering-guidelines.md) (why we design things the way we do).
- Know the [Coding Standards](coding-standards.md) (how code should look in LumaCore).

This document focuses on the *how*: it shows, step by step, how to build a feature that fits cleanly into LumaCore's architecture.

---

This guide provides practical, hands-on instructions for building features. It covers:

- **Contracts:** Defining HTTP request/response types
- **Options:** Configuring features with validated settings
- **Service Registration:** Choosing the right service lifetimes (Singleton, Scoped, Transient)
- **Endpoint Patterns:** Mapping HTTP routes effectively
- **Testing:** Writing tests for your features
- **Best Practices:** Common patterns and pitfalls to avoid
- **Real Examples:** Complete walkthrough of the *Auth* feature

---

## Table of Contents

1. [Core Integration Files](#core-integration-files-implementation-guide)
2. [Contracts](#contracts-the-features-public-api)
3. [Options](#options-feature-configuration)
4. [Validation Patterns](#validation-patterns)
5. [Testing Features](#testing-features)
6. [Common Patterns and Best Practices](#common-patterns-and-best-practices)
7. [Feature Checklist](#feature-checklist)
8. [Troubleshooting](#troubleshooting)
9. [Real-World Example: Auth Feature](#real-world-example-auth-feature)
10. [Advanced Topics](#advanced-topics)

---

## Core Integration Files: Implementation Guide

Every feature integrates through specialized files. Here's how to implement them:

### 1. ServiceRegistration.cs

Every feature that has services or configuration needs this file. It registers services and binds options with the DI container. This is called during application startup from `Program.Services.cs`.

**Signatures:**

```csharp
public static WebApplicationBuilder Add<Feature>Feature(
    this WebApplicationBuilder builder)

public static IServiceCollection Add<Feature>FeatureCore(
    this IServiceCollection services,
    IConfiguration configuration)
```

**Example:**

```csharp
/// <summary>
/// Provides extension methods for registering MyFeature services with the dependency injection container.
/// </summary>
/// <remarks>
/// This class is part of the MyFeature feature and configures options binding,
/// validation, and the supporting services needed for item management.
/// </remarks>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers options binding and supporting services for MyFeature
    /// using the <see cref="WebApplicationBuilder"/> facade.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper that forwards to <see cref="AddMyFeatureCore"/>
    /// using the <see cref="IServiceCollection"/> and <see cref="IConfiguration"/>
    /// exposed by the builder.
    /// </remarks>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The modified application builder.</returns>
    public static WebApplicationBuilder AddMyFeature(
        this WebApplicationBuilder builder)
    {
        builder.Services.AddMyFeatureCore(builder.Configuration);
        return builder;
    }
    
    /// <summary>
    /// Registers options binding and supporting services for MyFeature
    /// using the underlying <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// This method is factored to operate on <see cref="IServiceCollection"/> and
    /// <see cref="IConfiguration"/> so that it can be reused in other hosting scenarios
    /// and easily unit-tested.
    /// </remarks>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="configuration">The application configuration used to bind options.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddMyFeatureCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind and validate options
        services
            .AddOptions<MyFeatureOptions>()
            .Bind(configuration.GetSection(MyFeatureOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        // Register feature services
        services.AddSingleton<ITokenFactory, TokenFactory>();    // Stateless
        services.AddScoped<IRequestContext, RequestContext>();   // Per request
        services.AddTransient<IEmailSender, EmailSender>();      // Lightweight
        
        return services;
    }
}
```

The wrapper method (`AddMyFeature`) provides a clean API for `Program.cs`, while the core method (`AddMyFeatureCore`) is reusable in other hosting scenarios and easily unit-tested.

The pattern is straightforward: bind and validate options, register services with appropriate lifetimes, and return the builder for chaining.

#### Choosing Service Lifetimes

Choosing the right service lifetime is crucial. Get it wrong, and you'll have bugs that are extremely hard to diagnose.

**Singleton** — Use for stateless services or shared state:

```csharp
services.AddSingleton<ITokenFactory, TokenFactory>();
```

One instance for the entire application lifetime. The first request creates the instance; every subsequent request uses the same instance.

Imagine your API receives 10,000 requests per second. If you used Transient for `TokenFactory`, you'd create 10,000 new instances every second. But `TokenFactory` is stateless — it doesn't care which request it's serving. With Singleton, one instance serves all 10,000 requests. No allocations, no GC pressure, maximum performance.

When to use: service is stateless, expensive to create, manages shared state intentionally, or is thread-safe.

> [!WARNING]
> If you make a Singleton by mistake and it has mutable state, you'll get race conditions that only appear under load.

**Scoped** — Use for services tied to a request:

```csharp
services.AddScoped<IPersonaContext, PersonaContext>();
```

One instance per HTTP request. Created when the request starts, disposed when the request ends.

Imagine you're building a persona feature. Each request needs context about which persona is being used. If you used Singleton, all requests would share the same context — race condition! One user sees another user's data. With Scoped, each request gets its own instance. Request finishes, instance is disposed.

When to use: service holds request-specific state, interacts with request-scoped resources (like `DbContext`), or needs to share state across multiple injections in the same request.

Scoped services cannot be injected into Singleton services. The Singleton lives forever, so any Scoped service it receives would get trapped inside — imagine every request sharing the same `DbContext`. ASP.NET Core detects this at startup and throws an exception to prevent these subtle bugs.

**Transient** — Use for lightweight, short-lived services:

```csharp
services.AddTransient<IEmailSender, EmailSender>();
```

New instance every time it's requested. Even within the same request, two injections = two instances.

Imagine an email sender that connects to an SMTP server, sends one email, closes the connection. It's not thread-safe, it's lightweight, and it should be isolated. With Transient, each send is isolated — one failure doesn't cascade.

When to use: service is lightweight, not thread-safe, or needs isolation.

In practice, Transient is less common in LumaCore. Most services are either:

- Singleton (stateless, shared across requests), or
- Scoped (per-request context).

Use Transient when you have a specific reason for isolated instances (for example, lightweight, short-lived helpers that hold per-operation state).

#### Quick Decision Guide

1. **Does it have mutable state?** No → Singleton. Yes → Scoped or Transient.
2. **Is the state request-specific?** Yes → Scoped.
3. **Is it thread-safe?** Yes → Can be Singleton. No → Scoped or Transient.
4. **Does it need isolation?** Yes → Transient.

**Default rule:** Start with Singleton for stateless services, use Scoped for request context, only use Transient when you have a specific reason.

### 2. EndpointMapping.cs

Features that expose HTTP endpoints define them here. This file maps routes to handlers and attaches OpenAPI metadata. This is called during pipeline configuration from `Program.Pipeline.cs`.

**Signature:**

```csharp
public static IEndpointRouteBuilder Map<Feature>Feature(
    this IEndpointRouteBuilder endpoints)
```

> [!IMPORTANT]
> **Business API features** are mounted on the central versioned `/api/v{version}` route group in `Program.Pipeline.cs`. This group applies the `ValidationFilter` globally, so features should map **relative paths** (e.g., `/myfeature`, not `/api/v1/myfeature`). The `/api/v1` prefix is added automatically by the parent group.
>
> **Infrastructure endpoints** (like `/health`) are mapped directly to the application root, outside the versioned API group.

**Example (Business API Feature):**

```csharp
/// <summary>
/// Provides extension methods for mapping MyFeature endpoints to the application's routing pipeline.
/// </summary>
/// <remarks>
/// This class is part of the MyFeature feature and exposes endpoints for
/// retrieving and creating items.
/// </remarks>
public static class EndpointMapping
{
    /// <summary>
    /// Maps the MyFeature endpoints into the application's endpoint routing table.
    /// </summary>
    /// <param name="endpoints">
    /// The <see cref="IEndpointRouteBuilder"/> to map endpoints to. This is typically the
    /// versioned <c>/api/v{version}</c> route group from <c>Program.Pipeline.cs</c>, not the root application.
    /// </param>
    /// <returns>The <paramref name="endpoints"/> builder for method chaining.</returns>
    /// <remarks>
    /// This method groups the feature's endpoints under a common prefix (<c>/myfeature</c>)
    /// relative to the parent route group. The full path becomes <c>/api/v1/myfeature</c> when
    /// mounted on the central API group. It is intended to be called once during startup
    /// from <c>Program.Pipeline.cs</c>.
    /// </remarks>
    public static IEndpointRouteBuilder MapMyFeature(this IEndpointRouteBuilder endpoints)
    {
        // Note: Map relative paths only. The /api prefix is provided by the central
        // route group in Program.Pipeline.cs which also applies global validation.
        RouteGroupBuilder group = endpoints
            .MapGroup("/myfeature")
            .WithTags("MyFeature");
        
        group.MapGet("/items", HandleGetItems)
            .MapToApiVersion(ApiVersions.V1)
            .WithName("GetItems")
            .WithSummary("Returns all items.")
            .Produces<ItemResponse[]>(StatusCodes.Status200OK);
        
        group.MapPost("/items", HandleCreateItem)
            .MapToApiVersion(ApiVersions.V1)
            .RequireAuthorization()
            .WithName("CreateItem")
            .WithSummary("Creates a new item.")
            .Produces<ItemResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized);
        
        return endpoints;
    }
    
    // Handler methods...
}
```

**Key Points:**

1. **Every endpoint requires `MapToApiVersion()`:** This explicitly assigns each endpoint to an API version. LumaCore validates this at startup — the application will fail to start if any versioned endpoint is missing an explicit `MapToApiVersion()` call. Use `ApiVersions.V1` (or `V2`, etc.) from the central `ApiVersions` class

2. **Central `/api/v{version}` route group:** Business API features are mounted on a versioned route group in `Program.Pipeline.cs` that applies both API versioning and the `ValidationFilter` globally. Features map relative paths (e.g., `/myfeature`) and the `/api/v{version}/` prefix is added automatically

3. **Route groups organize related endpoints:** Features use `MapGroup()` to create a common prefix (e.g., `/myfeature`) that applies to all endpoints, reducing repetition and improving organization

4. **Minimal API style is preferred in LumaCore:** Direct route-to-handler mappings give good performance, less ceremony, and a clear code flow for the size and goals of this project. For large, existing MVC applications other trade-offs might make sense, but for LumaCore we standardize on Minimal APIs

5. **Authorization is explicit per endpoint:** Each endpoint must explicitly declare its security requirements with `RequireAuthorization()` or `AllowAnonymous()`. LumaCore validates this at startup — the application will fail to start if any versioned endpoint is missing an explicit authorization declaration

6. **OpenAPI metadata enables documentation:** Attributes like `WithName()`, `WithSummary()`, and `Produces()` generate Swagger documentation automatically, making the API self-documenting

7. **Dependencies are injected as parameters:** Handlers receive services directly as method parameters, not through constructor injection, keeping handlers focused and testable

#### Handler Patterns

**Pattern 1: Inline Lambda** — For simple logic:

```csharp
group.MapGet("/ping", () => Results.Ok(new PingResponse("pong")))
    .MapToApiVersion(ApiVersions.V1)
    .WithName("Ping");
```

**Pattern 2: Private Method** — Extract for readability:

```csharp
group.MapPost("/items", HandleCreateItem)
    .MapToApiVersion(ApiVersions.V1);

private static async Task<IResult> HandleCreateItem(
    CreateItemRequest request,
    IMyFeatureService service)
{
    // Request is already validated — focus on business logic
    var item = await service.CreateAsync(request);
    
    return Results.Ok(new ItemResponse(item.Id, item.Name));
}
```

Benefits: cleaner endpoint mapping, easier to test, better for complex logic.

**Pattern 3: Separate Handler Class** — For very complex handlers (50+ lines):

```csharp
// EndpointMapping.cs
group.MapPost("/batch-import", MyFeatureHandlers.BatchImport)
    .MapToApiVersion(ApiVersions.V1);

// MyFeatureHandlers.cs
internal static class MyFeatureHandlers
{
    internal static async Task<IResult> BatchImport(
        BatchImportRequest request,
        IMyFeatureService service)
    {
        // Complex logic here
    }
}
```

### 3. MiddlewareIntegration.cs

Features that need to hook into the HTTP pipeline provide this file. It integrates custom middleware for request/response processing. This is also called from `Program.Pipeline.cs` — middleware order matters.

**Signature:**

```csharp
public static IApplicationBuilder Use<Feature>Feature(
    this IApplicationBuilder app)
```

**Example:**

```csharp
/// <summary>
/// Provides extension methods for integrating MyFeature middleware in the request pipeline.
/// </summary>
public static class MiddlewareIntegration
{
    /// <summary>
    /// Configures the application to use MyFeature middleware.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    public static IApplicationBuilder UseMyFeature(this IApplicationBuilder app)
    {
        app.UseMiddleware<MyFeatureMiddleware>();
        return app;
    }
}
```

---

## Contracts: The Feature's Public API

Every feature defines its HTTP endpoints' request and response types through **Contracts** (DTOs). These live in the dedicated `LumaCore.Api.Contracts` project.

> 💡 **See [Contract Design Rules](#contract-design-rules)** below for detailed conventions.

### Why a Separate Project?

```
LumaCore.Api.Contracts/
├── V1/
│   ├── MyFeature/
│   │   ├── CreateItemRequest.cs
│   │   ├── ItemResponse.cs
│   │   └── ...
│   └── AnotherFeature/
│       └── SomeResponse.cs
└── V2/                              # Future versions
```

At first glance, a separate contracts project might seem like unnecessary ceremony. Why not just put DTOs inside the feature folders?

**The answer lies in understanding what a Contract is:**

A **Contract** is a promise to the outside world. It says: *"This is the shape of data I accept, and this is the shape of data I return."* Once published, it becomes part of your API surface. Clients depend on it. Breaking changes affect everyone who calls your API.

**The key benefit: Shared access without circular dependencies**

```
LumaCore.Api.Contracts (DTOs only, minimal dependencies)
         ↑
    ┌────┴────┐
    │         │
LumaCore.Api  LumaCore.Ui.Web
```

Both the API and the Blazor UI can reference the same contract types:
- **API** uses contracts for endpoint parameters and responses
- **Blazor UI** uses contracts for HTTP client calls

Without a separate project, you'd either duplicate DTOs or create circular dependencies.

**Real-world benefit - API versioning:**

Contracts are organized by version within the project:

```
LumaCore.Api.Contracts/
├── V1/
│   └── MyFeature/
│       └── CreateItemRequest.cs      # Original contract
└── V2/
    └── MyFeature/
        └── CreateItemRequest.cs      # Extended contract with new fields
```

The service layer doesn't duplicate - only the contracts. Internal logic is reused across versions.

**Contract organization conventions:**

1. **Namespace follows folder structure:**
   ```csharp
   // In LumaCore.Api.Contracts/V1/MyFeature/CreateItemRequest.cs
   namespace LumaCore.Api.Contracts.V1.MyFeature;
   
   // In LumaCore.Api.Contracts/V2/MyFeature/CreateItemRequest.cs
   namespace LumaCore.Api.Contracts.V2.MyFeature;
   ```

2. **Use using aliases in EndpointMapping for clarity:**
   ```csharp
   using V1 = LumaCore.Api.Contracts.V1.MyFeature;
   using V2 = LumaCore.Api.Contracts.V2.MyFeature;
   
   // V1 endpoint
   group.MapPost("/items", (V1.CreateItemRequest request) => HandleCreateV1(request))
       .MapToApiVersion(ApiVersions.V1);
   
   // V2 endpoint with extended DTO
   group.MapPost("/items", (V2.CreateItemRequest request) => HandleCreateV2(request))
       .MapToApiVersion(ApiVersions.V2);
   ```

3. **Contracts grouped by feature within each version:**
   ```
   V1/
   ├── MyFeature/           # Feature contracts
   ├── AnotherFeature/      # Feature contracts
   └── ...
   ```

4. **XMLDocs include full versioned path:**
   ```csharp
   /// <summary>
   /// Represents the response returned by the <c>/api/v1/myfeature/items</c> endpoint.
   /// </summary>
   public sealed record ItemResponse(string Id, string Name);
   ```

> [!IMPORTANT]
> **All contracts start in `V1/`** from day one. This ensures consistent structure and makes future versioning seamless. Organize by feature within each version folder. Only place contracts directly in the project root if they are truly shared across all versions.

**Another benefit - serialization concerns:**

Types in the Contracts project have special requirements:
- Must be JSON-serializable
- Should be immutable (records)
- Need DataAnnotations for validation
- Require careful thought about breaking changes

This follows the type conventions defined in the LumaCore Coding Standards: DTOs are immutable records by default, with changes treated as potential API-breaking changes.

**In short:** A separate Contracts project isn't ceremony - it's a clear architectural boundary that enables sharing between API and UI while preventing circular dependencies. This clarity prevents accidental breaking changes and makes the codebase easier to reason about.

### Contract Design Rules

**Use records for immutability:**
```csharp
public sealed record CreateItemRequest(
    string Name,
    string Description);

public sealed record ItemResponse(
    Guid Id,
    string Name);
```

**Add validation** (see [Validation Patterns](#validation-patterns) for details):
```csharp
public sealed record CreateItemRequest(
    [Required, MinLength(3)] string Name,
    [MaxLength(500)] string Description);
```

**Document with XML docs:**
```csharp
/// <summary>
/// Request payload for creating a new item.
/// </summary>
/// <param name="Name">Item name (3+ characters)</param>
/// <param name="Description">Optional description (max 500 characters)</param>
public sealed record CreateItemRequest(
    [Required, MinLength(3)] string Name,
    [MaxLength(500)] string Description);
```

---

## Options: Feature Configuration

Features that need configuration follow the Options pattern.

> 💡 **See [Options Registration](#options-registration) and [Options Usage](#options-usage)** below for implementation details.

### Options Class Structure

```csharp
/// <summary>
/// Configuration options for MyFeature.
/// </summary>
public sealed class MyFeatureOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "MyFeature";
    
    /// <summary>
    /// Gets or sets the API endpoint URL.
    /// </summary>
    [Required(ErrorMessage = "MyFeature:Endpoint must be configured.")]
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the API key (minimum 32 characters).
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "MyFeature:ApiKey must be at least 32 characters.")]
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the timeout in seconds (1-300).
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
```

Options classes are `sealed` (not designed for inheritance) and include a `SectionName` constant pointing to the configuration section. Add validation (see [Validation Patterns](#validation-patterns)), provide sensible default values, and add full XML documentation — especially for complex options.

### Options Registration

Always validate at startup (see [Validation Patterns](#validation-patterns) for attribute details):

```csharp
services
    .AddOptions<MyFeatureOptions>()
    .Bind(configuration.GetSection(MyFeatureOptions.SectionName))
    .ValidateDataAnnotations()      // Check [Required], [Range], etc.
    .ValidateOnStart();             // Fail at startup, not runtime
```

### Options Usage

Inject via `IOptions<T>`:

```csharp
public MyFeatureService(IOptions<MyFeatureOptions> options)
{
    mOptions = options.Value;  // Get the validated options
}
```

---

## Validation Patterns

Both Contracts and Options require validation. This section covers the common patterns.

| Attribute | Purpose |
|-----------|---------|
| `[Required]` | Must be provided |
| `[MinLength(n)]` | Minimum string length |
| `[MaxLength(n)]` | Maximum string length |
| `[Range(min, max)]` | Numeric range |
| `[Url]` | Valid URL format |
| `[EmailAddress]` | Valid email format |
| `[RegularExpression(pattern)]` | Regex pattern |

Always provide meaningful `ErrorMessage` — generic messages like "Validation failed" help no one:

```csharp
[Required(ErrorMessage = "MyFeature:Secret must be configured. " +
    "Set configuration key 'MyFeature:Secret' or environment variable 'MyFeature__Secret'.")]
[MinLength(32, ErrorMessage = "MyFeature:Secret must be at least 32 characters.")]
public string Secret { get; set; } = string.Empty;
```

For complex validations and validations that span multiple properties, implement `IValidatableObject`:

```csharp
public sealed class ProxyHeadersOptions : IValidatableObject
{
    public ForwardedHeaderMode Mode { get; set; }
    public List<string> TrustedProxies { get; set; } = [];
    public List<string> TrustedNetworks { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Mode == ForwardedHeaderMode.SelfManaged &&
            TrustedProxies.Count == 0 && TrustedNetworks.Count == 0)
        {
            yield return new ValidationResult(
                "SelfManaged mode requires at least one TrustedProxy or TrustedNetwork.",
                [nameof(TrustedProxies), nameof(TrustedNetworks)]);
        }
    }
}
```

---

## Testing Features

The feature architecture is designed with testability in mind. The two-method pattern in `ServiceRegistration.cs` (`AddMyFeature` vs `AddMyFeatureCore`) enables unit testing without spinning up the full application, while `WebApplicationFactory` provides a realistic environment for integration tests.

### Unit Testing Service Registration

The `AddMyFeatureCore` method accepts `IServiceCollection` and `IConfiguration` directly, making it easy to test service registration in isolation:

```csharp
[Fact]
public void AddMyFeatureCore_RegistersRequiredServices()
{
    // Arrange
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["MyFeature:Endpoint"] = "https://api.example.com",
            ["MyFeature:ApiKey"] = "this-is-a-32-character-secret-key",
            ["MyFeature:TimeoutSeconds"] = "30"
        })
        .Build();
    
    // Act
    services.AddMyFeatureCore(configuration);
    ServiceProvider provider = services.BuildServiceProvider();
    
    // Assert
    IMyFeatureService? service = provider.GetService<IMyFeatureService>();
    Assert.NotNull(service);
}
```

This test verifies that your feature registers all required services correctly. If a dependency is missing, the test fails immediately — not at runtime in production.

### Integration Testing Endpoints

For endpoint testing, `WebApplicationFactory<Program>` spins up your actual application in memory. This gives you a real HTTP client that exercises the full pipeline: routing, model binding, validation, authorization, and your handler code.

```csharp
[Fact]
public async Task CreateItem_WithValidRequest_ReturnsItem()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    HttpClient client = factory.CreateClient();
    var request = new CreateItemRequest("Test Item");
    
    // Act
    HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/myfeature/items", request);
    
    // Assert
    response.EnsureSuccessStatusCode();
    ItemResponse? itemResponse = await response.Content
        .ReadFromJsonAsync<ItemResponse>();
    Assert.NotNull(itemResponse);
    Assert.Equal("Test Item", itemResponse.Name);
}
```

Integration tests catch issues that unit tests miss: incorrect route definitions, missing OpenAPI metadata, authorization misconfigurations, and serialization problems.

---

## Common Patterns and Best Practices

These patterns have proven themselves across LumaCore's features. They're not arbitrary rules — each one prevents real problems we've encountered or anticipate.

### 1. Fail-Fast Configuration

Configuration errors should crash the application at startup, not cause mysterious failures at 3 AM.

```csharp
services
    .AddOptions<MyOptions>()
    .ValidateDataAnnotations()
    .ValidateOnStart();  // ← Crucial!
```

Without `ValidateOnStart()`, a missing or invalid configuration value only surfaces when code first accesses the option — potentially hours after deployment, under load, in production. With it, the application refuses to start until configuration is correct. This turns "debugging production" into "fixing a failed deployment" — a much better situation.

### 2. Structured Logging

Log messages should be searchable and parseable, not just human-readable.

```csharp
logger.LogInformation(
    "User {Username} authenticated successfully",
    username);  // Structured parameter
```

The `{Username}` placeholder creates a structured property that log aggregation tools (Seq, Elasticsearch, Application Insights) can filter and query. Instead of grepping through millions of log lines, you can query `WHERE Username = 'admin'`. This transforms debugging from archaeology into analytics.

### 3. Explicit Authorization

Every endpoint should declare its security requirements explicitly, even if it seems redundant.

```csharp
// ✅ Good - Clear intent
group.MapDelete("/items/{id}", HandleDeleteItem)
    .MapToApiVersion(ApiVersions.V1)
    .RequireAuthorization(new AuthorizeAttribute { Roles = "admin" });

// ❌ Bad - Relies on group-level auth
group.MapDelete("/items/{id}", HandleDeleteItem)
    .MapToApiVersion(ApiVersions.V1);
```

When authorization is implicit (inherited from the group), it's invisible at the endpoint level. A developer reading the code can't tell if the endpoint is protected without tracing back through the group configuration. Explicit authorization makes security requirements visible exactly where they matter — at the endpoint definition. It also prevents accidental exposure if someone refactors the group structure.

### 4. OpenAPI Documentation

Every endpoint should be fully documented for Swagger/OpenAPI. This isn't just about generating pretty documentation — it's about making the API discoverable and self-describing.

> 💡 This section is a comprehensive reference. You don't need to memorize it — use it as a lookup when documenting your endpoints.

```csharp
group.MapPost("/items", HandleCreateItem)
    .MapToApiVersion(ApiVersions.V1)
    .WithName("CreateItem")
    .WithSummary("Create a new item")
    .WithDescription("Creates a new item and returns the created resource")
    .Produces<ItemResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest);
```

Good OpenAPI metadata serves multiple purposes: it generates accurate Swagger UI for developers exploring the API, it enables code generation for client SDKs, and it acts as living documentation that stays in sync with the code. When the metadata is incomplete or wrong, developers waste time guessing what the API actually does.

#### Complete OpenAPI Metadata Reference

ASP.NET Core Minimal APIs provide extensive OpenAPI customization. Here's a reference of the most useful methods.

**Basic Metadata**

These methods define how the endpoint appears in Swagger UI. The operation ID (`WithName`) must be unique across the entire API — it's used for code generation and deep linking.

```csharp
.WithName("CreateItem")                   // Operation ID (unique identifier)
.WithSummary("Create a new item")         // Short description (1 line)
.WithDescription("Detailed description")  // Long description (multiple lines)
.WithTags("Items", "MyFeature")           // Group in Swagger UI
```

**Response Documentation**

Document all possible responses, not just the happy path. This helps API consumers handle errors correctly and enables accurate client code generation.

```csharp
// Success responses
.Produces<ItemResponse>(StatusCodes.Status200OK)
.Produces<ItemResponse>(StatusCodes.Status201Created)

// Error responses (document what's NOT automatic)
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status500InternalServerError)
```

> 💡 You don't need to document `400 Bad Request` or `401 Unauthorized` manually — LumaCore adds these automatically for endpoints with request bodies or authentication requirements. Endpoints with roles or policies also get `403 Forbidden` documented (authorization).

**Request Documentation**

Specify which content types the endpoint accepts. Most endpoints only need JSON, but you can support multiple formats if needed.

```csharp
.Accepts<CreateItemRequest>("application/json")
.Accepts<CreateItemRequest>("application/xml")  // If XML support enabled
```

**Advanced Control with Operation Transformers**

When the fluent methods aren't enough, LumaCore uses `IOpenApiOperationTransformer` for advanced customization. This is the .NET 9+ native approach — there is no `WithOpenApi()` method.

LumaCore already includes `SecurityResponsesTransformer` which automatically documents 401/403 responses for protected endpoints. For custom transformations, create your own transformer:

```csharp
// In Features/MyFeature/MyFeatureOpenApiTransformer.cs
public sealed class MyFeatureOpenApiTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Only transform endpoints in this feature
        string? path = context.Description.RelativePath;
        if (path is null || !path.Contains("/myfeature/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }
        
        // Add custom examples, headers, or other metadata
        // operation.RequestBody, operation.Responses, etc.
        
        return Task.CompletedTask;
    }
}

// Register in Features/OpenApi/ServiceRegistration.cs
services.AddOpenApi(documentName, options =>
{
    options.AddOperationTransformer<MyFeatureOpenApiTransformer>();
});
```

> [!TIP]
> For most endpoints, `WithSummary()`, `WithDescription()`, and `WithTags()` are sufficient. Only use transformers when you need to modify the OpenAPI operation object directly.

**Exclude from Documentation**

Some endpoints are internal and shouldn't appear in public documentation — health checks, debug endpoints, or internal admin tools.

```csharp
.ExcludeFromDescription()  // Don't show in Swagger UI (internal endpoints)
```

**Deprecation**

When retiring an endpoint, mark it as deprecated rather than removing it immediately. This gives API consumers time to migrate. Use the `Deprecated` attribute:

```csharp
group.MapGet("/legacy", HandleLegacy)
    .MapToApiVersion(ApiVersions.V1)
    .WithSummary("⚠️ DEPRECATED: Use /v2/items instead")
    .WithMetadata(new ObsoleteAttribute("Use /v2/items instead"));
```

#### Schema Documentation via Attributes

DTOs can be documented with attributes that appear in OpenAPI schema:

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request payload for creating a new item.
/// </summary>
public sealed record CreateItemRequest(
    /// <summary>
    /// The item name (3-100 characters).
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [MinLength(3, ErrorMessage = "Name must be at least 3 characters")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    [Description("Item's display name")]
    string Name,
    
    /// <summary>
    /// Optional description (max 500 characters).
    /// </summary>
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    [Description("Item's description")]
    string? Description
);
```

**What appears in Swagger:**
- `[Required]` → Field marked as required
- `[MinLength]` / `[MaxLength]` → String length constraints
- `[Range]` → Numeric value constraints
- `[RegularExpression]` → Pattern validation
- `[EmailAddress]` / `[Url]` / `[Phone]` → Format validation
- `[Description]` → Field description in schema
- XML comments (`/// <summary>`) → Additional documentation

#### Response Examples

For response examples, use XML documentation on your DTO types or create a schema transformer:

```csharp
/// <summary>
/// Response containing created item details.
/// </summary>
/// <example>
/// {
///   "id": "550e8400-e29b-41d4-a716-446655440000",
///   "name": "My Item"
/// }
/// </example>
public sealed record ItemResponse(Guid Id, string Name);
```

For more complex example scenarios, implement `IOpenApiSchemaTransformer` to programmatically add examples to your schemas.

#### Complete Example

Fully documented endpoint with all metadata:

```csharp
group.MapPost("/items", HandleCreateItem)
    .MapToApiVersion(ApiVersions.V1)
    
    // Basic metadata
    .WithName("CreateItem")
    .WithSummary("Create a new item")
    .WithDescription(@"
        Creates a new item with the provided details.
        
        **Workflow:**
        1. Submit item details via POST request
        2. Receive created item with generated ID
        3. Use the ID for subsequent operations
        
        **Validation:** Name is required, Description is optional.
    ")
    .WithTags("Items", "MyFeature")
    
    // Request/Response types
    .Accepts<CreateItemRequest>("application/json")
    .Produces<ItemResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest, "application/problem+json")
    
    // Security
    .RequireAuthorization();
```

> [!NOTE]
> For request/response examples, use XML documentation on your DTO types. For more advanced OpenAPI customization, implement an `IOpenApiOperationTransformer`.

#### XML Documentation for Handlers

If you enable XML documentation for the project, handler method XML comments are included:

```csharp
/// <summary>
/// Creates a new item.
/// </summary>
/// <param name="request">Item creation request</param>
/// <param name="service">Feature service</param>
/// <returns>Created item on success</returns>
/// <response code="200">Returns the created item</response>
/// <response code="400">Invalid request format</response>
private static async Task<IResult> HandleCreateItem(
    CreateItemRequest request,
    IMyFeatureService service)
{
    // Implementation...
}
```

**`.Produces()` vs `/// <response>` — What's the difference?**

Both contribute to OpenAPI documentation, but they serve different purposes:

- **`.Produces<T>(StatusCode)`** defines the response *schema* — it tells OpenAPI "this endpoint can return this type with this status code". This is required for correct schema generation.

- **`/// <response code="...">`** adds a *description* — it tells developers "this is what this status code means". This is optional and only works when XML docs are enabled.

For the best documentation, use both: `.Produces()` for the schema, XML docs for the explanation.

To enable XML docs:
```xml
<!-- In .csproj -->
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn> <!-- Disable missing XML comment warnings -->
</PropertyGroup>
```

With `GenerateDocumentationFile` enabled, .NET's native OpenAPI support automatically includes XML comments in the generated specification — no additional configuration needed in `Program.cs`.

#### Testing Your Documentation

Always verify your OpenAPI documentation looks correct:

1. **Run application:** `dotnet run`
2. **Open Swagger UI:** `http://localhost:5080/swagger`
3. **Check each endpoint:**
   - Summary is clear and concise
   - Description provides enough detail
   - Request/Response schemas are correct
   - Examples are helpful
   - Status codes are documented
4. **Test "Try it out":** Ensure examples work

#### Common Pitfalls

These mistakes lead to confusing or incomplete documentation. The comments explain what's wrong.

**❌ Don't:**
```csharp
// Too vague
.WithSummary("Create")

// Missing status codes
.Produces<ItemResponse>()  // Only 200, what about errors?

// No description
.WithName("CreateItem")  // What does this do?
```

**✅ Do:**
```csharp
// Clear and descriptive
.WithSummary("Create a new item with name and description")

// All possible status codes
.Produces<ItemResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)

// Helpful description
.WithDescription("Creates a new item and returns it with a generated ID")
```

#### Quick Reference

| Method | Purpose | Required? |
|--------|---------|-----------|
| `.WithName()` | Operation ID (must be unique) | Recommended |
| `.WithSummary()` | Short description (1 line) | **Required** |
| `.WithDescription()` | Detailed description (markdown) | Recommended |
| `.WithTags()` | Group in Swagger UI | Recommended |
| `.Produces<T>()` | Success response type | **Required** |
| `.Produces()` | Error status codes | **Required** |
| `.Accepts<T>()` | Request content type | Optional |
| `.ExcludeFromDescription()` | Hide from Swagger | Rare |

**Rule of thumb** — every endpoint should have at minimum:
- `.WithName()` - Unique identifier
- `.WithSummary()` - What it does
- `.Produces<T>()` - Success response
- `.Produces()` - Common errors (400, 401, 404, 500)

### 5. Use Records for DTOs

Data Transfer Objects should be immutable. Once created, they shouldn't change.

```csharp
// ✅ Good - Immutable record
public sealed record CreateItemRequest(string Name, string Description);

// ❌ Bad - Mutable class
public class CreateItemRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
}
```

Records give you immutability by default, value-based equality (two requests with the same data are equal), and concise syntax. They also play nicely with pattern matching and `with` expressions. For DTOs that cross HTTP boundaries, there's rarely a reason to use mutable classes.

### 6. Error Responses

All error responses in LumaCore use the ProblemDetails format (RFC 7807). This provides a consistent, machine-readable structure for errors across all endpoints.

> 💡 The infrastructure for this is configured once in `Program.cs` via `AddProblemDetails()` and `UseExceptionHandler()`. You don't need to set this up per feature.

**Request validation is automatic.** LumaCore validates incoming requests globally based on the DataAnnotations on your contracts. If validation fails, clients receive a standardized `ValidationProblem` response — you don't need to check this in your handlers.

```csharp
// Just add DataAnnotations to your contracts
public sealed record CreateItemRequest(
    [Required, MinLength(3)] string Name,
    [MaxLength(500)] string Description);

// Your handler receives only valid requests
private static IResult HandleCreate(CreateItemRequest request)
{
    // No validation code needed — request is guaranteed valid
    // Focus on business logic
}
```

**For business logic errors,** return ProblemDetails explicitly:

```csharp
// Resource not found
return TypedResults.NotFound();

// Business rule violation
return Results.Problem(
    detail: "Cannot delete an item that has active references.",
    statusCode: StatusCodes.Status409Conflict);
```

**What clients receive:**

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "Bad Request",
    "status": 400,
    "detail": "Cannot delete an item that has active references.",
    "instance": "/api/v1/items/123",
    "traceId": "00-1234abcd..."
}
```

**Document your error responses in OpenAPI:**

```csharp
group.MapGet("/items/{id}", HandleGetItem)
    .MapToApiVersion(ApiVersions.V1)
    .Produces<ItemResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);
```

> 💡 You don't need to add `ProducesValidationProblem()` — LumaCore automatically documents the 400 response for endpoints that accept a request body. Similarly, endpoints with `RequireAuthorization()` automatically get a 401 response documented. If the endpoint also specifies roles or policies, a 403 response is added as well.

This makes your API predictable — clients always know the shape of error responses and can handle them programmatically.

---

## Feature Checklist

When creating a new feature, ensure:

### File Structure
- [ ] `ServiceRegistration.cs` with `Add<Feature>Feature()` method
- [ ] `EndpointMapping.cs` with `Map<Feature>Feature()` method
- [ ] DTOs in `LumaCore.Api.Contracts/V1/<Feature>/` folder
- [ ] `<Feature>Options.cs` if configuration is needed
- [ ] Service interfaces and implementations if needed

### Code Quality
- [ ] All public APIs have XML documentation
- [ ] Options classes use DataAnnotations
- [ ] Options registered with `ValidateOnStart()`
- [ ] Services registered with appropriate lifetime
- [ ] Endpoints have OpenAPI metadata
- [ ] Authorization requirements are explicit

### Registration
- [ ] Feature registered in `Program.Services.cs`
- [ ] Feature mapped in `Program.Pipeline.cs`
- [ ] Configuration section added to `appsettings.json`

### Documentation
- [ ] Feature documented in `docs/features/<feature>.md`
- [ ] Navigation links updated in documentation

### Testing
- [ ] Unit tests for service registration
- [ ] Integration tests for endpoints
- [ ] Options validation tested

---

## Troubleshooting

Common issues and how to resolve them:

| Problem | Solution |
|---------|----------|
| `InvalidOperationException: Cannot consume scoped service from singleton` | Check service lifetimes — you're injecting a Scoped service into a Singleton. Either make the consumer Scoped, or inject `IServiceScopeFactory` instead. |
| OpenAPI shows no XML documentation | Enable `GenerateDocumentationFile` in `.csproj`. With native OpenAPI, XML comments are included automatically. |
| Configuration not loading | Verify section name matches `appsettings.json`. Check `appsettings.{Environment}.json` and environment variables. |
| `ValidateOnStart` not catching errors | Ensure you called both `ValidateDataAnnotations()` and `ValidateOnStart()` in the options registration chain. |
| Endpoint returns 404 | Check that `MapMyFeature()` is called in `Program.Pipeline.cs` and the route prefix matches your request. |

---

## Real-World Example: Auth Feature

Let's walk through the complete *Auth* feature as a real example:

### Step 1: Define Contracts

Contracts live in the `LumaCore.Api.Contracts` project:

```csharp
// LumaCore.Api.Contracts/V1/Auth/LoginRequest.cs
namespace LumaCore.Api.Contracts.V1.Auth;

public sealed record LoginRequest(
    [Required, MinLength(3)] string Username,
    [Required, MinLength(8)] string Password);

// LumaCore.Api.Contracts/V1/Auth/LoginResponse.cs
namespace LumaCore.Api.Contracts.V1.Auth;

public sealed record LoginResponse(string AccessToken);

// LumaCore.Api.Contracts/V1/Auth/AuthWhoAmIResponse.cs
namespace LumaCore.Api.Contracts.V1.Auth;

public sealed record AuthWhoAmIResponse(
    string Name,
    string[] Roles,
    AuthClaimItem[] Claims);

// LumaCore.Api.Contracts/V1/Auth/AuthClaimItem.cs
namespace LumaCore.Api.Contracts.V1.Auth;

public sealed record AuthClaimItem(string Type, string Value);
```

### Step 2: Define Options

```csharp
// JwtOptions.cs
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

### Step 3: Create Service

```csharp
// IJwtTokenFactory.cs
public interface IJwtTokenFactory
{
    string CreateToken(string subject, IEnumerable<Claim> claims);
}

// JwtTokenFactory.cs
public sealed class JwtTokenFactory : IJwtTokenFactory
{
    private readonly JwtOptions mOptions;
    private readonly byte[] mSigningKeyBytes;
    
    public JwtTokenFactory(IOptions<JwtOptions> options)
    {
        mOptions = options.Value;
        mSigningKeyBytes = Encoding.UTF8.GetBytes(mOptions.SigningKey);
    }
    
    public string CreateToken(string subject, IEnumerable<Claim> claims)
    {
        // Token creation logic...
    }
}
```

### Step 4: Register Services

```csharp
// ServiceRegistration.cs
public static class ServiceRegistration
{
    public static WebApplicationBuilder AddAuthFeature(
        this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthFeatureCore(builder.Configuration);
        return builder;
    }
    
    public static IServiceCollection AddAuthFeatureCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        // Configure authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(/* ... */);
        
        // Register services
        services.AddAuthorization();
        services.AddSingleton<IJwtTokenFactory, JwtTokenFactory>();
        
        return services;
    }
}
```

### Step 5: Map Endpoints

```csharp
// EndpointMapping.cs
public static class EndpointMapping
{
    public static IEndpointRouteBuilder MapAuthFeature(
        this IEndpointRouteBuilder endpoints)
    {
        // Note: Map relative paths. The /api prefix comes from the central route group.
        RouteGroupBuilder group = endpoints.MapGroup("/auth")
            .WithTags("Auth");
        
        group.MapPost("/login", HandleLogin)
            .MapToApiVersion(ApiVersions.V1)
            .WithName("AuthLogin")
            .WithSummary("Authenticate and obtain an access token.")
            .Produces<LoginResponse>();
        
        group.MapGet("/whoami", HandleWhoAmI)
            .MapToApiVersion(ApiVersions.V1)
            .RequireAuthorization()
            .WithName("AuthWhoAmI")
            .WithSummary("Returns the current user's identity and claims.")
            .Produces<AuthWhoAmIResponse>();
        
        return endpoints;
    }
    
    private static IResult HandleLogin(
        LoginRequest request,
        IJwtTokenFactory tokenFactory)
    {
        // Handler logic...
    }
    
    private static IResult HandleWhoAmI(ClaimsPrincipal user)
    {
        // Handler logic...
    }
}
```

### Step 6: Wire Into Application

```csharp
// Program.Services.cs
builder.AddAuthFeature();  // ← One line!

// Program.Pipeline.cs
RouteGroupBuilder api = app.MapVersionedApiGroup();  // ← /api/v{version} with validation

api.MapAuthFeature();   // ← Maps to /api/v1/auth/*
```

**That's it!** The feature is completely integrated and working.

---

## Advanced Topics

### Feature Flags

Features can be disabled via configuration. The check happens inside the feature itself:

```csharp
// In MyFeatureOptions.cs
public bool Enabled { get; set; } = true;

// In MiddlewareIntegration.cs (analogous for ServiceRegistration.cs and EndpointMapping.cs)
public static IApplicationBuilder UseMyFeature(this IApplicationBuilder app)
{
    MyFeatureOptions options = app.ApplicationServices
        .GetRequiredService<IOptions<MyFeatureOptions>>().Value;
    
    if (!options.Enabled)
        return app;  // No-op when disabled
    
    // Apply middleware...
    return app;
}
```

This keeps `Program.cs` clean — no conditional registration needed.

### Feature Versioning

When you need to make breaking changes to an API, versioning lets you evolve without breaking existing clients. Old clients continue using V1 while new clients adopt V2.

LumaCore uses the `Asp.Versioning` library with URL segment-based versioning (`/api/v1/...`, `/api/v2/...`). Version-specific endpoints are mapped using `MapToApiVersion()`:

```csharp
// EndpointMapping.cs - Single file handles multiple versions
using V1 = LumaCore.Api.Contracts.V1.MyFeature;
using V2 = LumaCore.Api.Contracts.V2.MyFeature;

public static IEndpointRouteBuilder MapMyFeature(this IEndpointRouteBuilder group)
{
    // V1 endpoint (original contract)
    group.MapPost("/items", (V1.CreateItemRequest request) => HandleCreateV1(request))
        .MapToApiVersion(ApiVersions.V1);
    
    // V2 endpoint (extended contract with new fields)
    group.MapPost("/items", (V2.CreateItemRequest request) => HandleCreateV2(request))
        .MapToApiVersion(ApiVersions.V2);
    
    return group;
}
```

Each version has its own contracts in the Contracts project, but features share the same service layer. Only the API surface is duplicated — not the business logic:

```
LumaCore.Api.Contracts/
├── V1/
│   └── MyFeature/
│       └── CreateItemRequest.cs   # Old format
└── V2/
    └── MyFeature/
        └── CreateItemRequest.cs   # New format (extra fields)

LumaCore.Api/Features/MyFeature/
├── MyFeatureService.cs            # Shared implementation
└── EndpointMapping.cs             # Single file, uses MapToApiVersion()
```

This keeps Swagger clean (separate endpoint groups per version via the dropdown) and makes the API self-documenting.

This is why contracts matter: DTOs decouple the API surface from internal data structures. You can evolve your database schema without breaking existing API versions.

### Feature Dependencies

Some features depend on others. For example, any feature that requires authentication depends on the *Auth* feature being registered first — it needs the authentication services and middleware in place.

```csharp
// MyFeature needs Auth to be registered first
builder.AddAuthFeature();      // Must come first
builder.AddMyFeature();        // Depends on Auth
```

If a dependency is missing, you'll typically get a runtime error when DI can't resolve a required service. Document dependencies clearly in the feature's README so developers know what to register.

---

© 2025 LumaCoreTech • MIT License