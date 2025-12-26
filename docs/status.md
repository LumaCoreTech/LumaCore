# LumaCore Status & Roadmap

This document is the **single source of truth** for implementation progress.
It tracks each Feature and Capability with current maturity, and with **Phase Targets**
which indicate when a capability is planned to reach — or has already reached — the *Functional* or *Hardened* state.

---

## 🔷 Capability Maturity Scale

| Stage | Meaning |
|---|---|
| ❌ None        | No implementation exists yet |
| 🟡 Prototype   | Partial/experimental, unstable |
| 🟠 Developing  | Works in parts, not complete |
| 🟢 Functional  | Reliably usable, meets purpose |
| 🔵 Hardened    | Operationally safe, durable |

---

## 📌 Current Phase

| Phase | Scope |
|---|---|
| **0 — Infrastructure Foundation** *(complete)* | Versioning, Validation, OpenAPI, JWT Auth, CORS, Security Headers, HTTPS, Health, Proxy, Logging, Static Files, UI Shell, Status Page, System Diagnostics, Docker |
| **1 — LLM Integration & Persistence** *(active)* | Database (multi-DB), User Store, Ollama (models, health), Chat (sessions, SSE, history), Persona config, Web UI (Login, Chat), Docker Compose (Ollama/DB) |
| **2 — Storage & Retrieval** *(planned)* | Vector embeddings, Semantic search, RAG, Advanced features (persona switching, search, export), Native deployment (Services), Admin APIs, Config Management, System Dashboard |
| **3 — Observability & Hardening** *(planned)* | Log viewer, Metrics export, Security hardening (audit, rate limiting) |

**Phases** indicate where development is heading.
**Status tables** below describe the current maturity of each feature capability.

---

# 🔥 Feature Status + Capability Targets

## 🤖 LLM Integration — Model Access & Chat Orchestration

The LLM Integration layer connects LumaCore to AI models and orchestrates conversations. This is the **core engine** that brings personas to life.

### Model Backends
| Capability       | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|------------------|-------|---------------------------|-----------------|
| **Ollama Integration**<br/>_Connect to Ollama REST API (/api/v1/generate, /api/v1/chat) for model inference._ | ❌ None | P1 | P2 |
| **OpenAI API Support**<br/>_Support OpenAI-compatible API endpoints as alternative backend._ | ❌ None | P2 | P3 |
| **Model Health Checks**<br/>_Check Ollama connection, configured model availability, and inference readiness._ | ❌ None | P1 | P2 |
| **Model Configuration**<br/>_Configure available models and inference parameters (temperature, top_p, max_tokens)._ | ❌ None | P1 | P2 |

### Chat Management
| Capability          | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|---------------------|-------|---------------------------|-----------------|
| **Conversation Sessions**<br/>_Create, manage, and delete chat sessions with user isolation._ | ❌ None | P1 | P2 |
| **Message Streaming (SSE)**<br/>_Stream AI responses in real-time via Server-Sent Events._ | ❌ None | P1 | P2 |
| **Context Window Management**<br/>_Track token usage and apply truncation/sliding window to stay within model limits._ | ❌ None | P1 | P2 |
| **Multi-turn Conversations**<br/>_Maintain conversation history across multiple turns. Requires Chat History Storage._ | ❌ None | P1 | P2 |

---

## 🧩 Persona Runtime — Identity & Behavior Configuration

The Persona Runtime defines **who** the AI is — personality, tone, behavior patterns through configuration and prompts.

### Persona Configuration
| Capability       | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|------------------|-------|---------------------------|-----------------|
| **Persona Identity**<br/>_Name, description, avatar, and display properties. What users see in persona selection._ | ❌ None | P1 | P2 |
| **Persona Behavior & Prompts**<br/>_Personality traits (tone, style) and system prompt for LLM instruction. Defines how the persona communicates._ | ❌ None | P1 | P2 |
| **Persona Switching**<br/>_Switch between personas at runtime with isolated session contexts per persona._ | ❌ None | P2 | P3 |

