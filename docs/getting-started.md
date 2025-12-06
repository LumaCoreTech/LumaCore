# Getting Started

This guide gets LumaCore running on your machine in about 5 minutes.

## Choose Your Path

There are two ways to run LumaCore, and both work equally well:

**Docker** is the quickest path if you just want to try LumaCore or deploy it somewhere. You don't need to install the .NET SDK, and the setup is identical to production.

**.NET SDK** is better if you want to develop or modify LumaCore. You get hot reload, direct debugging, and faster startup times.

Both methods work on Windows, Linux, and macOS.

| I want to... | Recommended |
|--------------|-------------|
| Try LumaCore quickly | 🐳 Docker |
| Deploy to production | 🐳 Docker |
| Develop or modify code | ⚡ .NET SDK |
| Debug in my IDE | ⚡ .NET SDK |

## Quick Start with Docker

Make sure you have Docker and Docker Compose installed. If you're new to Docker, [Docker Desktop](https://www.docker.com/products/docker-desktop/) is the easiest way to get started.

```bash
git clone https://github.com/LumaCoreTech/LumaCore.git
cd LumaCore/deploy/docker/http-only
cp .env.example .env
docker-compose up --build
```

After a minute or so, LumaCore is running at http://localhost:5080. The Web UI greets you, and Swagger is available at http://localhost:5080/swagger.

For production deployments with HTTPS, see [Docker Deployment](deployment/docker.md).

## Quick Start with .NET SDK

Make sure you have the .NET 10 SDK installed. You can download it from https://dotnet.microsoft.com/download/dotnet/10.0 or install it via your package manager.

```bash
git clone https://github.com/LumaCoreTech/LumaCore.git
cd LumaCore
dotnet restore
dotnet run --project src/LumaCore.Api
```

LumaCore starts on http://localhost:5080. You'll see log output in the terminal, and Swagger is available at http://localhost:5080/swagger.

For development, you can use `dotnet watch` for hot reload:

```bash
cd src/LumaCore.Api
dotnet watch run
```

## Verify It's Running

Check if LumaCore is healthy:

```bash
curl http://localhost:5080/api/health/live
```

You should see:

```json
{
  "status": "ok"
}
```

## Your First API Call

LumaCore uses JWT authentication. Let's get a token and explore the API.

### Get a Token

The default development credentials are `admin` / `changeme`:

```bash
curl -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "changeme"}'
```

You'll get back an access token:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

### Use the Token

Save the token and use it in the `Authorization` header:

```bash
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5080/api/auth/whoami
```

This tells you who you're authenticated as:

```json
{
  "name": "admin",
  "roles": [
    "admin"
  ],
  "claims": [
    ...
  ]
}
```

### Inspect Your Token

To see the full token details including expiration:

```bash
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5080/api/auth/introspect
```

```json
{
  "subject": "admin",
  "name": "admin",
  "roles": [
    "admin"
  ],
  "notBeforeUtc": "2025-12-06T20:15:14Z",
  "expiresUtc": "2025-12-06T20:20:14Z",
  "expiresIn": "00:04:09",
  "jwtId": "120ad484f92d4839b4fed8aef3311d74",
  "issuer": "LumaCore",
  "audience": "LumaCore-AdminUi"
}
```

## Exploring with Swagger

The easiest way to explore the API is through Swagger UI at http://localhost:5080/swagger.

To authenticate in Swagger:

1. First, call `POST /api/auth/login` with the credentials
2. Copy the `accessToken` from the response
3. Click the "Authorize" button at the top right
4. Paste the token (without "Bearer" prefix)
5. Click "Authorize"

Now you can try all protected endpoints directly in the browser.

## Available Endpoints

**Authentication:**
- `POST /api/auth/login` — Get a JWT token
- `GET /api/auth/whoami` — Who am I? (requires token)
- `GET /api/auth/introspect` — Full token details (requires token)

**Health:**
- `GET /api/health/live` — Liveness check (no auth required)
- `GET /health` — Detailed health with dependencies (no auth required)

## Development Mode

When running with `ASPNETCORE_ENVIRONMENT=Development`, LumaCore enables:

- **Swagger UI** at `/swagger`
- **Detailed error pages** with stack traces
- **Verbose logging** in the console

In Production, these are disabled for security.

## Troubleshooting

**Port 5080 already in use?**

Either stop the other process, or change the port in `src/LumaCore.Api/appsettings.json`:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://localhost:5081" }
    }
  }
}
```

**401 Unauthorized?**

Make sure you:
1. Got a token from `/api/auth/login`
2. Include it as `Authorization: Bearer <token>`
3. The token hasn't expired (default: 60 minutes)

**JWT validation errors?**

Check that `Jwt__SigningKey` in your `.env` (Docker) or `appsettings.json` (.NET) is at least 32 characters.

## What's Next?

Now that LumaCore is running:

**Explore the API**
- Open Swagger at http://localhost:5080/swagger and try out the endpoints
- Check `/api/health/live` and `/health` to understand the health check system

**Deploy to Production**
- [Docker Deployment](deployment/docker.md) — HTTPS setup with Caddy, registry workflows, and security notes
- [Configuration](deployment/configuration.md) — All available settings

**Follow the Progress**
- [Project Status](status.md) — Current implementation status and roadmap

---

**Welcome to LumaCore!** 🎉