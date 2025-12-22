# Design Principles

**Audience:** Architects and Developers seeking to understand LumaCore's design

Understanding why LumaCore is built the way it is.

---

## The Philosophy Behind LumaCore

LumaCore isn't just another API framework - it's an opinionated approach to building AI-powered systems that treats personas as first-class citizens rather than disposable prompt templates.

This philosophy influences every architectural decision, from how we organize code to how we handle configuration. Let's explore the principles that shape the system.

---

## 1. Feature-First Design

LumaCore organizes code by feature, not by technical layer. Everything related to a capability (endpoints, services, configuration, DTOs) lives in one folder.

**Why?** Reduces cognitive load, improves discoverability, makes maintenance easier, and helps with onboarding.

👉 **[Read more: Feature Pattern](feature-pattern.md)** - Complete guide with examples and best practices

---

## 2. Separation of Concerns

### Three Distinct Layers

LumaCore is split into three layers, each with a clear responsibility:

```
LumaCore.Api       → Handles communication with the outside world
LumaCore.Core      → Handles intelligence and persona logic
LumaCore.Ui.Web    → Provides the user interface
```

The key insight: **communication and intelligence are fundamentally different concerns**. Mixing them makes both harder to understand and test.

### Why Not Just One Big Project?

You might wonder: "Why split this up? Isn't that just more complexity?"

Fair question. Here's why we do it:

**Testability:** Core logic can be tested without spinning up an HTTP server. You can write fast, focused unit tests that don't need to mock HttpContext or worry about middleware.

**Reusability:** Tomorrow, you might want to build a CLI tool or desktop app that uses the same persona logic. With a separate Core layer, you can reuse that intelligence without dragging in HTTP dependencies.

**Clarity:** When you see code in `LumaCore.Api`, you know it's about HTTP, routing, or authentication. When you see code in `LumaCore.Core`, you know it's about personas, memory, or inference. This mental model makes the codebase easier to navigate.

**Modularity:** The clear boundary between API and Core means you can work on persona intelligence without touching HTTP code, and vice versa. This separation reduces coupling and makes the codebase more maintainable.

### Current State

Right now, **LumaCore.Core is empty** - it's a placeholder for future business logic. But the separation is already valuable:

- The API layer has foundational infrastructure in place
- When we add business logic (personas, memory, intelligence), it will have a clean home
- We won't need to refactor the entire system to accommodate it

**Important note:** This is a **logical separation** for code organization and testability. Both layers are deployed together as a single application.

The **Blazor UI** (`LumaCore.Ui.Web`), however, *is* separately deployable - it's a WebAssembly app that runs entirely in the browser and can be hosted on any static file server (CDN, S3, etc.), independently of where the API runs. The API can also serve the UI as static files for simpler deployment scenarios.

---

## 3. Fail-Fast Configuration

### The 3 AM Production Problem

It's 3 AM. Your production deployment is failing. Users can't log in. After an hour of debugging, you discover the JWT signing key is only 24 characters instead of the required 32.

This key was misconfigured when you deployed two weeks ago, but the application started fine. It only failed when someone actually tried to use authentication.

This is the nightmare scenario that fail-fast configuration prevents.

### Validate Everything at Startup

In LumaCore, if your configuration is wrong, **the application won't start**.

**Example from JWT authentication:**

```csharp
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()      // Check annotations
    .ValidateOnStart();             // ← Fail immediately if invalid
```

The `ValidateOnStart()` call means:
- If a required value is missing → **crash on startup**
- If a value is too short/long → **crash on startup**
- If a value is out of valid range → **crash on startup**