---

## 💾 Storage & Retrieval — Context, History & Memory

Storage provides persistence and retrieval capabilities for conversations, enabling continuity and semantic memory.

### Database Support
| Capability          | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|---------------------|-------|---------------------------|-----------------|
| **Database Abstraction (EF Core)**<br/>_Entity Framework Core for multi-database support with migrations. Required for User Store and Chat History._ | ❌ None | P1 | P1 |
| **SQLite Support**<br/>_Default embedded database for zero-config self-hosting._ | ❌ None | P1 | P2 |
| **PostgreSQL Support**<br/>_Optional PostgreSQL backend (recommended for pgvector in P2)._ | ❌ None | P1 | P2 |
| **MySQL/MariaDB Support**<br/>_Optional MySQL/MariaDB backend for LAMP stack environments._ | ❌ None | P1 | P2 |
| **MSSQL Support**<br/>_Optional SQL Server backend for Windows/Azure environments._ | ❌ None | P1 | P2 |

### Conversation Storage
| Capability          | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|---------------------|-------|---------------------------|-----------------|
| **Chat History Storage**<br/>_Persist conversation messages and session state to database via EF Core. Enables multi-turn conversations and state restoration after server restarts._ | ❌ None | P1 | P2 |
| **Session Metadata**<br/>_Store session start time, persona, model used, token counts._ | ❌ None | P1 | P2 |
| **Message Search**<br/>_Full-text search across conversation history by keywords, date, and persona._ | ❌ None | P2 | P3 |
| **Export/Archive**<br/>_Export conversation history to JSON/Markdown for archival or sharing._ | ❌ None | P2 | P3 |

### Semantic Memory (RAG)
| Capability          | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|---------------------|-------|---------------------------|-----------------|
| **Embedding Generation**<br/>_Generate vector embeddings via Ollama embedding models (e.g., nomic-embed-text)._ | ❌ None | P2 | P3 |
| **Vector Storage**<br/>_Store embeddings in database with similarity search support._ | ❌ None | P2 | P3 |
| **Semantic Retrieval**<br/>_Query stored content by semantic similarity using cosine distance/dot product._ | ❌ None | P2 | P3 |
| **RAG Integration**<br/>_Inject retrieved context into LLM prompts automatically._ | ❌ None | P2 | P3 |

---
## 🔐 Security — Authentication & HTTP

Security establishes a foundation where mistakes are not fatal. It limits damage, ensures confidentiality and protects identity.

### Authentication & Authorization
| Capability | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|---|---|---|---|
| **Login (JWT)**<br/>_Authenticates users securely using token-based identity._ | 🟢 Functional | ✔ (P0) | P1 |
| **Token Refresh & Lifetime**<br/>_Issue refresh tokens to extend sessions with policy control._ | ❌ None | P2 | P2+ |
| **Role Claims**<br/>_Assign role-based permissions (admin, user, readonly) via JWT claims._ | 🟠 Developing | P1 | P2 |
| **User Store (Database)**<br/>_Database-backed user storage with password hashing (replaces hardcoded admin/changeme). Requires Database Support._ | ❌ None | P1 | P2 |
| **Endpoint Access Control**<br/>_Secures privileged API routes behind authorization policies._ | 🟡 Prototype | P1 | P2 |
| **Login throttling / lockout**<br/>_Mitigates brute-force password attempts with rate limiting and account lockout._ | ❌ None | P1 | P2 |

