# Architecture Overview

**Audience:** Architects and Developers seeking to understand LumaCore's design

This section explains how LumaCore is designed and why architectural decisions were made the way they were.

---

> **About this documentation**
> This section describes LumaCore's **target architecture** - how the system is designed to work when fully implemented.
> For the current implementation status, see: **[Status & Roadmap](../status.md)**

---

## Understanding LumaCore's Architecture

LumaCore follows a **feature-oriented architecture** where every major capability is a self-contained module. This isn't just about organizing files - it's a fundamental architectural principle that shapes how the entire system works.

### Core Concept

> **"Every feature owns its endpoints, services, configuration, and contracts."**

This means when you want to understand authentication, you open the *Auth* feature folder and everything you need is there. No hunting across the codebase for controllers, data models, or services scattered in different locations.

---

## Architecture Documents

### 🎯 [Design Principles](principles.md)
**Start here to understand the "why" behind LumaCore's architecture.**

- Feature-first design philosophy
- Separation of concerns (API vs Core)
- Convention over configuration
- Fail-fast validation
- Explicit dependencies

This document explains the architectural decisions and trade-offs that shape the entire system.

### 🏗️ [Project Structure](project-structure.md)
**Learn how the codebase is organized.**

- Repository layout
- Build system (Directory.Build.props)
- Artifacts management
- Versioning strategy
- Project dependencies

Understand where everything lives and why it's organized that way.

### ⚙️ [Feature Pattern](feature-pattern.md)
**Master the feature-based architecture pattern.**

- Feature anatomy
- Service registration lifecycle
- Endpoint mapping
- Feature isolation principles
- Real examples from *Auth*, *System*, *Health* features

This is the most important pattern in LumaCore - understand this and you understand the system.

### 🔒 [Security Architecture](security.md)
**Understand how LumaCore handles security.**

- JWT authentication flow
- Token validation and lifecycle
- Role-based authorization
- Configuration security
- HTTPS enforcement
- Security best practices

Learn how security is baked into the architecture from the ground up.

### 📝 [Decision Records (ADRs)](decisions/README.md)
**Understand *why* specific patterns were chosen — and what was deliberately rejected.**

Short, focused records of individual design decisions. Where the documents above explain *how the system works*, ADRs explain *why a particular pattern was chosen over the obvious alternative*. Read these when reviewing changes to core patterns or when an unfamiliar design feels surprising.

---

## Key Architectural Decisions

### Feature-Based Architecture

LumaCore organizes all code by feature, not by technical layer. Each feature owns its endpoints, services, configuration, and contracts in a single folder.

**Example:** The *Auth* feature lives in `Features/Auth/` within the Api project (`src/LumaCore.Api/Features/Auth/`).

**Why?** Traditional ASP.NET projects scatter related code across Controllers, Services, Models folders—requiring you to open 5+ files in different locations to understand one capability. Feature-based architecture keeps everything together.

👉 **[Read more: Feature Pattern](feature-pattern.md)** - Complete guide to LumaCore's feature architecture

---

### Separation of API and Core

LumaCore is split into two projects:

- **LumaCore.Api** — Handles HTTP communication, routing, authentication.
- **LumaCore.Core** — Hosting-agnostic foundation library (lifecycle, async primitives, diagnostics, controlled termination, filesystem utilities). Higher-level domain capabilities deliberately live elsewhere; `LumaCore.Core` is intentionally kept low-level.

**Why?** Testability, reusability, and clarity. Foundation code can be tested without HTTP. The API could be replaced with a CLI or desktop app while reusing Core.

Both compile into a single application (monolithic deployment).

---

### Fail-Fast Configuration

All configuration is validated when the application starts. If something is wrong (like a JWT signing key that's too short, or a required setting that's missing), the application refuses to start and shows a clear error message.

**Why?** Catching configuration errors immediately during deployment is much better than discovering them in production at 3 AM when the application crashes under load.

👉 **[Read more: Design Principles](principles.md)** - Deep dive into architectural decisions

---

## Target Architecture

LumaCore's architecture consists of three layers, each with clear responsibilities:

```
┌─────────────────────┐
│   Client Layer      │  Blazor UI + External clients
└──────────┬──────────┘
           │
┌──────────▼──────────┐
│    API Layer        │  HTTP, Auth, Routing, Features
│  (LumaCore.Api)     │
└──────────┬──────────┘
           │
┌──────────▼──────────┐
│   Core Layer        │  Lifecycle, Threading, Diagnostics, …
│  (LumaCore.Core)    │  (hosting-agnostic foundation)
└─────────────────────┘
```

### Features

LumaCore is built around self-contained feature modules. Each feature owns its endpoints, services, configuration, and contracts.

👉 **[See all features](../features/README.md)** — Complete list with documentation links

The feature-based architecture is explained in detail in the [Feature Pattern](feature-pattern.md) document.

> 📊 **For current implementation status**, see **[Status & Roadmap](../status.md)**

### Cross-Cutting Concerns

These foundational capabilities support all features:

- **Structured Logging** — Serilog with file rotation and multiple outputs
- **Response Compression** — Gzip/Brotli for reduced bandwidth
- **OpenAPI/Swagger** — Interactive API documentation
- **Fail-Fast Configuration** — Validation at startup

---

## Understanding the Request Flow

Every HTTP request flows through a middleware pipeline in this order:

1. **HTTPS Redirect** (if enabled)
2. **Forwarded Headers** (Preserve client info when behind a proxy)
3. **CORS** (Cross-Origin Resource Sharing)
4. **Routing** (Match request to endpoint)
5. **Authentication** (JWT validation)
6. **Authorization** (Role checks)
7. **Request Logging** (Serilog)
8. **Response Compression** (Gzip/Brotli)
9. **Endpoint Handler** (Feature logic)

Each middleware does one specific job. Forwarded Headers processes reverse proxy information, CORS handles cross-origin requests, authentication validates the JWT token, authorization checks roles, and endpoint handlers contain the business logic.

👉 **[Read more: Security Architecture](security.md)** - Complete authentication and authorization flow

---

## Core Patterns and Technologies

LumaCore uses industry-standard patterns and modern .NET technologies:

**Patterns:**
- **Dependency Injection** - Components receive their dependencies rather than creating them
- **Options Pattern** - Configuration is strongly-typed and validated
- **Minimal APIs** - Clean endpoint mapping with extension methods
- **Feature Modules** - Self-contained, testable components

**Technology Stack:**
- **.NET 10** - Latest LTS with best performance and modern C# 13
- **Serilog** - Structured logging with async buffering
- **JWT** - Stateless authentication that scales
- **Swagger/OpenAPI** - Interactive API documentation

👉 **[Read more: Design Principles](principles.md)** - Why these choices and how they work together

---

## Next Steps

### For Developers
👉 **[Project Structure](project-structure.md)** - Learn the codebase layout  
👉 **[Feature Pattern](feature-pattern.md)** - Master the core pattern  

### For Architects
👉 **[Design Principles](principles.md)** - Deep dive into decisions  
👉 **[Security Architecture](security.md)** - Security design details  

### For Operators
👉 **[Configuration](../deployment/configuration.md)** - Production config  
👉 **[Deployment](../deployment/docker.md)** - Deploy to production  

---

## Questions?

- 📖 **[Documentation Index](../README.md)** — Main documentation hub
- 💻 **[Development](../development/README.md)** — Development guides
- 🚀 **[Deployment](../deployment/README.md)** — Deployment guides

---

© 2025 LumaCoreTech • MIT License
