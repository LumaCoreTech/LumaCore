# Claude LumaCore Cheatsheet

Kondensierte Regeln für die Arbeit an LumaCore. Bei Unsicherheit: Originaldokumente konsultieren.

---

## Code-Formatierung

- **Max. Zeilenlänge:** 120 Zeichen
- **Indentation:** Tabs (nicht Spaces)
- **Line Endings:** LF (außer Windows-Scripts)
- **Encoding:** UTF-8 ohne BOM

### File Header (jede .cs Datei)

```csharp
// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore
```

### Using-Reihenfolge (mit Leerzeilen getrennt)

1. System.*
2. Microsoft.*
3. Third-party (Serilog, etc.)
4. LumaCore.*

### Member-Reihenfolge in Klassen

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

| Element | Konvention | Beispiel |
|---------|------------|----------|
| Klasse/Record/Interface | PascalCase (`I` prefix) | `JwtTokenFactory`, `IJwtTokenFactory` |
| Method/Property | PascalCase | `CreateToken`, `AccessToken` |
| Instance field | `m` + camelCase | `mLogger`, `mOptions` |
| Static field | `s` + camelCase | `sDefaultValue`, `sCache` |
| Const | PascalCase | `SectionName`, `DefaultPort` |
| Parameter/Local | camelCase | `userId`, `result` |
| Async methods | `Async` suffix | `GetUserAsync()` |
| Collections | Plural | `Users`, `Claims` |

---

## Types

- `class` für Services, Behavior → `sealed` by default
- `record` für DTOs, Configs, Value-Equality
- `readonly struct` wenn Value-Type nötig
- `static class` nur für Extension Methods

---

## Async/Await

- **`ConfigureAwait(false)`** in allen Library/Service-Methoden
- **Niemals** `.Result` oder `.Wait()` – async all the way
- `Task` by default, `ValueTask` nur wenn profiled

---

## Null Handling

```csharp
public string Name { get; set; } = string.Empty;     // Nie null
public string? OptionalName { get; set; }            // Explizit nullable
public List<User> Users { get; set; } = [];          // Collections nie null

ArgumentNullException.ThrowIfNull(user);             // Moderne Prüfung
```

---

## XML Documentation

Alle public Members dokumentieren.

```csharp
/// <summary>Brief description.</summary>
/// <param name="name">Parameter.</param>
/// <returns>Return value.</returns>
/// <exception cref="Exception">When thrown.</exception>
```

| Tag | Verwendung |
|-----|------------|
| `<see cref="Type"/>` | Link zu Type/Member |
| `<see langword="null"/>` | Keywords (`null`, `true`, `false`) |
| `<c>code</c>` | Inline code (Strings, Zahlen) |
| `<paramref name="x"/>` | Parameter referenzieren |

---

## Feature-Struktur

```
Features/
└── FeatureName/
    ├── ServiceRegistration.cs    // AddFeature(), AddFeatureCore()
    ├── EndpointMapping.cs        // MapFeature() - nur wenn API-Endpoints
    ├── FeatureOptions.cs         // Options-Klasse mit SectionName
    └── [weitere Services]
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

**Immer** `AddFeatureOptions<T>()` verwenden – nie raw `AddOptions<T>().Bind()`.

```csharp
services.AddFeatureOptions<MyOptions>(configuration, MyOptions.SectionName);
```

### Endpoint Pattern

```csharp
public static RouteGroupBuilder MapMyFeature(this RouteGroupBuilder group)
{
    group.MapGet("/path", (IService svc) => { ... })
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()  // oder .AllowAnonymous()
        .Produces<ResponseType>(StatusCodes.Status200OK)
        .WithSummary("Short summary")
        .WithDescription("Longer description.")
        .WithName("EndpointName");
    
    return group;
}
```

**Beachte:** 401/403 werden automatisch vom `SecurityResponsesTransformer` hinzugefügt.

---

## Commit Messages (Conventional Commits)

```
<type>(<scope>): <Subject>

<body>
```

### Types

`feat` | `fix` | `docs` | `refactor` | `test` | `perf` | `style` | `chore` | `revert`

### Scopes (Auswahl)

`api` | `auth` | `core` | `health` | `openapi` | `cors` | `errors` | `roadmap` | `system` | `docs` | `build` | `ci`

### Regeln

- Header max. 72 Zeichen
- Type/Scope lowercase, Subject Großbuchstabe am Anfang
- Imperative mood: "Add feature" nicht "Added feature"
- Kein Punkt am Ende
- Body bei nicht-trivialen Changes

### Breaking Changes

```
feat(auth)!: Change token response structure

BREAKING CHANGE: Response field "token" renamed to "accessToken".
```

---

## Feature Documentation (system.md etc.)

### Struktur

1. Title + Intro (Prosa, kein Bullet-List)
2. Endpoints (falls vorhanden)
3. Configuration
4. Registered Services
5. Pipeline Order
6. Typical Usage
7. Related Features
8. Copyright Footer

### Stil

- Feature-Name in Prosa: *kursiv* (`*Auth*`)
- Keine JSON-Beispiele in Endpoints (→ OpenAPI)
- Keine .NET-Interna (`ClaimsPrincipal`, `AddOptions<T>()`)
- Behavior als Prosa beschreiben, nicht als Liste
- `**Requires:** admin role` bei geschützten Endpoints

---

## Prinzipien (Kurzfassung)

- **SOLID** – SRP, OCP, LSP, ISP, DIP
- **DRY** – Don't Repeat Yourself (aber nicht über-abstrahieren)
- **KISS** – Keep It Simple, Stupid
- **YAGNI** – You Ain't Gonna Need It

**Goldene Regel:** Wenn du "vielleicht später" denkst → baue es nicht.

---

## Quick Checklist vor Commit

- [ ] Max. 120 Zeichen pro Zeile
- [ ] Copyright Header vorhanden
- [ ] Usings sortiert (System → Microsoft → Third-party → LumaCore)
- [ ] `m` prefix für Instance Fields, `s` für Static
- [ ] `sealed` auf Klassen (außer wenn Vererbung geplant)
- [ ] `ConfigureAwait(false)` bei async
- [ ] XML Docs auf public Members
- [ ] Options via `AddFeatureOptions<T>()` registriert
- [ ] Endpoints haben `MapToApiVersion()` und Auth-Deklaration
- [ ] Commit Message folgt Conventional Commits

---

© 2025 LumaCoreTech • Für Claude's internen Gebrauch