### HTTP Security
| Capability | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|---|---|---|---|
| **HTTPS support (Kestrel)**<br/>_Enables encrypted transport without proxy._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Reverse Proxy Support**<br/>_Trust X-Forwarded-* headers (Proto, Host, For) from configured proxies; TLS offload support._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **CORS Support**<br/>_Configurable CORS policies for cross-origin API access._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Secure defaults (TLS1.2+, HTTPS redirect)**<br/>_TLS 1.2+ enforced; HTTPS redirect available (opt-in via configuration)._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Security Headers (HSTS, CSP, X-Frame-Options)**<br/>_Adds HTTP security headers to protect against common web vulnerabilities._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **TLS exposure guidelines / proxy strategy**<br/>_Defines safe public exposure & TLS hardening._ | 🟢 Functional | ✔ (P0) | P1 |

### Runtime Security
| Capability | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|---|---|---|---|
| **Secrets via env / docker secrets**<br/>_Manages sensitive values securely via environment._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Config validation on startup**<br/>_Ensures misconfiguration fails early._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Audit logging (who did what, when)**<br/>_Tracks privileged operations for security review._ | ❌ None | P2 | P3 |
| **Rate limiting (API throttling)**<br/>_Prevents abuse via request rate limits._ | ❌ None | P2 | P3 |

---

## 🌐 API Foundation — Versioning, Validation & Documentation

Core API infrastructure that ensures consistency, discoverability, and correctness across all endpoints.

### API Structure
| Capability | Stage | Functional Target (Phase) | Hardened Target (Phase) |
|---|---|---|---|
| **API Versioning**<br/>_URL segment versioning (`/api/v1/`) with automatic version reporting headers._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Endpoint Validation**<br/>_Startup validation ensures all endpoints declare API version and authorization._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Request Validation**<br/>_Automatic DataAnnotations validation with RFC 7807 ProblemDetails responses._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **OpenAPI Documentation**<br/>_Native .NET 10 OpenAPI generation with per-version documents and CI tooling._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |

---

## 🔧 Administration APIs

APIs for system administration and configuration management.

| Capability      | Stage       | Functional Target (Phase) | Hardened Target (Phase) |
|-----------------|-------------|---------------------------|-----------------|
| **User Management API**<br/>_CRUD operations for user accounts (create, update, delete, list)._ | ❌ None | P1 | P2 |
| **System Configuration API**<br/>_Manage system settings via authenticated endpoints._ | ❌ None | P2 | P3 |
| **Model Management API**<br/>_CRUD operations for model configurations, enable/disable models, set defaults._ | ❌ None | P1 | P2 |
| **Persona Management API**<br/>_CRUD operations for persona configurations._ | ❌ None | P1 | P2 |

---

## 🧭 Infrastructure — Health, Observability & Static Assets

Core operational lifelines: probes ensure survival, observability makes debugging possible, static files serve the UI.

### Health 
| Capability      | Stage      | Functional Target (Phase) | Hardened Target (Phase) |
|-----------------|------------|---------------------------|-----------------|
| **Liveness Probe**<br/>_Confirms the process is running and recoverable._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Readiness Probe**<br/>_Signals readiness to accept traffic safely._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Dependency Health**<br/>_Check health of Ollama, database, and other dependencies._ | ❌ None | P1 | P2 |

### Logging & Observability
| Capability      | Stage      | Functional Target (Phase) | Hardened Target (Phase) |
|-----------------|------------|---------------------------|-----------------|
| **Structured Logging**<br/>_JSON-formatted logs with TraceId correlation across all requests._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |
| **Request Logging**<br/>_Middleware logs all HTTP requests with method, path, status, duration, IP._ | 🟡 Prototype | P1 | P2 |
| **Performance Metrics**<br/>_Track response times, token counts, model latency._ | ❌ None | P2 | P3 |

### Static Assets
| Capability      | Stage       | Functional Target (Phase) | Hardened Target (Phase) |
|-----------------|-------------|---------------------------|-----------------|
| **Static File Hosting**<br/>_Serves Blazor WebAssembly UI as static files from API root._ | 🔵 Hardened | ✔ (P0) | ✔ (P0) |

---

## 🖥 Web Interface — User Interaction

The web interface is where users **experience** LumaCore — chat with personas, manage conversations.

