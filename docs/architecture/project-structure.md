# Project Structure

This page is for people who need to find their way in the LumaCore repository and understand what each project is for. It walks you from the top-level layout down through every project, explains how API features are organized, and points to the files that hold the actual build configuration. For exact project references, packages, and MSBuild property values, follow the links to the source files; for *why* the product is split the way it is, read [Design Principles](principles.md) and the [Architecture Overview](README.md).

**On this page:** [Repository and solution](#repository-and-solution) · [Projects and dependencies](#projects-and-dependencies) · [API feature layout](#api-feature-layout) · [Build, SDK, and versioning](#build-sdk-and-versioning) · [Where to go next](#where-to-go-next)

---

## Repository and solution

### Top-level layout

```
LumaCore/
├── src/                          → All `LumaCore.*` code; project folders = assembly names
│   ├── LumaCore.Api/
│   ├── LumaCore.Api.Contracts/
│   ├── LumaCore.Api.Tests/
│   ├── LumaCore.BackgroundProcessing/
│   ├── LumaCore.BackgroundProcessing.Tests/
│   ├── LumaCore.Configuration/
│   ├── LumaCore.Core/
│   ├── LumaCore.Core.Tests/
│   ├── LumaCore.Data/
│   ├── LumaCore.Data.Tests/
│   ├── LumaCore.Definitions/
│   ├── LumaCore.HealthCheck/
│   ├── LumaCore.TestUtilities/
│   ├── LumaCore.Ui.Web/
│   ├── Directory.Build.props
│   ├── Directory.Build.targets
│   └── Directory.Packages.props
│
├── artifacts/                    → Build output root (`UseArtifactsOutput`; not in source control)
│   ├── bin/
│   └── obj/
│
├── docs/                         → Markdown for this product
│   ├── architecture/
│   ├── deployment/
│   ├── development/
│   └── features/
│
├── assets/                       → Branding and static non-code assets
│   └── branding/
│
├── build.net/                    → Git submodule with shared tooling (see Build, SDK, and versioning)
│
├── LumaCore.sln
├── global.json
├── LICENSE
└── README.md
```

If a project folder is on disk but not in the tree above, the repository is the source of truth.

### Solution file (`LumaCore.sln`)

[`LumaCore.sln`](../../LumaCore.sln) groups the projects in [`src/`](../../src/), documentation in [`docs/`](../../docs/), and **solution items** (`.editorconfig`, `global.json`, `Directory.Packages.props`, `Dockerfile`, `coverlet.runsettings`, GitHub workflows, and similar) so you can open them in one IDE session. The on-disk order of projects inside the file is not guaranteed to be alphabetical; use search or the folder tree to find a name.

---

## Projects and dependencies

The sections that follow describe each project's role, starting at the host and the UI and working down to contracts, the foundation, server libraries, and tests. Every project is described with the same frame:

- **Type** — the SDK and project shape
- **Project file** — link to the `.csproj`, which is the source of truth for `ProjectReference` and `PackageReference` items
- A short paragraph of what role it plays in the system

If you need the exact list of dependencies for a project, follow the project file link; this page does not duplicate that list.

### Entry: API and UI

#### LumaCore.Api
**Type:** `Microsoft.NET.Sdk.Web` (ASP.NET Core web application)  
**Project file:** [`LumaCore.Api.csproj`](../../src/LumaCore.Api/LumaCore.Api.csproj)

The HTTP host — the process you run as the application. The wiring of most of the `LumaCore.*` graph is easiest to read starting from this project's MSBuild file: it lists what the host pulls in, including optional `LumaCore.Ui.Web` when the **`IncludeBlazorUi`** property is `true` (the default in that file). For an API-only build without the Blazor static assets, publish with **`/p:IncludeBlazorUi=false`**.

The middleware and DI story is implemented in C# under `LumaCore.Api/`: a cross-cutting pipeline (HTTPS, CORS, logging, compression, authentication, and so on) and feature modules under `LumaCore.Api/Features/`. The running process uses Kestrel, and feature work follows [API feature layout](#api-feature-layout) below.

When the Blazor static assets are included, the host serves the WebAssembly app from the same origin as the API in the current setup, so the default case does not need a separate CORS story for the UI.

#### LumaCore.Ui.Web
**Type:** `Microsoft.NET.Sdk.BlazorWebAssembly` (Blazor WebAssembly client)  
**Project file:** [`LumaCore.Ui.Web.csproj`](../../src/LumaCore.Ui.Web/LumaCore.Ui.Web.csproj)

The static bundle the browser runs. To keep the client thin, the WASM build does not take a `ProjectReference` to server-only stacks such as `LumaCore.Data` or the full surface of `LumaCore.Core`. The browser talks to the server over HTTP using contract types from `LumaCore.Api.Contracts`.

### Contracts: DTOs and cross-layer definitions

#### LumaCore.Api.Contracts
**Type:** `Microsoft.NET.Sdk` (class library)  
**Project file:** [`LumaCore.Api.Contracts.csproj`](../../src/LumaCore.Api.Contracts/LumaCore.Api.Contracts.csproj)

Versioned DTOs and types for the HTTP surface, shared by the API and the Blazor project without pulling EF or server internals into the client. The folders under [`src/LumaCore.Api.Contracts/`](../../src/LumaCore.Api.Contracts/) hold the version segments (e.g. `V1/`).

A separate project keeps contract changes visible in one assembly and lets the UI reference DTOs without taking on `LumaCore.Data` or the persistence stack.

#### LumaCore.Definitions
**Type:** `Microsoft.NET.Sdk` (class library)  
**Project file:** [`LumaCore.Definitions.csproj`](../../src/LumaCore.Definitions/LumaCore.Definitions.csproj)

Very small, widely shared constants and types with minimal dependencies, so both client and server can reference the same binary where needed.

### Foundation

#### LumaCore.Core
**Type:** `Microsoft.NET.Sdk` (class library)  
**Project file:** [`LumaCore.Core.csproj`](../../src/LumaCore.Core/LumaCore.Core.csproj)

Hosting-agnostic building blocks: lifecycle, threading, diagnostics, controlled termination, filesystem helpers. The folder [`src/LumaCore.Core/`](../../src/LumaCore.Core) groups these into `Diagnostics/`, `IO/`, and `Threading/`, with root files such as `LifecycleManagement.cs` and `FailFast*.cs`.

The non-obvious design choices behind this foundation — the lifecycle state machine, the custom async primitives, `ExecutionStageMonitor`, and `FailFast` — are documented in [ADRs 0001–0004](decisions/README.md).

**Out of scope:** persona runtimes, long-term memory, vector stores, LLM orchestration, and similar high-level product domains do **not** belong in this assembly. They would live in other projects and consume `LumaCore.Core` as a foundation.

### Server-side libraries and tooling

#### LumaCore.BackgroundProcessing
**Type:** `Microsoft.NET.Sdk` (class library)  
**Project file:** [`LumaCore.BackgroundProcessing.csproj`](../../src/LumaCore.BackgroundProcessing/LumaCore.BackgroundProcessing.csproj)

Background work-queue processing infrastructure with DI integration, used by the API host for fire-and-forget background tasks.

#### LumaCore.Configuration
**Type:** `Microsoft.NET.Sdk` (class library)  
**Project file:** [`LumaCore.Configuration.csproj`](../../src/LumaCore.Configuration/LumaCore.Configuration.csproj)

Options registration, validation, and `Secret` metadata used by the API for configuration binding and startup-time checks.

#### LumaCore.Data
**Type:** `Microsoft.NET.Sdk` (class library)  
**Project file:** [`LumaCore.Data.csproj`](../../src/LumaCore.Data/LumaCore.Data.csproj)

EF Core, migrations, and database integration. Server-side only — the Blazor client must not reference it.

#### LumaCore.HealthCheck
**Type:** `Microsoft.NET.Sdk` with `OutputType=Exe` (small console executable)  
**Project file:** [`LumaCore.HealthCheck.csproj`](../../src/LumaCore.HealthCheck/LumaCore.HealthCheck.csproj)

A small, standalone HTTP **probe** intended to run inside containers (e.g. as a Docker `HEALTHCHECK` instruction). It exits with code `0` for healthy and `1` for unhealthy.

> **Two different "health" stories:** the in-process `AddHealthChecks` and the types under the API's `Features/Health/` are part of the web app and provide the actual health endpoints. The `LumaCore.HealthCheck` project here is a **separate** small HTTP **client** executable for container probes — it is not linked into the API as a `ProjectReference`. They are different layers.

### Tests and documentation projects

#### LumaCore.TestUtilities
**Type:** `Microsoft.NET.Sdk` (class library, `IsPackable=false`)  
**Project file:** [`LumaCore.TestUtilities.csproj`](../../src/LumaCore.TestUtilities/LumaCore.TestUtilities.csproj)

Shared xUnit helpers used by the test assemblies (in-memory `ILogger` capture, async wait helpers, and the like). Test-only; not referenced by production code.

#### Test assemblies (`LumaCore.*.Tests`)

Each `LumaCore.*.Tests` project sits next to the project it tests (for example, `LumaCore.Core.Tests` next to `LumaCore.Core`). The system under test and any `InternalsVisibleTo` declarations are in the respective `*.csproj` files, so opening a test project's `.csproj` is the quickest way to see what it covers.

#### docs/Docs.csproj
**Type:** `Microsoft.Build.NoTargets` (no-targets, IDE-only)  
**Project file:** [`docs/Docs.csproj`](../Docs.csproj)

A small workaround for a Visual Studio limitation: `.sln` files do not support wildcards, so every new markdown file would otherwise have to be added by hand to be visible in Solution Explorer. This project uses the official [`Microsoft.Build.NoTargets`](https://github.com/microsoft/MSBuildSdks/tree/main/src/NoTargets) SDK and a single `<None Include="**/*" />` item to pull the entire `docs/` tree into the IDE automatically. It builds nothing and produces no artifacts.

---

## API feature layout

Within `LumaCore.Api`, feature code lives under `Features/{FeatureName}/` rather than a top-level split between `Controllers/` and `Services/`. A typical feature folder contains a `ServiceRegistration.cs`, an `EndpointMapping.cs`, an optional `MiddlewareIntegration.cs`, and a `{Feature}Options.cs`. Not every feature uses all four; the actual files in the folder define the shape.

Wire-format DTOs stay in [`LumaCore.Api.Contracts`](#lumacoreapicontracts), so the client and host share types without the client depending on `Features/{FeatureName}` implementation details.

**Further reading:** [Feature Pattern](feature-pattern.md).

---

## Build, SDK, and versioning

Build configuration lives in a small set of shared MSBuild and tooling files at the root of `src/` and the repository:

| Read this for… | File |
|:---------------|------|
| Shared MSBuild defaults, `LangVersion`, analyzers, `UseArtifactsOutput`, MinVer, Source Link, shared package references | [`src/Directory.Build.props`](../../src/Directory.Build.props) |
| Shared targets (clean, publish output naming, …) | [`src/Directory.Build.targets`](../../src/Directory.Build.targets) |
| Centralized NuGet package versions | [`src/Directory.Packages.props`](../../src/Directory.Packages.props) |
| SDK `version` and `rollForward` | [`global.json`](../../global.json) (repo root) |

The intent across these files is consistent: a single C# `LangVersion` for the entire tree, a fixed SDK policy in `global.json`, versioning derived from Git tags through MinVer, and a single `artifacts/` output root configured by the shared targets.

### `build.net` submodule

The [`build.net`](https://github.com/LumaCoreTech/build.net) Git submodule (pinned to a fixed commit) carries shared IDE settings, OpenAPI scripts, and optional generators. It is **not** loaded as an application dependency — neither the API nor the libraries take a `ProjectReference` into the submodule. Submodule bumps are an explicit step rather than something a regular build does on its own.

---

## Where to go next

**Start here**

- **[Getting started](../getting-started.md)** — environment setup
- **[Design Principles](principles.md)** — *why* the product is split the way it is
- **[Architecture overview](README.md)** — system-level picture, including ADRs
- **[Feature Pattern](feature-pattern.md)** — how feature modules are organized inside `LumaCore.Api`

**Other documentation trees**

- **[Features](../features/README.md)** — feature-specific notes
- **[Development](../development/README.md)** — contributor workflow
- **[Deployment](../deployment/README.md)** — operations and configuration

**Assets**

- **[`assets/`](../../assets)** — branding for docs, UI, and published materials

---

© 2026 LumaCoreTech • MIT License
