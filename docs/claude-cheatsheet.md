# Claude LumaCore Cheatsheet

Condensed rules for working on LumaCore. When in doubt: consult the original documents.

---

## ⚠️ CRITICAL RULES - CHECK FIRST!

### 🚫 NEVER ConfigureAwait(false) in Razor Components

```csharp
// ❌ WRONG - breaks thread safety
await Something.DoAsync().ConfigureAwait(false);

// ✅ CORRECT - always in .razor files
await Something.DoAsync().ConfigureAwait(true);
```

**Rule:** In `.razor` files: `ConfigureAwait(true)` — **always, everywhere, no exceptions.**

**Why:**
1. **Thread-Safety:** `ConfigureAwait(false)` runs continuation on ThreadPool. Another event could fire on the Blazor Dispatcher, causing race conditions on component state.
2. **JSInterop:** Requires Blazor synchronization context.
3. **UI Updates:** `StateHasChanged()` needs correct context.

**Common mistake:** "It's the last await, so `false` is okay" — **Wrong!** While awaiting, other events can still access component fields.

**Decision Tree:**
```
Is it in a .razor file?                → ConfigureAwait(true)
Does it use IJSRuntime?                → ConfigureAwait(true)
Is it backend service (HTTP/DB)?       → ConfigureAwait(false)
```

**Files that NEVER get ConfigureAwait(false):**
- ✅ `*.razor` - All Razor components
- ✅ `LocalizationService.cs` - Uses IJSRuntime
- ✅ Any service with `IJSRuntime` dependency

**Files that ALWAYS get ConfigureAwait(false):**
- ✅ `AuthService.cs` - HTTP/backend
- ✅ `*Repository.cs` - Database
- ✅ HTTP client wrappers

**Golden Rule:**
> ALWAYS be explicit — never omit ConfigureAwait().
> - In `.razor` files: Always `ConfigureAwait(true)`
> - In backend services: Always `ConfigureAwait(false)`
> 
> Omitting it defaults to `true`, which may not be what you want in backend code.

### 🔒 Blazor Server Thread-Safety

Blazor Server has one `SynchronizationContext` per user circuit — like a "virtual UI thread" (similar to WPF/WinForms Dispatcher).

| Service Lifetime | Instances | Thread-Safety |
|------------------|-----------|---------------|
| **Scoped** | 1 per circuit | ✅ Automatically serialized |
| **Singleton** | 1 for all users | ⚠️ Must lock yourself! |
| **Transient** | New per inject | ✅ No sharing |

**Dangerous — needs Lock/ConcurrentDictionary:**
```csharp
services.AddSingleton<GlobalCacheService>();  // ⚠️ All users share this!
```

**Safe without lock:**
```csharp
services.AddScoped<UserPreferencesService>();  // ✅ Isolated per circuit
```

**Caution with external callbacks** (Timer, SignalR, Events):
```csharp
// ❌ Does not run on the Blazor Dispatcher
mTimer = new Timer(_ => mCounter++);

// ✅ Use InvokeAsync
mTimer = new Timer(_ => InvokeAsync(() => mCounter++));
```

**Blazor WASM:** Single-threaded, so not an issue.

### 🚫 No Side-Effects in Razor Markup

```razor
@* ❌ WRONG - Side-effect during render *@
<NotAuthorized>
    @{
        NavigationManager.NavigateTo("login");
    }
</NotAuthorized>

@* ✅ CORRECT - Dedicated component with lifecycle method *@
<NotAuthorized>
    <RedirectToLogin />
</NotAuthorized>
```

**Rule:** Never put `NavigateTo`, API calls, or state changes in `@{ }` blocks within markup.

**Why:**
1. **Multiple execution:** Blazor can re-render components multiple times — your side-effect runs each time.
2. **Race conditions:** Navigation during render can conflict with the render process itself.
3. **Debugging nightmare:** Side-effects in render are unpredictable — you don't know when/how often they run.

**Solution:** Use lifecycle methods (`OnInitialized`, `OnAfterRender`) or create a dedicated component.

### 🚫 No JS Interop in OnInitializedAsync