### Chat UI
| Capability      | Stage       | Functional Target (Phase) | Hardened Target (Phase) |
|-----------------|-------------|---------------------------|-----------------|
| **UI Shell (Layout)**<br/>_Blazor app shell with main layout, navigation, and health indicator in header._ | 🟢 Functional | ✔ (P0) | P1 |
| **Login UI**<br/>_Login form with credentials input, token storage, and error handling. Theme-aware styling supports all 5 themes._ | 🟢 Functional | ✔ (P1) | P2 |
| **Chat Interface**<br/>_Chat page with message input, history display, and persona integration._ | ❌ None | P1 | P2 |
| **Message Display**<br/>_Render messages with Markdown support, syntax highlighting, and copy-to-clipboard._ | ❌ None | P1 | P2 |
| **Streaming Display**<br/>_Show AI responses as they stream in real-time._ | ❌ None | P1 | P2 |
| **Session Management UI**<br/>_Sidebar with session list, create new sessions, switch between or delete existing._ | ❌ None | P2 | P3 |
| **Persona Selector**<br/>_Dropdown menu to switch personas with avatar preview and description._ | ❌ None | P2 | P3 |

### Theming & Visual Design
| Capability      | Stage       | Functional Target (Phase) | Hardened Target (Phase) |
|-----------------|-------------|---------------------------|-----------------|
| **Theme System (CSS Variables)**<br/>_CSS Custom Properties based theming system with hot-swappable themes via stylesheet link._ | 🟢 Functional | ✔ (P1) | P2 |
| **Official Themes**<br/>_LumaCore Dark (default) and LumaCore Light themes with cohesive color palettes._ | 🟢 Functional | ✔ (P1) | P2 |
| **Community Themes**<br/>_Missi Pink (playful pink/purple), Ocean Blue (calm blue tones), Forest Green (nature-inspired green). User-contributed themes ready._ | 🟢 Functional | ✔ (P1) | P2 |
| **Refined Sparkle Effects**<br/>_Subtle animations with admin-mode toggle (animations disabled in professional contexts)._ | 🟢 Functional | ✔ (P1) | P2 |
| **Responsive Design**<br/>_Mobile-optimized layouts with touch-friendly interactions and adaptive navigation._ | ❌ None | P1 | P2 |

---

## ⚙ Ops — Operations & Administration

Ops ensures the system can be deployed, configured, monitored, and maintained.

### Deployment  
| Capability         | Stage        | Functional Target (Phase) | Hardened Target (Phase) |
|--------------------|--------------|---------------------------|-----------------|
| **Docker Runtime**<br/>_Dockerfile builds LumaCore API + Blazor UI with native health check tool (.NET 10)._ | 🟢 Functional | ✔ (P0) | P2 |
| **Docker Compose**<br/>_Single-container orchestration for LumaCore with volumes and env config. Ollama integration planned._ | 🟢 Functional | ✔ (P0) | P2 |
| **Windows Service (SCM)**<br/>_Run as Windows Service with SCM integration, automatic startup, and recovery options._ | ❌ None | P2 | P3 |
| **Linux systemd Service**<br/>_Run as systemd service with Restart=always, Type=notify, and proper service management._ | ❌ None | P2 | P3 |

### Configuration Management
| Capability         | Stage        | Functional Target (Phase) | Hardened Target (Phase) |
|--------------------|--------------|---------------------------|-----------------|
| **Config Reload & Editor**<br/>_Edit configurations via UI with validation and in-process graceful restart._ | ❌ None | P2 | P3 |

