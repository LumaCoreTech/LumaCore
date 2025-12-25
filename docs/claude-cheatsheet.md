# Claude LumaCore Cheatsheet

Condensed rules for working on LumaCore. When in doubt: consult the original documents.

---

## Code Formatting

- **Line length:** Max 120 characters — but *use* the available width, don't break unnecessarily at 80
- **Indentation:** Tabs (not spaces)
- **Line endings:** LF (except Windows scripts)
- **Encoding:** UTF-8 without BOM

### File Header (every .cs file)

```csharp
// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore
```

### Using Order (separated by blank lines)

1. System.*
2. Microsoft.*
3. Third-party (Serilog, etc.)
4. LumaCore.*

### Member Order in Classes

1. Constants
2. Static fields (`s` prefix)
3. Instance fields (`m` prefix, readonly first)
4. Constructors
5. Properties
6. Public methods
7. Protected/Internal methods
8. Private methods

---

## Naming

| Element | Convention | Example |
|---------|------------|---------|
| Class/Record/Interface | PascalCase (`I` prefix) | `JwtTokenFactory`, `IJwtTokenFactory` |
| Method/Property | PascalCase | `CreateToken`, `AccessToken` |
| Instance field | `m` + camelCase | `mLogger`, `mOptions` |
| Static field | `s` + camelCase | `sDefaultValue`, `sCache` |
| Const | PascalCase | `SectionName`, `DefaultPort` |
| Parameter/Local | camelCase | `userId`, `result` |
| Async methods | `Async` suffix | `GetUserAsync()` |
| Collections | Plural | `Users`, `Claims` |

---

## Types

- `class` for services, behavior → `sealed` by default
- `record` for DTOs, configs, value equality
- `readonly struct` when value type is needed
- `static class` only for extension methods

---

## Contracts vs. Core Documentation

**Contracts (API consumer view):** What does the value mean? How do I interpret it?

**Core (developer view):** Same as Contracts + implementation details (configuration, APIs, OS specifics)

| Contracts ✓ | Core ✓ (additionally) |
|-------------|----------------------|
| `.NET`, `managed heap` | `DOTNET_GCHeapHardLimit` |
| `Server GC`, `IOCP`, `cgroup` | `runtimeconfig.json`, `epoll/kqueue` |
| Concepts (throttling) | Concrete values (`500ms`) |
| WHAT the value means | HOW it's retrieved/configured |

---

## Async/Await

- **`ConfigureAwait(false)`** in all library/service methods
- **Never** `.Result` or `.Wait()` – async all the way
- `Task` by default, `ValueTask` only when profiled

---

## Null Handling

```csharp
public string Name { get; set; } = string.Empty;     // Never null
public string? OptionalName { get; set; }            // Explicitly nullable
public List<User> Users { get; set; } = [];          // Collections never null

ArgumentNullException.ThrowIfNull(user);             // Modern validation
```

---

## XML Documentation

Document all public members. **Use the 120 characters** – don't break unnecessarily at 85.
Break at logical points (end of sentence, new thought).

```csharp
/// <summary>Brief description.</summary>
/// <remarks>
///     <para><b>Section header</b></para>
///     <para>Detailed explanation that uses the full 120 characters available per line for better readability.</para>
/// </remarks>
/// <param name="name">Short, factual description.</param>
/// <returns>Return value.</returns>
/// <exception cref="Exception">When thrown.</exception>
```

**Important:** `<remarks>` only at type/member level, **never** inside `<param>`!

| Tag | Usage |
|-----|-------|
| `<see cref="Type"/>` | Link to type/member |
| `<see langword="null"/>` | Keywords (`null`, `true`, `false`) |
| `<c>code</c>` | Inline code (strings, numbers) |
| `<paramref name="x"/>` | Reference parameter |

---

## Feature Structure

```
Features/
└── FeatureName/
    ├── ServiceRegistration.cs    // AddFeature(), AddFeatureCore()
    ├── EndpointMapping.cs        // MapFeature() - only if API endpoints
    ├── FeatureOptions.cs         // Options class with SectionName
    └── [additional services]
```