This might seem harsh, but it's far better to fail at deploy time (when you're watching) than at 3 AM (when you're asleep).

### Data Annotations Make It Clear

Configuration validation uses standard .NET data annotations.

**Example:**

```csharp
public sealed class JwtOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "SigningKey must be at least 32 characters")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "AccessTokenLifetimeMinutes must be 1-1440")]
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
```

This isn't just validation - it's **documentation**. Anyone reading this code knows exactly what's required and what the constraints are.

### The Trade-Off

Fail-fast configuration is strict. You can't run with partial configuration "just to see what happens." But that's the point - we want to prevent those "let's just try it" deployments that cause production incidents.

Better to fail visibly during deployment than mysteriously during operation.

---

## 4. Explicit Over Implicit

### No Magic, No Surprises

LumaCore avoids "magic" - hidden behavior that happens behind the scenes without you asking for it.

Every dependency is explicit.

**Example:**

```csharp
public class JwtTokenFactory : IJwtTokenFactory
{
    private readonly IOptions<JwtOptions> mOptions;
    private readonly ILogger<JwtTokenFactory> mLogger;

    public JwtTokenFactory(
        IOptions<JwtOptions> options,
        ILogger<JwtTokenFactory> logger)
    {
        mOptions = options;
        mLogger = logger;
    }
}
```

You can see **exactly** what this class depends on by looking at its constructor. No hidden static dependencies. No service locator pattern. No ambient context.

### Why This Matters for Testing

When dependencies are explicit, testing is straightforward. You can see exactly what a class needs and provide test versions of those dependencies. No hidden static dependencies to mock, no service locators to configure, no global state to clean up between tests.

### Configuration is Explicit Too

Configuration isn't scattered through attributes or convention-based scanning. It's explicit in `Program.Services.cs`.

**Example:**

```csharp
builder.AddAuthFeature();
builder.AddSystemFeature();
builder.AddHealthFeature();
```

Want to know what features are enabled? Look at `Program.Services.cs` - every feature is explicitly registered there. No reflection-based scanning, no convention-based discovery, no surprises.

This explicitness has a cost - you have to write the registration code. But you gain **traceability** - you can always see exactly what's wired up and why.

---

## 5. Production-Ready Infrastructure

### Infrastructure First, Features Follow

LumaCore's infrastructure is built with production in mind from the start, even while features are still in development:

- **Structured logging** - Serilog with proper configuration, not console.WriteLine()
- **Health checks** - Built in from day one for monitoring
- **Configuration validation** - Catches errors at startup before deployment
- **Security foundation** - JWT, HTTPS, authentication patterns established
- **Monitoring hooks** - Ready for observability tools
- **Documentation** - Architecture and patterns documented as they're built

**Important:** While the infrastructure is production-grade, individual features (like authentication) may start in bootstrap mode with simplified implementations. The point is the **foundation** is solid - features mature on top of it.

### Why This Matters

Many projects start as "quick prototypes" and end up in production before they're ready. Then you spend months retrofitting logging, monitoring, proper error handling, and all the things you should have had from the start.

LumaCore avoids this trap. The infrastructure is here. The patterns are established. When you add a new feature, you're building on a solid foundation.

### The Trade-Off

This approach requires more upfront investment. The first features take longer because you're establishing patterns alongside functionality. But the investment pays off - subsequent features become progressively faster as patterns are reused and developers gain familiarity with the established structure.

---

## Putting It All Together

These principles aren't independent - they reinforce each other:

**Feature-first design** keeps related code together, reducing cognitive load and making the system easier to navigate.

**Separation of concerns** between API and Core enables independent testing and future reusability.

**Explicit dependencies** combined with **fail-fast configuration** means you can see exactly what a system needs and know immediately if something is wrong.

**Production-ready infrastructure** from the start means you're building on solid foundations, not retrofitting quality later.

Every principle serves the same ultimate goal: **Building a system that's understandable, maintainable, and reliable.**

---

## What This Means for You

### As a Developer

These principles make your life easier:
- Less time hunting for code
- Testable components
- Clearer understanding of how things work
- Confident that your changes won't break unexpected things

### As an Architect

These principles provide:
- Clear boundaries between layers
- Extensible patterns
- Clean separation of concerns
- Scalable architecture

### As an Operator/DevOps

These principles mean:
- Production-ready infrastructure
- Fail-fast configuration catches issues early
- Proper logging and monitoring
- Clear deployment requirements

### As a User

These principles mean:
- Fewer bugs
- Better performance
- More reliable application

---

## Next Steps

**Understand the patterns:**
- [Feature Pattern](feature-pattern.md) - See these principles in action
- [Project Structure](project-structure.md) - How the codebase is organized

**See it in practice:**
- [Auth Feature](../features/auth.md) - Real implementation following these principles
- [System Feature](../features/system.md) - Another example

---

© 2025 LumaCoreTech • MIT License
