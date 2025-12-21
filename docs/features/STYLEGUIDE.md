# Feature Documentation Styleguide

This document defines the structure, style, and content guidelines for LumaCore feature documentation. Follow these rules to ensure consistency across all feature docs.

---

## Target Audience

Feature documentation serves two audiences:

1. **API Consumers** — Developers integrating with the REST API. They need to understand endpoints and their purpose.

2. **Operators/Self-Hosters** — People deploying and configuring LumaCore. They need configuration options, environment variables, and validation rules.

**Not the target audience:** Feature developers. They should refer to the source code with its XML documentation comments.

---

## Design Philosophy

Feature docs and OpenAPI serve different purposes:

| Feature Docs | OpenAPI |
|--------------|---------|
| Context and guidance | Technical specifications |
| The "why" and "how to use" | Schemas, examples, status codes |
| Prose descriptions | Machine-readable contracts |
| Configuration and setup | Request/response formats |

This separation avoids redundancy and ensures a **single source of truth** for API contracts. Duplicating JSON examples in both places creates drift risk — when the API changes, only one location gets updated.

**Rule of thumb:** If you're tempted to add a JSON example to a feature doc, add it to the OpenAPI annotations in the code instead. The feature doc should link to OpenAPI, not duplicate it.

> **Exception:** Cross-cutting features like *Error Handling* may include JSON examples and status codes to illustrate behavior that applies across the entire API. These features define response formats that aren't tied to specific endpoints.

---

## Document Structure

Every feature document follows this exact section order:

```
# FeatureName Feature (Short Description)

Intro paragraph

> [!WARNING] or [!NOTE] (if applicable)

---

## Endpoints (if applicable)

(one or more endpoint sections)

---

## Configuration

---

## Registered Services

---

## Pipeline Order

---

## Typical Usage

---

## Related Features (if applicable)

---

© 2025 LumaCoreTech • MIT License
```

---

## Section Details

### Title and Introduction

```markdown
# FeatureName Feature (Short Description)

The *FeatureName* feature provides [what it does] for [whom/what purpose]. [Expand with 
additional context about capabilities and use cases — aim for a flowing paragraph that 
gives readers a clear mental model of the feature.]
```

Rules:
- Feature name in title: plain text
- Feature name in prose: *italicized*
- Write a flowing paragraph, not a bullet list
- Explain the "why" and "what", not implementation details
- Aim for 2–4 sentences that read naturally

### Admonitions

Use GitHub-style admonitions sparingly:

```markdown
> [!NOTE]
> Informational context (e.g., temporary limitations, bootstrap mechanisms)

> [!WARNING]
> Security-sensitive information or important caveats

> [!IMPORTANT]
> Critical information that must not be overlooked
```

### Endpoints

Introduce the endpoints section with a brief sentence explaining what endpoints the feature provides:

```markdown
## Endpoints

The *FeatureName* feature exposes [number] endpoints: [brief description of each].
```

Each endpoint follows this structure:

```markdown
### `METHOD /api/v1/path`

Short description of what the endpoint does. [Explain the purpose and typical use case.]

[One or two paragraphs describing the behavior in prose. Explain what the endpoint 
does, what it returns, and any notable characteristics. Avoid bullet lists for 
behavior — write flowing text that reads naturally.]

**Requires:** a valid JWT (`Authorization: Bearer <token>`)

> For request/response schemas and examples, see the [OpenAPI documentation](../api/README.md).
```

For endpoints requiring specific roles:

```markdown
**Requires:** a valid JWT with the `admin` role (`Authorization: Bearer <token>`)
```

For endpoints that work with any authentication method:

```markdown
**Requires:** an authenticated user
```

Rules:
- Introduce endpoint section with a brief overview sentence
- Describe behavior in prose paragraphs, not bullet lists
- **Requires:** states what's needed (only for protected endpoints)
- Do not include JSON examples or HTTP request samples — OpenAPI is the single source of truth for schemas and examples
- OpenAPI link after every endpoint
- Separate multiple endpoints with `---`

### Features Without Endpoints (Middleware-Only)

Some features (e.g., *CORS*, *SecurityHeaders*, *ProxyHeaders*) configure middleware but do not expose endpoints. For these features:

