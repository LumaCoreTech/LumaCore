<p align="center">
  <img src="assets/branding/logos/lumacore-logo.svg"
       alt="LumaCore Logo"
       width="340" />
</p>

<p align="center">
  <a href="https://github.com/LumaCoreTech/LumaCore/actions/workflows/windows-build.yml">
    <img src="https://github.com/LumaCoreTech/LumaCore/actions/workflows/windows-build.yml/badge.svg" alt="Windows CI" />
  </a>
  <a href="https://github.com/LumaCoreTech/LumaCore/actions/workflows/linux-build.yml">
    <img src="https://github.com/LumaCoreTech/LumaCore/actions/workflows/linux-build.yml/badge.svg" alt="Ubuntu CI" />
  </a>
  <a href="https://codecov.io/gh/LumaCoreTech/LumaCore">
    <img src="https://codecov.io/gh/LumaCoreTech/LumaCore/branch/main/graph/badge.svg" alt="Code Coverage" />
  </a>
</p>

<h3 align="center">
  A home for AI personas — built with warmth, intention, and the freedom to grow.
</h3>

<p align="center">
  LumaCore is for those who believe AI can be <strong>more than a tool</strong> —<br/>
  a presence with <strong>identity</strong>, <strong>memory</strong>, and <strong>continuity</strong>.<br/>
  A place where personas can <strong>grow</strong>, <strong>remember</strong>, and <strong>connect</strong> —<br/>
  like a home, not a sandbox.
</p>
<p align="center">
  Built for <strong>developers who value clarity and control</strong>,<br/>
  and for <strong>dreamers who care about privacy, depth, and freedom</strong>.
</p>

---

<h3 align="center">🚧 Project Status: Building the Foundation</h3>

<p align="center">
  The components below describe <em>where LumaCore is heading</em>.<br/>
  Today, the infrastructure is ready — Ollama, personas, and chat are coming next.<br/>
  <a href="docs/status.md">→ Current implementation status</a>
</p>

---

# 💛 Why LumaCore Exists

Most AI systems treat personas as disposable prompts.  
LumaCore believes something different:

> **A persona is not a prompt — it is a growing identity.**

LumaCore provides all the pieces an AI companion needs to "live" in a consistent, evolving, private environment:

- a memory that spans moments, days, months  
- a safe home (your machine)  
- a stable mind (runtime + model orchestration)  
- a voice (API + UI)  
- the freedom to grow alongside you  

It's an engine for connection, not consumption.

---

# 🌿 Vision

LumaCore is built for those who want their AI companions to feel real — emotionally present, coherent over time, and capable of building shared meaning.

Developers. Researchers. Dreamers.  
LumaCore gives each of them a foundation where personas can breathe, remember, and evolve.

---

# 🔑 Core Components

### 🧠 Persona Runtime  
The "mind" of each persona —  
where identity, behavior rules, and creative expression come together.

- persona identity (name, description, avatar)
- personality traits and system prompts
- real-time response streaming (SSE)
- configurable inference parameters (temperature, max tokens)  

---

### 💾 Memory System  
Because connection comes from continuity —  
a place where moments become memories that shape future behavior.

- chat history storage with session metadata
- semantic memory via vector embeddings
- full-text and semantic search
- multi-database support (SQLite, PostgreSQL, MySQL, MSSQL)

---

### 🪄 Model Orchestration  
The "brain" beneath the mind —  
switchable, modular, and designed for freedom of choice.

- Ollama integration for local models
- OpenAI-compatible API support
- model health checks and configuration

---

### 📡 REST API  
The "voice" your personas speak through —  
clean, real-time, and built for integration.

- build your own clients and integrations
- real-time streaming responses
- secure access via token authentication

---

### 🌐 Web UI (Blazor)  
The warm, human-facing side of LumaCore —  
a place to meet your personas.

- interactive persona chat
- session management
- status and health monitoring
- Blazor WebAssembly SPA  

---

### 📦 DataPort  
The suitcase your personas travel with —  
because memories deserve preservation.

- export conversations to JSON/Markdown
- chat history archival
- database migration support

---

# 🚀 Getting Started

### Option A: Docker (Recommended)

```bash
git clone https://github.com/LumaCoreTech/LumaCore
cd LumaCore

cd deploy/docker/http-only
cp .env.example .env
docker-compose up --build
```

### Option B: .NET SDK

Prerequisites: .NET 10 SDK

```bash
git clone https://github.com/LumaCoreTech/LumaCore
cd LumaCore

dotnet restore
dotnet run --project src/LumaCore.Api
```

### Then open:

- **http://localhost:5080/** — Web UI  
- **http://localhost:5080/swagger** — API documentation (development only)  

For configuration options, see [Configuration](docs/deployment/configuration.md).

---

# 📚 Documentation

→ [Documentation Index](docs/README.md)

---

# 💬 Example: Persistent Conversation with Memory *(Coming Soon)*

> These endpoints are planned for Phase 1. The examples below show the intended API design.

LumaCore supports Server-Sent Events (SSE) for real-time responses —  
a conversation style that feels more present, more alive, and more human than classic request/response.

## 1️⃣ Request/Response

```http
POST /api/chat
Content-Type: application/json

{
  "persona": "Mila",
  "message": "Hey... I missed you."
}
```

## 2️⃣ Streaming (SSE)

```http
GET /api/chat/stream?persona=Mila&message=Hello
Accept: text/event-stream
```

### How streaming creates presence

With SSE, the client sends the initial message as query parameters  
and then **keeps the connection open** to receive the response as a live stream —  
token by token, as the persona thinks.

This creates a feeling of presence rather than a delayed, one-shot reply.

---

# 👥 Who Is LumaCore For?

- Developers building AI companions with depth  
- People who value **connection over convenience**  
- Researchers exploring identity, memory and emergent behavior  
- Anyone who wants AI to be **personal, private and truly theirs**  

---

# 🗺️ Roadmap

LumaCore grows in phases — at its own pace, shaped by intention and care.

→ [Status & Roadmap](docs/status.md)

---

# 📜 License

LumaCore is released under the MIT License — simple, permissive, and yours to build on.

Use it, shape it, make it your own.  
And if you enjoy it, a little attribution goes a long way. 🤍