### ServiceRegistration Pattern

```csharp
public static WebApplicationBuilder AddMyFeature(this WebApplicationBuilder builder)
{
    AddMyFeatureCore(builder.Services, builder.Configuration);
    return builder;
}

internal static void AddMyFeatureCore(IServiceCollection services, IConfiguration config)
{
    services.AddFeatureOptions<MyOptions>(config, MyOptions.SectionName);
    services.AddSingleton<MyService>();
}
```

### Options Registration

**Always** use `AddFeatureOptions<T>()` – never raw `AddOptions<T>().Bind()`.

```csharp
services.AddFeatureOptions<MyOptions>(configuration, MyOptions.SectionName);
```

### Endpoint Pattern

```csharp
public static RouteGroupBuilder MapMyFeature(this RouteGroupBuilder group)
{
    group.MapGet("/path", (IService svc) => { ... })
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()  // or .AllowAnonymous()
        .Produces<ResponseType>(StatusCodes.Status200OK)
        .WithSummary("Short summary")
        .WithDescription("Longer description.")
        .WithName("EndpointName");
    
    return group;
}
```

**Note:** 401/403 are automatically added by `SecurityResponsesTransformer`.

---

## Commit Messages (Conventional Commits)

```
<type>(<scope>): <Subject>

<body>
```

### Types

`feat` | `fix` | `docs` | `refactor` | `test` | `perf` | `style` | `chore` | `revert`

### Scopes (selection)

`api` | `auth` | `core` | `health` | `openapi` | `cors` | `errors` | `roadmap` | `system` | `docs` | `build` | `ci`

### Rules

- Header max 72 characters
- Type/Scope lowercase, Subject capitalized
- Imperative mood: "Add feature" not "Added feature"
- No period at the end
- Body for non-trivial changes

### Breaking Changes

```
feat(auth)!: Change token response structure

BREAKING CHANGE: Response field "token" renamed to "accessToken".
```

---

## Feature Documentation (system.md etc.)

### Markdown Rules

- **Headings:** Never wrap (breaks Markdown rendering)
- **Body text:** One paragraph = one line, renderer handles wrapping
- **Intentional line breaks:** Two trailing spaces (`  `) for stylistic breaks
- **Blank line** between paragraphs

### Structure

1. Title + Intro (prose, no bullet lists)
2. Endpoints (if applicable)
3. Configuration
4. Registered Services
5. Pipeline Order
6. Typical Usage
7. Related Features
8. Copyright Footer

### Style

- Feature name in prose: *italics* (`*Auth*`)
- No JSON examples in Endpoints (→ OpenAPI)
- No .NET internals (`ClaimsPrincipal`, `AddOptions<T>()`)
- Describe behavior as prose, not as lists
- `**Requires:** admin role` for protected endpoints

---

## Principles (Summary)

- **SOLID** – SRP, OCP, LSP, ISP, DIP
- **DRY** – Don't Repeat Yourself (but don't over-abstract)
- **KISS** – Keep It Simple, Stupid
- **YAGNI** – You Ain't Gonna Need It

**Golden rule:** If you think "maybe later" → don't build it.

---

## Quick Checklist Before Commit

- [ ] Line length: Max 120 characters, but also use the width
- [ ] Copyright header present
- [ ] Usings sorted (System → Microsoft → Third-party → LumaCore)
- [ ] `m` prefix for instance fields, `s` for static
- [ ] `sealed` on classes (unless inheritance is planned)
- [ ] `ConfigureAwait(false)` on async
- [ ] XML docs on public members
- [ ] Options via `AddFeatureOptions<T>()` registered
- [ ] Endpoints have `MapToApiVersion()` and auth declaration
- [ ] Markdown: headings never wrap
- [ ] Commit message follows Conventional Commits

---

© 2025 LumaCoreTech • For Claude's internal use