- Omit the `## Endpoints` section entirely
- Focus on `## Configuration` and `## Pipeline Order`
- Pipeline Order is especially important — middleware order often matters

```markdown
# FeatureName Feature (Short Description)

The *FeatureName* feature configures [what middleware does]. It runs in the 
request pipeline and [explain the effect on requests/responses].

---

## Configuration

[Options, examples, etc.]

---

## Registered Services

The *FeatureName* feature does not register any injectable services.

---

## Pipeline Order

The *FeatureName* feature registers middleware only — no endpoints. The order 
of `Use{FeatureName}()` [matters/does not matter] because [reason].

[If order matters: explain what must come before/after]

---

## Typical Usage

[When/why would someone enable this feature]

---

© 2025 LumaCoreTech • MIT License
```

### Configuration

For features with configuration options:

````markdown
## Configuration

All [feature]-related settings are configured in `appsettings.json` (or via environment variables) under the `SectionName` section.

### Options

| Option | Required | Default | Validation | Description |
|--------|----------|---------|------------|-------------|
| `OptionName` | Yes/No | `value` | Validation rule | What it does |

If any option is missing or invalid, LumaCore refuses to start.

### Example: `appsettings.json`

```json
{
  "SectionName": {
    "Option": "value"
  }
}
```

### Environment Variables

Options can also be set via environment variables with the `SectionName__` prefix:

```text
SectionName__Option=value
```

The feature is registered via `builder.Add{FeatureName}Feature()` and mapped to the versioned API group in `Program.cs`.
````

For features without own configuration:

```markdown
## Configuration

The *FeatureName* feature does not introduce additional configuration options. It relies on the *OtherFeature* feature for [what it needs].

The feature is registered via `builder.Add{FeatureName}Feature()` and mapped to the versioned API group in `Program.cs`.
```

> **Note:** For infrastructure features that don't expose API endpoints (e.g., middleware-only features), use `app.Use{FeatureName}Feature()` or `app.Map{FeatureName}Feature()` directly on the application builder instead.

Rules:
- Options table with: Option | Required | Default | Validation | Description
- Always mention fail-fast behavior for invalid config
- Always include the registration line at the end
- Reference other features in *italics*

### Registered Services

Always include this section.

If the feature registers injectable services:

```markdown
## Registered Services

The *FeatureName* feature registers the following services for dependency injection:

| Service | Lifetime | Description |
|---------|----------|-------------|
| `IServiceName` | Singleton/Scoped/Transient | What it does |
```

If the feature does not register any services:

```markdown
## Registered Services

The *FeatureName* feature does not register any injectable services.
```

Rules:
- Always include this section — never omit it
- Only list services that consumers might inject
- Keep descriptions brief and consumer-focused
- Omit internal implementation services

### Pipeline Order

```markdown
## Pipeline Order

The *FeatureName* feature registers [endpoints only / middleware and endpoints]. The order of `Map{FeatureName}Feature()` relative to other features [does / does not] matter.

[If order matters, explain why and what must come before/after]
```

Rules:
- Always state whether order matters
- For middleware features, explain the required position
- For endpoint-only features, state that order doesn't matter

### Typical Usage

This section describes how users interact with the feature. Use a **numbered list for step-by-step flows** — these are easier to follow than prose when the reader wants to understand a sequence of actions.

```markdown
## Typical Usage

[Optional: brief intro sentence setting the context]

1. First, the user does X.
2. Then, Y happens.
3. Finally, the user can Z.

[Optional: closing paragraph about future evolution or additional context]
```

Rules:
- Use numbered lists for sequential workflows — they're easier to scan
- Add a brief intro sentence if context is needed
- Keep steps concise and action-oriented
- Use prose for the closing paragraph (future evolution, additional notes)

### Related Features

Only include if there are real dependencies:

```markdown
## Related Features

- [*FeatureName*]({feature-name}.md) — Why it's related (dependency, displays data from, etc.)
```

Rules:
- Only list actual dependencies, not thematically related features
- Feature name in link text: *italicized*
- Brief explanation of the relationship

### Copyright Footer

```markdown
---

© 2025 LumaCoreTech • MIT License
```

---

## Writing Style

### Prefer Prose Over Lists