### Monitoring
| Capability         | Stage        | Functional Target (Phase) | Hardened Target (Phase) |
|--------------------|--------------|---------------------------|-----------------|
| **Status Page**<br/>_Blazor page showing backend health status with refresh button and error handling._ | 🟡 Prototype | ✔ (P0) | P1 |
| **System Diagnostics API**<br/>_Admin-only endpoints for runtime info and configuration inspection with automatic secret masking._ | 🟢 Functional | ✔ (P0) | P1 |
| **System Metrics API**<br/>_Admin-only endpoint for memory, GC, process, and thread pool metrics. Extensible via IMetricsContributor for feature-specific metrics._ | 🟢 Functional | ✔ (P1) | P1 |
| **System Dashboard**<br/>_Full dashboard with metrics, performance charts, and detailed system information._ | ❌ None | P2 | P3 |

### Observability
| Capability         | Stage        | Functional Target (Phase) | Hardened Target (Phase) |
|--------------------|--------------|---------------------------|-----------------|
| **Metrics Export**<br/>_Expose /metrics endpoint in Prometheus format for monitoring systems._ | ❌ None | P2 | P3 |
| **Log Viewer**<br/>_Real-time log streaming with filtering by level, source, and correlation ID._ | ❌ None | P3 | later |

---

## 📊 Implementation Priority

**Phase 0 (Complete):** Foundation is stable
- ✅ Basic API structure exists
- ✅ API Versioning (URL segment `/api/v1/` with validation)
- ✅ Request validation (DataAnnotations with ProblemDetails)
- ✅ Health checks work (Liveness, Readiness)
- ✅ Reverse proxy support ready
- ✅ Static file hosting (API serves Blazor UI)
- ✅ CORS support configured
- ✅ Security Headers (HSTS, CSP, X-Frame-Options) with validation
- ✅ UI Shell exists (MainLayout, Health Indicator, Index/NotFound pages)
- ✅ Status page works (shows backend health, refresh button)
- ✅ JWT Auth functional
- ✅ Config validation on startup
- ✅ Structured logging with TraceId correlation
- ✅ Error handling with RFC 7807 ProblemDetails and TraceId (20 status codes mapped)
- ✅ HTTPS works (Kestrel)
- ✅ appsettings.json fully documented with comments
- ✅ OpenAPI native (.NET 10, PowerShell generator for CI)
- ✅ System diagnostics API (runtime info, configuration with secret masking)
- ✅ Docker ready (Dockerfile, docker-compose.yml, native HealthCheck tool)
- ⚠️ Hardcoded admin/changeme (bootstrap only, removed in P1)

**Phase 1 (Active):** Make it **useful**
- ✅ System metrics API (memory, GC, process, thread pool with extensible IMetricsContributor)
- ✅ Login UI (theme-aware, AuthService integration, token management)
- ✅ Theming System (5 themes: lumacore-dark/light, missi-pink, ocean-blue, forest-green)
- ✅ Refined Sparkle Effects (subtle animations with admin-mode toggle)
- 🎯 Database support (EF Core + multi-DB: SQLite, PostgreSQL, MySQL, MSSQL)
- 🎯 User store & user management API (replaces hardcoded admin/changeme)
- 🎯 Ollama integration (models, health checks, configuration)
- 🎯 Chat session management & SSE streaming
- 🎯 Multi-turn conversations with context window management
- 🎯 Persona configuration (personality, system prompts)
- 🎯 Chat history storage & session metadata
- 🎯 Web chat UI (chat interface, message display, streaming)
- 🎯 Docker Compose integration (Ollama, Database)

**Phase 2 (Later):** Make it **smart** and **manageable**
- 📌 Vector embeddings & semantic search
- 📌 RAG integration
- 📌 Advanced persona features (switching, search)
- 📌 Native deployment (Windows Service, systemd)
- 📌 Admin APIs (Model, Persona Management)
- 📌 Config Management & System Dashboard

**Phase 3 (Future):** Make it **excellent**
- 🔮 Log viewer & metrics export
- 🔮 Security hardening (audit logging, rate limiting)
- 🔮 Performance optimization & advanced metrics
- 🔮 Comprehensive observability

---

> This document is updated incrementally as LumaCore evolves.
> Feature capability tables remain authoritative — phases guide intent, not duplication.