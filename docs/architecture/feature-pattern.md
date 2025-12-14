# Feature Pattern

**Audience:** Architects and Developers seeking to understand LumaCore's design

The definitive guide to LumaCore's feature-based architecture.

---

## What is a Feature?

In LumaCore, a **Feature** is a self-contained module that owns everything related to a specific capability:

> **"Every major capability lives in a Feature. A Feature owns its endpoints, services, configuration, and contracts."**

A Feature is **not** just a folder - it's an architectural pattern that shapes how the entire system is built.

---

## Why Features?

### The Problem: Traditional ASP.NET Architecture

In a typical ASP.NET application, code is organized by **technical layer**:

```
MyApp/
├── Controllers/        # All controllers
├── Services/           # All services  
├── Models/             # All models
├── Configuration/      # All config
└── Startup.cs          # All registration
```

This **technical organization** means that code for a single capability is scattered. Want to understand how authentication works? You'll visit the `Controllers/` folder for endpoints, the `Services/` folder for business logic, the `Models/` folder for DTOs, the `Configuration/` folder for settings, and `Startup.cs` for registration.

**The cognitive cost is high:** To understand one capability, you hunt through multiple folders, mentally stitching together pieces from different locations.

### The Solution: Feature-Based Architecture

LumaCore organizes code by **capability** instead:

```
Features/
├── Auth/               # Everything auth-related
├── Admin/              # Everything admin-related
└── Health/             # Everything health-related
```

Each feature folder contains its own registration, endpoints, contracts, services, and configuration - everything needed for that capability lives together.

**The cognitive cost is low:** To understand one capability, open one folder. No hunting, no mental stitching. Everything you need is right there.

### The Benefits

1. **Cognitive Load:** Everything related lives together - less context switching
2. **Discoverability:** New developers know exactly where to look
3. **Modularity:** Features can be added/removed independently
4. **Testing:** Clear boundaries make testing easier
5. **Code Review:** Changes to a feature stay within its folder

---

## Feature Anatomy

Every feature follows the same structure:

```
Features/<Feature>/
│
├── ServiceRegistration.cs          # Required (for features with services/configuration)
│   └── Add<Feature>Feature()       # Registration method
│
├── EndpointMapping.cs              # Required (for HTTP features)
│   └── Map<Feature>Feature()       # Endpoint mapping method
│
├── MiddlewareIntegration.cs        # Required (for features with middleware)
│   └── Use<Feature>Feature()       # Middleware integration method
│
├── Contracts/                      # Required (for DTOs)
│   ├── <Endpoint>Request.cs        # One per endpoint that accepts input
│   ├── <Endpoint>Response.cs       # One per endpoint that returns data
│   └── <Shared>Item.cs             # Shared DTOs used across endpoints
│
├── <Feature>Options.cs             # Optional (if configuration needed)
│
├── I<Service>.cs                   # Optional (service interfaces)
├── <Service>.cs                    # Optional (service implementations)
│
└── README.md                       # Optional (feature documentation)
```

**Naming patterns:**
- `<Feature>` = Feature name (Auth, Admin, Health, Persona, etc.)
- `<Endpoint>` = Specific endpoint/action (Login, WhoAmI, CreatePersona, etc.)
- `<Service>` = Service name (TokenFactory, PersonaEngine, etc.)
- `<Shared>` = Shared type name (ClaimItem, PersonaMetadata, etc.)

Every feature follows this pattern - no variations, no surprises, no guessing where things are.

### The Core Integration Files

The most important files are the **core integration files** - they connect the feature to LumaCore:

- **`ServiceRegistration.cs`** registers services and configuration with the DI container. This is where features set up their dependencies, bind options from configuration, and prepare everything needed for the feature to function.

- **`EndpointMapping.cs`** maps HTTP routes to handlers. This file defines which URLs the feature responds to and connects them to the code that handles the requests.

> 💡 **Naming Convention:** Endpoints typically follow the pattern `/api/<feature>/*`. For example, `/api/auth/login` comes from `Features/Auth/EndpointMapping.cs`. This makes it easy to locate the code for any endpoint. The OpenAPI documentation at `/swagger` also groups endpoints by feature.

- **`MiddlewareIntegration.cs`** integrates middleware into the HTTP pipeline. Features that need to inspect or modify requests/responses before they reach handlers use this file to plug into the pipeline.

### Why Separate Files?

In traditional ASP.NET, registration and routing often happen in the same place - routes are defined via controller attributes, but DI registration lives in Startup.cs, and configuration in appsettings.json. This scatters concerns across multiple files.

**LumaCore's solution:** Keep them separate, but colocated in the same feature folder. Each concern gets its own file.

### The Flow

Here's how the integration files work together:

```
Application Startup
    ↓
Program.Services.cs calls:
    builder.Add<Feature>Feature()
        ↓
    ServiceRegistration.cs executes:
        - Registers feature services
        - Binds feature options
        - Configures dependencies
    ✅ Feature is ready
    
var app = builder.Build()  ← DI container is now sealed
    ↓
Application Pipeline Configuration
    ↓
Program.Pipeline.cs calls:
    app.Map<Feature>Feature()
        ↓
    EndpointMapping.cs executes:
        - Creates route group
        - Maps endpoints to handlers
        - Attaches authorization (if needed)
    ✅ Endpoints are live
    
    app.Use<Feature>Feature()  (if feature has middleware)
        ↓
    MiddlewareIntegration.cs executes:
        - Registers middleware in pipeline
        - Configures middleware order
    ✅ Middleware is integrated
    
HTTP Request arrives
    ↓
Routing finds matching endpoint
    ↓
Handler executes
    ↓
Dependencies injected (registered earlier!)
    ↓
Response returned
```

> 💡 **For implementation details** (signatures, examples, key points), see [Core Integration Files: Implementation Guide](../development/building-features.md#core-integration-files-implementation-guide) in the Developer Guide.

---

## Feature Lifecycle

Understanding how a feature integrates into the application is crucial. Features go through three distinct phases:

### The Three Phases

**1. Registration Phase (Startup)**

During application startup, features prepare themselves by registering their services, binding configuration, and validating options. This happens through the `builder.Add<Feature>Feature()` call. If configuration is invalid, the application crashes immediately at startup rather than failing later when the configuration is actually used - this is the fail-fast principle in action. The phase ends when `builder.Build()` is called, sealing the DI container so no more services can be registered.

**2. Mapping Phase (Pipeline)**

After the DI container is sealed, the HTTP pipeline is configured. Features map their endpoints through `app.Map<Feature>Feature()` and integrate any middleware through `app.Use<Feature>Feature()`. At this point, the feature defines which URLs it responds to and how requests flow through its middleware.

**3. Runtime Phase (Request Handling)**

Once the application is running, HTTP requests arrive and are routed to the appropriate endpoints. The handler executes with all dependencies automatically injected from the DI container, processes the request, and returns a response to the client.

**Think of it like a restaurant:**
- **Registration** = Hiring staff, stocking ingredients, setting up equipment
- **Mapping** = Printing the menu, assigning tables
- **Runtime** = Taking orders, cooking food, serving customers

### Why Three Phases?

ASP.NET Core's architecture enforces this separation:

1. **Before `builder.Build()`** → Services can be registered (Registration Phase)
2. **`builder.Build()` is called** → DI container is sealed, returns `WebApplication`
3. **After `builder.Build()`** → Endpoints can be mapped, middleware configured (Mapping Phase)
4. **`app.Run()` starts** → Application handles requests (Runtime Phase)

Features follow this architecture naturally - each phase has a dedicated integration file.

> 💡 **For detailed walkthrough** with code examples, see [Feature Lifecycle](../development/building-features.md#feature-lifecycle) in the Developer Guide.

---

## Feature Communication

### Features Should Be Independent

One of the core principles of the Feature pattern is **independence** - features should not directly depend on each other. This means a feature should never import services, types, or logic from another feature.

**Why this matters:**

When features are independent, they can be added, removed, or modified without affecting other features. Testing becomes simpler because there are no circular dependencies to mock or work around. The boundaries between features remain clear, making the system easier to understand and maintain.

**When features need to share logic:**

If multiple features need the same functionality, that's a sign the logic belongs in **LumaCore.Core** as shared infrastructure. Both features can then depend on the shared abstraction from Core, not on each other. This preserves independence while avoiding duplication.

---

## Summary

The Feature pattern is LumaCore's architectural foundation:

✅ **Self-contained** - Everything for a capability lives in one feature folder  
✅ **Predictable** - Same structure for every feature  
✅ **Testable** - Clear boundaries, dependency injection  
✅ **Scalable** - Add features without refactoring  
✅ **Maintainable** - Easy to understand and modify  

**When you understand the Feature pattern, you understand LumaCore.**

---

## Next Steps

👆 **[Back to Architecture Overview](README.md)** - See all architecture documentation

Or continue with related topics:

**Architecture:**
- [Design Principles](principles.md) - Architectural philosophy (Fail-Fast, Explicit Over Implicit)
- [Project Structure](project-structure.md) - How features fit into the project

**Development:**
- [Building Features: Developer Guide](../development/building-features.md) - Implementation details (Contracts, Options, Patterns, Testing)
- [Auth Feature](../features/auth.md) - Complete implementation example

---

© 2025 LumaCoreTech • MIT License