Feature documentation should read like a well-written guide, not a specification sheet. Use flowing paragraphs for descriptions and explanations. However, **numbered lists are appropriate for sequential workflows** where the reader needs to follow steps in order.

Bullet lists are appropriate for:

- Short enumerations within prose (3–5 items)
- Related Features section

Tables are appropriate for:

- Configuration options
- Registered services

Numbered lists are appropriate for:

- Step-by-step workflows (Typical Usage)
- Sequential procedures

Bullet lists are **not** appropriate for:

- Feature introductions (write a paragraph instead)
- Endpoint behavior descriptions (write prose instead)

**Example — Feature introduction as prose:**
```markdown
The *Auth* feature provides JWT-based authentication and identity introspection 
for the LumaCore HTTP API. It validates tokens, issues access tokens for clients, 
and provides helper endpoints for inspecting the current identity.
```

### Create a Red Thread

Each section should flow naturally into the next. Use transitional phrases and ensure the document tells a coherent story from introduction through configuration to usage.

---

## Text Formatting

| Element | Format | Example |
|---------|--------|---------|
| Feature names in prose | *Italic* | The *Auth* feature provides... |
| Feature names in titles | Plain | # Auth Feature |
| Configuration keys | `Code` | Set `Issuer` to... |
| HTTP methods | `Code` + uppercase | `GET`, `POST` |
| Endpoint paths | `Code` | `/api/v1/auth/login` |
| Type names | `Code` | `LoginResponse` |
| Environment variables | `Code` | `Jwt__Issuer` |
| Roles | `Code` | `admin` role |

### Punctuation

- Use em-dash (—) in descriptions, not hyphen (-): `name` — the user's name
- Use curly quotes only in prose, straight quotes in code
- End list items with periods if they are complete sentences

### Language

- Write in present tense
- Use active voice
- Be concise — avoid filler words
- Address the reader as implicit "you" (don't say "the user should")

### What NOT to Include

These belong in OpenAPI documentation, not feature docs:

- JSON request/response examples
- HTTP request samples
- Response status codes (401, 403, etc.)

These are implementation details for developers, not documentation for consumers:

- .NET class names: ~~`ClaimsPrincipal`~~, ~~`ClaimTypes.Role`~~
- C# syntax: ~~`user.Identity?.Name`~~
- Internal method names: ~~`TryParseUnixTimeSeconds`~~
- Framework internals: ~~`.ValidateDataAnnotations()`~~
- Internal service wiring: ~~`AddOptions<T>()`~~

Instead, describe the **observable behavior**:
- ❌ "Uses `ClaimsPrincipal` to derive roles from `ClaimTypes.Role`"
- ✅ "Extracts roles from the authentication context"

### What TO Include

- Endpoint paths and HTTP methods
- Prose descriptions explaining purpose and behavior
- Configuration options with validation rules
- Injectable services (interface names)
- Pipeline order requirements
- Authentication requirements

---

## Checklist for New Feature Docs

- [ ] Title follows pattern: `# Name Feature (Short Description)`
- [ ] Intro is a flowing paragraph (not a bullet list)
- [ ] Intro uses *italic* for feature name

**For features with endpoints:**
- [ ] Endpoints section has intro sentence
- [ ] Endpoint behavior described in prose (not bullets)
- [ ] All endpoints have: Requires (if auth), OpenAPI link
- [ ] No JSON examples in endpoints (OpenAPI is single source of truth)

**For all features:**
- [ ] Configuration section includes Options table (if applicable)
- [ ] Configuration ends with registration line
- [ ] Registered Services section present (with table or "does not register any")
- [ ] Pipeline Order states whether order matters
- [ ] Typical Usage uses numbered list for sequential steps
- [ ] Related Features lists only real dependencies
- [ ] Copyright footer present
- [ ] No .NET internals or C# syntax
- [ ] Feature names italic in prose
- [ ] Em-dashes used in descriptions
- [ ] Document reads as coherent narrative (red thread)

---

## Examples

See these files as reference implementations:

- [auth.md](auth.md) — Feature with configuration, services, multiple endpoints
- [admin.md](admin.md) — Feature without own configuration, single endpoint

---

© 2025 LumaCoreTech • MIT License