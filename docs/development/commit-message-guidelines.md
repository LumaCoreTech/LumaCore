# Commit Message Guidelines

This document defines the **commit message standards** for all LumaCore repositories.

Commit messages are considered part of the codebase. They must be precise, consistent, and suitable for long‑term maintenance, auditing, and automation.

---

## 1. Commit Message Format

LumaCore follows the **Conventional Commits** specification.

### 1.1 Required Structure

```
<type>(<scope>): <subject>

<body>
```

- The **header line is mandatory**
- The **body is optional**, but required for non‑trivial changes
- The maximum length of the header line is **72 characters**

---

## 2. Commit Types

The following commit types are allowed:

| Type       | Meaning |
|------------|---------|
| `feat`     | Introduces a new user‑visible feature |
| `fix`      | Fixes a defect or incorrect behavior |
| `docs`     | Documentation changes only |
| `refactor` | Code restructuring without behavior changes |
| `test`     | Adding or modifying tests |
| `perf`     | Performance improvements |
| `style`    | Formatting only (no logic changes) |
| `chore`    | Tooling, build, CI, dependencies |
| `revert`   | Reverts a previous commit (reference original in body) |

Rules:
- Exactly **one type** MUST be used
- The type MUST reflect the **primary intent** of the change
- Avoid `chore` if a more specific type applies

---

## 3. Scope

The scope identifies the **affected feature or domain area**.

### 3.1 Scope Rules

- The scope MUST be provided
- The scope MUST be **stable and domain‑oriented**
- The scope MUST be one of the allowed scopes listed below

### 3.2 Allowed Scopes

**Core Scopes**

| Scope | Description |
|-------|-------------|
| `api` | REST API endpoints, controllers, middleware |
| `auth` | Authentication, JWT, identity, authorization |
| `core` | Repository setup, solution structure, foundational changes |
| `health` | Health checks, liveness/readiness endpoints |
| `openapi` | OpenAPI specification, documentation generation |

**Infrastructure Scopes**

| Scope | Description |
|-------|-------------|
| `build` | Build configuration, MSBuild, project files |
| `ci` | GitHub Actions, workflows, CI/CD pipelines |
| `deps` | Dependency updates (NuGet, npm) |
| `docker` | Dockerfile, Docker Compose, container configuration |

**Documentation Scopes**

| Scope | Description |
|-------|-------------|
| `architecture` | Architecture documentation |
| `deployment` | Deployment guides and configuration docs |
| `development` | Development guides and contributor docs |
| `docs` | General documentation (when no specific scope applies) |
| `features` | Feature documentation |
| `guides` | Getting started and how-to guides |
| `roadmap` | Status tracking, phase planning, design notes |

**Feature Scopes**

| Scope | Description |
|-------|-------------|
| `assets` | Static assets, branding, logos |
| `cors` | CORS configuration and policies |
| `errors` | Error handling, ProblemDetails, exception handlers |
| `https` | HTTPS redirection configuration |
| `logging` | Logging, Serilog configuration |
| `proxy` | Proxy/forwarded headers configuration |
| `security` | Security headers (CSP, HSTS, etc.) |
| `system` | System diagnostics, runtime info, configuration |
| `ui` | Blazor UI components, pages, layouts, styling |
| `validation` | Request validation, validation filters |

### 3.3 Adding New Scopes

New scopes may be introduced when:

- A new major feature area is added
- An existing scope becomes too broad
- A clear domain boundary emerges

New scopes MUST be added to this document before use.

---

## 4. Subject Line

The subject describes **what the commit does** in a concise form.

### 4.1 Subject Rules

- Type and scope MUST be **lowercase**
- Subject MUST **start with a capital letter**
- Subject MUST use **imperative mood**
- Subject MUST NOT end with punctuation
- Subject MUST describe the change, not the implementation

### 4.2 Examples

Valid:

```
fix(auth): Correct token signature calculation
feat(openapi): Expose schema version endpoint
refactor(core): Simplify configuration resolution
```

Invalid:

```
fix(auth): fixed token bug
feat: added new endpoint
refactor(core): TokenSigner.cs cleanup
```

---

## 5. Commit Body

The commit body provides context for the change. Describe **what** was changed at a high level and **why** it was necessary. Focus on reasoning and impact rather than low-level details the diff already shows.

### 5.1 When a Body Is Required

A commit body MUST be included if:

- The change is not self‑explanatory
- The behavior differs from previous expectations
- A workaround or limitation is introduced
- The change affects correctness, security, or compatibility

### 5.2 Body Rules

- Separate header and body with a blank line
- Wrap lines at **72 characters**
- Use full sentences
- Reference issues or decisions if applicable

### 5.3 Example

```
fix(auth): Correct token signature calculation

The previous implementation used the raw secret instead of the
key‑derived signing value, causing invalid tokens during key rotation.
```

---

## 6. Breaking Changes

A breaking change is any modification that may cause existing consumers to fail — including API clients, configuration files, or database schemas.

**Examples of breaking changes:**

- Removing or renaming an endpoint
- Changing response structure or status codes
- Renaming configuration keys
- Requiring new mandatory settings
- Database schema changes requiring migration

### 6.1 Marking a Breaking Change

Use the `!` suffix to make breaking changes visible in the commit history:

```
feat(auth)!: Change token response structure
```

Additionally, use the `BREAKING CHANGE` footer to explain what breaks:

```
feat(auth)!: Change token response structure

BREAKING CHANGE: Response field "token" renamed to "accessToken".
Clients must update their JSON parsing logic.
```

```
feat(core)!: Rename User.Id to User.Uuid

BREAKING CHANGE: Database migration required.
Run `dotnet ef database update` after deployment.
```

### 6.2 Requirements

- The breaking impact MUST be clearly described
- Migration expectations SHOULD be mentioned if applicable

---

## 7. Commit Granularity

Guidelines:

- A commit SHOULD represent **one logical change** where practical
- Mixing unrelated changes is discouraged
- Refactorings SHOULD NOT be bundled with behavior changes

If multiple concerns are addressed, consider splitting into multiple commits.

> [!TIP]
> Use `git add -p` to stage changes interactively and ensure atomic commits.

---

## 8. Prohibited Commit Messages

The following are not acceptable:

```
fix
wip
stuff
minor changes
cleanup
```

Commits MUST always be understandable **without inspecting the diff**.

---

## 9. Compliance

These guidelines serve as a shared reference for consistent commit messages. While not automatically enforced, contributors are expected to follow them in good faith.

Future repositories MAY introduce automated validation if needed.

---

## 10. Rationale

These guidelines exist to ensure:

- Long‑term maintainability
- Meaningful project history
- High‑quality reviews
- Reliable automation (changelogs, releases, audits)

Consistency is valued higher than personal preference.

---

© 2025 LumaCoreTech • MIT License