```csharp
// ❌ WRONG - JS may not be ready yet
protected override async Task OnInitializedAsync()
{
    await Task.Delay(50); // Fragile hack!
    var value = await JsRuntime.InvokeAsync<string>("getValue");
}

// ✅ CORRECT - JS is guaranteed to be ready after first render
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender) return;
    
    var value = await JsRuntime.InvokeAsync<string>("getValue");
    StateHasChanged(); // Trigger re-render with new data
}
```

**Rule:** JS interop calls belong in `OnAfterRenderAsync(firstRender: true)`, not in `OnInitializedAsync`.

**Why:** JavaScript and the DOM are only guaranteed to be ready after the first render. `OnInitializedAsync` runs before that.

---

## 🏗️ Architecture Principles

### Discovery Systems — NO Hardcoded Lists!

All discovery systems use **manifest files**. Never hardcode IDs in JavaScript or C#.

| System | Manifest Location | To add new entry |
|--------|-------------------|------------------|
| **Themes** | `/wwwroot/themes/manifest.json` | Add folder + manifest entry |
| **Locales** | `/wwwroot/locales/manifest.json` | Add folder + manifest entry |

**Example — Adding a new theme:**
```
1. Create folder: /wwwroot/themes/my-theme/
2. Add theme.json and theme.css in folder
3. Add entry to manifest.json:
   { "id": "my-theme", "order": 6 }
```

### Theme Inheritance

```
themes/
├── lumacore-base/        ← Shared foundation (NOT selectable)
│   ├── theme.css         ← Common CSS variables & rules
│   └── icons/            ← Default icons (SVG)
├── lumacore-dark/        ← @import "../lumacore-base/theme.css"
├── lumacore-light/       ← @import "../lumacore-base/theme.css"
└── missi-pink/           ← Can override icons in icons/ subfolder
```

**Rules:**
- `lumacore-base` is NOT in manifest.json (not user-selectable)
- Themes import from `lumacore-base`, not from each other
- Icons fall back to `lumacore-base`, not to `lumacore-dark`
- Theme-specific icons go in `themes/{id}/icons/`

**Why this matters:**
- ❌ Themes depending on `lumacore-dark` = implicit coupling
- ✅ Themes depending on `lumacore-base` = explicit shared foundation
- ✅ Theme authors can skip base entirely if they want full control

### Consistency Check

When refactoring discovery/loading systems, verify:
- [ ] Is there a manifest file?
- [ ] Does the code load from manifest (not hardcoded)?
- [ ] Is the pattern consistent with other discovery systems?

### Don't Describe What Isn't Built

**CRITICAL:** Never tell the user "X works automatically" without verifying the code actually does that.

- ❌ "Themes are discovered automatically when you add a folder" (if code has hardcoded list)
- ✅ "Themes are discovered via manifest.json" (verified in code)
- ✅ "Currently themes are hardcoded — should we add manifest-based discovery?"

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

- **`ConfigureAwait(true)`** in all `.razor` files — no exceptions!
- **`ConfigureAwait(false)`** in backend services (HTTP, DB, no UI context)
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

**No `<returns>` for async Task:**
```csharp
// ❌ Boilerplate — says nothing useful
/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
async Task DoSomethingAsync()

// ✅ No returns tag for async Task (it's like async void)
/// <summary>Does something.</summary>
async Task DoSomethingAsync()

// ✅ Only document returns when there's an actual value
/// <returns>The user's name.</returns>
async Task<string> GetUserNameAsync()
```

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
- [ ] `ConfigureAwait(true)` in .razor, `ConfigureAwait(false)` in backend services
- [ ] XML docs on public members
- [ ] Options via `AddFeatureOptions<T>()` registered
- [ ] Endpoints have `MapToApiVersion()` and auth declaration
- [ ] Markdown: headings never wrap
- [ ] Commit message follows Conventional Commits
- [ ] **Discovery systems use manifests, not hardcoded lists**
- [ ] **Don't claim features work a certain way without verifying code**
- [ ] **No side-effects in Razor markup** — use lifecycle methods or dedicated components
- [ ] **JS interop in OnAfterRenderAsync** — not in OnInitializedAsync

---

© 2025 LumaCoreTech • For Claude's internal use