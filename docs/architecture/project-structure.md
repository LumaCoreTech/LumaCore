# Project Structure

**Audience:** Architects and Developers seeking to understand LumaCore's design

This document explains how the LumaCore repository is organized, how the build system works, and how versioning is managed.

---

## Repository Layout

```
LumaCore/
├── src/                          → Source Code Organization
│   ├── LumaCore.Api/
│   ├── LumaCore.Api.Contracts/
│   ├── LumaCore.Core/
│   ├── LumaCore.Ui.Web/
│   ├── Directory.Build.props
│   └── Directory.Build.targets
│
├── artifacts/                    → Build Outputs (not in source control)
│   ├── bin/
│   └── obj/
│
├── docs/                         → Documentation Organization
│   ├── architecture/
│   ├── deployment/
│   ├── development/
│   └── features/
│
├── assets/                       → Assets
│   └── branding/
│
├── build.net/                    → Build.Net Submodule
│
├── LumaCore.sln                  → Solution File
├── global.json                   → SDK Configuration
├── LICENSE
└── README.md
```

**Quick Navigation:**
- [Source Code Organization](#source-code-organization) — `/src` projects and structure
- [Build System](#build-system) — `Directory.Build.props` and artifacts
- [Versioning](#versioning) — *MinVer* and semantic versioning via Git tags
- [SDK Configuration](#sdk-configuration) — `global.json` and .NET SDK
- [Solution File](#solution-file) — `LumaCore.sln`
- [Documentation Organization](#documentation-organization) — `/docs` structure
- [Assets](#assets) — `/assets` contents

---

## Source Code Organization

The `/src` folder contains all source code projects.

### Projects

#### LumaCore.Api
**Type:** `Microsoft.NET.Sdk.Web` (ASP.NET Core application)  
**Purpose:** Main HTTP API with authentication, routing, and features  
**Status:** Operational with foundational infrastructure

**Key responsibilities:**
- HTTP request handling (Kestrel)
- Feature-based modules (see `Features/` folder)
- Middleware pipeline (HTTPS, CORS, logging, compression)
- JWT authentication and authorization
- Blazor UI hosting

**Dependencies:**
- `LumaCore.Api.Contracts` (project reference)
- `LumaCore.Core` (project reference)
- `LumaCore.Ui.Web` (project reference)

---

#### LumaCore.Api.Contracts
**Type:** `Microsoft.NET.Sdk` (class library)  
**Purpose:** Shared API contract types (DTOs) for requests and responses  
**Status:** Operational

**Key responsibilities:**
- Request DTOs with validation attributes
- Response DTOs for API endpoints
- Shared types used across multiple endpoints

**Why separate?**
- **Shared access** — Both API and Blazor UI can reference contracts without circular dependencies
- **Clear API surface** — Contract changes are intentional and visible
- **Minimal dependencies** — Only `System.ComponentModel.Annotations` for validation

**Structure:**
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

---

#### LumaCore.Core
**Type:** `Microsoft.NET.Sdk` (class library)  
**Purpose:** Core abstractions, diagnostics, and shared functionality  
**Status:** Operational with diagnostics infrastructure

**Current responsibilities:**
- Runtime diagnostics (memory, GC, process, thread pool metrics)
- `IMetricsContributor` interface for extensible metrics
- Shared abstractions used across features

**Planned responsibilities:**
- Persona runtime and state management
- Memory system (long-term conversation storage)
- Vector store integration (semantic search)
- LLM orchestration (Ollama, custom backends)

**Why separate?**
- **Testability** — Core logic can be unit tested without HTTP
- **Reusability** — Could be used by CLI, desktop app, or other frontends
- **Clarity** — Clear separation between communication (API) and intelligence (Core)

**Structure:**
```
LumaCore.Core/
└── Diagnostics/
    ├── IMetricsContributor.cs       # Interface for metrics extensibility
    ├── *Metrics.cs                  # Diagnostic snapshots (Memory, Gc, Process, ThreadPool)
    ├── *MetricsFactory.cs           # Static factories for snapshot creation
    └── *MetricsContributor.cs       # Built-in contributors for DI integration
```

---

#### LumaCore.Ui.Web
**Type:** `Microsoft.NET.Sdk.BlazorWebAssembly` (Blazor WebAssembly)  
**Purpose:** Single-page application UI for LumaCore  
**Status:** Hosted by LumaCore.Api

Blazor WebAssembly compiles to static files (HTML, JS, WebAssembly DLLs) and runs entirely in the browser. It is currently served by LumaCore.Api on the same origin, so no CORS configuration is needed.

**Dependencies:**
- `LumaCore.Api.Contracts` (project reference) — Shared DTOs for API communication

---

### Feature-Based Directory Structure

Features are organized in self-contained folders under `LumaCore.Api/Features/`:

**Common components (not every feature has all):**
```
Features/{FeatureName}/
├── ServiceRegistration.cs              # Registers services and configuration with DI
├── EndpointMapping.cs                  # HTTP endpoints (if feature exposes API)
├── MiddlewareIntegration.cs            # Pipeline middleware (if needed)
├── {FeatureName}Options.cs             # Configuration class
└── *.cs                                # Implementation (factories, services, validators, etc.)
```

> **Note:** Request/response DTOs (contracts) live in the separate `LumaCore.Api.Contracts` project, organized by API version (`V1/`, `V2/`, etc.). This allows both the API and Blazor UI to share the same types.

Each feature contains only what it needs. No mandatory structure beyond ServiceRegistration.cs.

👉 **[Read more: Feature Pattern](feature-pattern.md)** — Complete guide to the feature architecture

---

## Build System

### Artifacts Organization

LumaCore centralizes all build outputs in a single `/artifacts` folder at the repository root, instead of scattering `bin/` and `obj/` folders throughout each project.

**Structure:**
```
artifacts/
├── bin/                          # Compiled outputs
│   ├── LumaCore.Api/
│   │   └── AnyCPU.Release/net10.0/
│   └── LumaCore.Core/
│       └── AnyCPU.Release/net10.0/
└── obj/                          # Intermediate build files
    ├── LumaCore.Api/
    └── LumaCore.Core/
```

**Why centralized artifacts?**
- **Cleaner repository** — No `bin/obj` clutter in source folders
- **Easier cleanup** — Single command: `rm -rf artifacts/`
- **CI/CD friendly** — Predictable output locations for build pipelines
- **Git-safe** — Single `.gitignore` entry covers all outputs

**How it's configured:**
This redirection is configured in `Directory.Build.props` (see below).

---

### Directory.Build.props

The `src/Directory.Build.props` file provides shared configuration for all projects.

#### Language & Code Style

All projects use consistent language settings:

```xml
<Nullable>enable</Nullable>                       <!-- Nullable reference types -->
<ImplicitUsings>enable</ImplicitUsings>           <!-- Automatic using directives -->
<LangVersion>latestMajor</LangVersion>            <!-- Latest C# version -->
<GenerateDocumentationFile>true</GenerateDocumentationFile>  <!-- XML docs -->
```

**Result:**
- Modern C# 13 features available in all projects
- Nullable reference types catch null-related bugs at compile time
- XML documentation is generated for IntelliSense

---

#### Assembly Metadata

All assemblies share common metadata:

```xml
<Company>LumaCoreTech</Company>
<Product>LumaCore</Product>
<Authors>LumaCoreTech</Authors>
<RepositoryUrl>https://github.com/LumaCoreTech/LumaCore</RepositoryUrl>
```

This appears in compiled DLLs and helps identify the source.

---

### Directory.Build.targets

The `src/Directory.Build.targets` file contains shared build targets:

**CleanArtifacts Target:**
Enables complete artifact cleanup with:
```bash
dotnet clean /p:RemoveArtifacts=true
```
This removes the entire `/artifacts` folder, not just the current project's outputs.

**SetArtifactName Target:**
Automatically names publish outputs with semantic versioning:
```
LumaCore.Api-0.1.42-ci/      # Prerelease build
LumaCore.Api-1.0.0/          # Public release
```

This ensures published artifacts are clearly versioned for deployment tracking.

---

### Build.Net Submodule

The `build.net/` folder is a **Git submodule** pointing to:  
`https://github.com/LumaCoreTech/build.net`

**Purpose:** Shared configuration and tooling reused across multiple LumaCoreTech repositories:
- ReSharper/Rider code style settings
- OpenAPI documentation scripts and generators

The submodule points to a specific commit of the build.net repository, ensuring consistent tooling across all builds. Updates to the submodule are managed through the main repository's version control.

---

## Versioning

### Versioning

Versioning is managed by *MinVer*, which derives the version automatically from Git tags. No configuration file is required.

**How it works:**
- Create a Git tag like `v1.0.0` → produces version `1.0.0`
- Commits after a tag → produces version `1.0.1-ci.{height}` (e.g., `1.0.1-ci.5`)
- No tags → produces version `0.0.0-ci.{height}`

**Version format:**
- Release builds: Tag-based (e.g., `v1.2.3` → `1.2.3`)
- Prerelease builds: `{version}-ci.{commits}` (e.g., `1.0.1-ci.5`)

*MinVer* is integrated via `Directory.Build.props` and requires no additional configuration.

---

## SDK Configuration

### global.json

Locks the .NET SDK version to ensure consistent builds across all environments:

```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

**What this means:**
- Requires .NET 10.0.0 SDK or newer
- `rollForward: latestFeature` — Can use newer feature releases (10.1, 10.2, etc.) but not major versions (11.0)
- `allowPrerelease: false` — Only stable SDK releases

**Why lock the SDK version?**
- Reproducible builds (same SDK = same results)
- Prevents breaking changes from new SDK releases
- Developers see exactly which SDK the project targets

---

## Solution File

### LumaCore.sln

The Visual Studio solution file references all projects:

```
LumaCore.sln
├── LumaCore.Api
├── LumaCore.Api.Contracts
├── LumaCore.Core
└── LumaCore.Ui.Web
```

---

## Documentation Organization

The `/docs` folder is organized by audience and topic:

### By Audience

- **[Architecture](README.md)** — For architects: design decisions and patterns
- **[Features](../features/README.md)** — For developers: feature implementation details
- **[Development](../development/README.md)** — For contributors: setup, coding standards, workflow
- **[Deployment](../deployment/README.md)** — For operators: configuration and production setup

Navigate to the relevant section based on your role.

---

## Assets

The `/assets` folder contains non-code project assets:

### Branding
- Logos (SVG, PNG)
- Icons
- Brand guidelines

**Usage:** Referenced in documentation, UI, and marketing materials.

---

## Next Steps

### For New Developers
👉 **[Getting Started](../getting-started.md)** — Setup guide and first steps  
👉 **[Feature Pattern](feature-pattern.md)** — Learn the core architecture pattern

### For Architects
👉 **[Design Principles](principles.md)** — Why the structure is designed this way  
👉 **[Architecture Overview](README.md)** — High-level architectural decisions

---

© 2025 LumaCoreTech • MIT License
