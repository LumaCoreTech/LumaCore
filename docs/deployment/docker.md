# Docker Deployment

LumaCore runs beautifully in Docker — one container with everything included: API, Web UI, and health checks. This guide walks you through from the first command to a running system.

## Deployment Options

LumaCore provides two ready-to-use Docker Compose setups in the [`deploy/docker`](../../deploy/docker) folder. Which one you need depends on where LumaCore will run.

> [!NOTE]
> The examples use `docker-compose` (V1 syntax). If you have Docker Compose V2 installed as a plugin, use `docker compose` (without hyphen) instead. Both work identically.

The **[`http-only`](../../deploy/docker/http-only)** setup is for local development, trusted networks, or situations where your infrastructure already handles TLS (like a load balancer or Kubernetes ingress). It's simple: just LumaCore, no extras.

The **[`https-acme`](../../deploy/docker/https-acme)** setup is for public servers. It includes Caddy as a reverse proxy, which automatically obtains and renews TLS certificates from Let's Encrypt. No manual certificate management required.

## Quick Start (HTTP)

For local development or trusted networks, the `http-only` setup gets you running in seconds:

```bash
cd deploy/docker/http-only
cp .env.example .env
docker-compose up --build            # Watch output, Ctrl+C to stop
docker-compose up --build -d         # Run in background
```

After the build completes, open http://localhost:5080. You'll see the LumaCore Web UI. The `.env.example` contains sensible defaults for development — Swagger is enabled at `/swagger`, and logging is verbose so you can see what's happening.

If you started with `-d`, the container runs in the background. Check the logs with `docker-compose logs -f`.

## Production with HTTPS

For internet-facing servers, use the `https-acme` setup. It adds Caddy as a reverse proxy in front of LumaCore. Caddy handles TLS automatically — it obtains certificates from Let's Encrypt when you first start up, and renews them before they expire. No Certbot, no cronjobs, no manual intervention.

Before you start, make sure you have a domain name pointing to your server, and that ports 80 and 443 are open to the internet. Caddy needs both: port 80 for the ACME challenge, port 443 for HTTPS.

```bash
cd deploy/docker/https-acme
cp .env.example .env
```

Now edit the `.env` file. You need to set two things: your domain and a secure JWT signing key.

```bash
DOMAIN=lumacore.example.com
Jwt__SigningKey=your-generated-key-here
```

Generate a secure key with:

```bash
openssl rand -base64 32
```

Copy the output into your `.env` as `Jwt__SigningKey`.

Then start it up:

```bash
docker-compose up --build            # Watch output, Ctrl+C to stop
docker-compose up --build -d         # Run in background
```

Give it a minute for the initial certificate issuance, then open https://your-domain.com. That's it — you have a production-ready LumaCore with automatic HTTPS.

If you're planning to run multiple services behind Caddy, take a look at [caddy-docker-proxy](https://github.com/lucaslorentz/caddy-docker-proxy) which configures routing via Docker labels instead of a Caddyfile.

## Configuration via .env

All application settings live in the `.env` file. Docker Compose reads this file and passes every variable to the container as an environment variable. This means you can configure LumaCore without touching any code or config files inside the container.

LumaCore uses ASP.NET Core's configuration syntax. Settings are named `Section__Key`, where the double underscore represents nesting levels from `appsettings.json`. For example, `Jwt__SigningKey` corresponds to:

```json
{
  "Jwt": {
    "SigningKey": "..."
  }
}
```

The most important setting is `ASPNETCORE_ENVIRONMENT`. Set it to `Development` for local work (verbose logging, Swagger enabled) or `Production` for public deployments (log level Warning, Swagger disabled).

```bash
ASPNETCORE_ENVIRONMENT=Production
```

The JWT signing key is required. For development, the default in `.env.example` works fine. For production, generate a secure one — at least 32 characters.

> [!CAUTION]
> Always generate your own JWT signing key for production. Never use the default key from `.env.example` — it's public and offers no security.

```bash
Jwt__SigningKey=your-generated-key-here
```

Logging can be adjusted if you need more or less output:

```bash
Logging__LogLevel__Default=Information
```

The beauty of this approach is that any setting from `appsettings.json` can be overridden in `.env`. Just translate the JSON path to the `Section__Key` format.

## Keeping Images Up to Date

The .NET base images receive regular security updates from Microsoft. To make sure you're running the latest patched version, use the `--pull` flag:

```bash
docker-compose up --build --pull
```

This tells Docker to check for newer versions of the base images before building. Without `--pull`, Docker uses whatever version is cached locally, which might be months old.

In production, you should do this regularly — ideally as part of your deployment process or on a schedule. Security patches in the base image protect against vulnerabilities in the .NET runtime and underlying OS libraries.

## Health Checks

Docker automatically monitors whether LumaCore is still alive. Every 30 seconds, it calls the `/api/v1/health/live` endpoint. If this check fails three times in a row, Docker marks the container as "unhealthy".

Important: Docker only *marks* the container — it doesn't automatically restart it. Automatic restarts require additional tooling like Docker Swarm, Kubernetes, or a watchdog service. The `restart: unless-stopped` policy only kicks in when the process actually crashes, not when health checks fail.

You can see the health status with:

```bash
docker ps
```

The STATUS column shows something like `Up 5 minutes (healthy)` or `Up 2 minutes (unhealthy)`.

LumaCore exposes two health endpoints for different purposes. The `/api/v1/health/live` endpoint is a simple liveness check — it just confirms the process is running and can respond to HTTP requests. It's fast and has no dependencies.

The `/health` endpoint is a readiness check. It verifies that all registered services are healthy, including any database connections or external dependencies. This is more thorough but slower.

We use the liveness check for Docker's HEALTHCHECK intentionally. If the liveness check fails repeatedly, something is seriously wrong with the process itself. Use the `/health` endpoint for load balancer health checks or monitoring dashboards where you want the full picture including dependencies.

## Without Docker Compose

If you prefer not to use Docker Compose, you can run the container directly with `docker run`. This is useful for minimal setups or when integrating with other orchestration tools.

First, build the image from the repository root:

```bash
docker build -t lumacore:latest .
```

> [!NOTE]
> The Docker build requires a Git clone, not a ZIP download. *MinVer* extracts the version from Git tags and history. Without `.git`, the version falls back to `0.0.0-ci.0`.

Then run it with the minimum required configuration:

```bash
docker run -d \
  --name lumacore \
  -p 5080:5080 \
  -e Kestrel__Endpoints__Http__Url=http://+:5080 \
  -e Jwt__SigningKey="your-secret-key-min-32-characters!" \
  lumacore:latest
```

Without Docker Compose, you pass configuration as `-e` flags instead of using a `.env` file. The syntax is the same: `Section__Key=value`. The Kestrel binding to `http://+:5080` is important — it tells the server to listen on all network interfaces, not just localhost.

## Resource Limits

For production deployments, it's a good idea to limit how much memory the container can use. This prevents a memory leak or runaway process from affecting other services on the same host.

Create a `docker-compose.override.yml` file next to your `docker-compose.yml` (e.g., in `deploy/docker/http-only/`):

```yaml
services:
  lumacore:
    deploy:
      resources:
        limits:
          memory: 512M
```

Docker Compose automatically merges this with the base file. The container will be killed if it tries to use more than 512 MB of memory. For most LumaCore deployments, 512 MB is plenty — adjust based on your workload.

## Pushing to a Registry

For team deployments or CI/CD pipelines, you might want to build the image once and push it to a registry. This way, your servers pull a pre-built image instead of building from source.

After building the image (see above), extract the version and tag it:

```bash
VERSION=$(docker run --rm --entrypoint cat lumacore:latest /lumacore.version)
docker tag lumacore:latest lumacore:$VERSION
```

Push it to your registry. The registry URL varies depending on your provider:

```bash
# Docker Hub (no prefix needed)
docker tag lumacore:$VERSION your-username/lumacore:$VERSION
docker push your-username/lumacore:$VERSION

# Other registries (GitHub, Azure ACR, AWS ECR, Harbor, etc.)
docker tag lumacore:$VERSION your-registry.example.com/lumacore:$VERSION
docker push your-registry.example.com/lumacore:$VERSION
```

Then on your server, edit your `docker-compose.yml` (in `deploy/docker/http-only/` or `https-acme/`) to use the pre-built image instead of building locally:

```yaml
services:
  lumacore:
    image: your-registry.example.com/lumacore:x.y.z  # Your registry and version
    # build:
    #   context: ../../..
    #   dockerfile: Dockerfile
```

This speeds up deployments significantly — pulling an image is much faster than building one.

## Troubleshooting

**Container won't start?** The first thing to check is the logs:

```bash
docker-compose logs
```

The most common cause is a JWT signing key that's too short. It needs at least 32 characters. Another frequent issue is syntax errors in the `.env` file — make sure there are no spaces around the `=` sign.

**Build seems outdated?** Docker aggressively caches build layers. If you've made changes that aren't showing up, force a complete rebuild:

```bash
docker-compose build --no-cache
docker-compose up
```

This ignores all cached layers and builds everything from scratch.

**Can't connect from the browser?** First, verify the container is actually running and the port mapping is correct:

```bash
docker ps
```

You should see `0.0.0.0:5080->5080/tcp` in the PORTS column. If it shows `127.0.0.1:5080`, the container is only accessible from the Docker host itself, not from other machines.

**Health check failing?** Check if the endpoint actually responds:

```bash
curl http://localhost:5080/api/v1/health/live
```

If this works but Docker still shows unhealthy, the application might need more time to start. The health check has a start period of 10 seconds by default — it waits that long before the first check. For larger deployments, you might need to increase this in the Dockerfile.

**Need to poke around inside the container?** You can get a shell:

```bash
docker exec -it lumacore bash
```

This drops you into a bash session inside the running container, useful for checking files, environment variables, or running diagnostic commands.

## Security Notes

A few things to keep in mind when running LumaCore in production.

> [!WARNING]
> Do not expose the `http-only` setup directly to the public internet. It has no TLS encryption — use `https-acme` or put it behind a reverse proxy that handles TLS.

The `http-only` setup is meant for local development or trusted internal networks only. For anything exposed to the public internet, use the `https-acme` setup or another reverse proxy with TLS.

Never bake secrets into Docker images. Anyone who can pull the image can extract them. Use the `.env` file for secrets, or better yet, a proper secrets manager like Docker Secrets, HashiCorp Vault, or your cloud provider's secret management service.

Use specific version tags for images from registries. Avoid `:latest` when pulling from Docker Hub or other registries — it's convenient but makes deployments unpredictable. When something breaks, you want to know exactly which version is running. For your own builds, tag with version numbers like `lumacore:1.0.0` before pushing to a registry.

Keep base images updated. Run `docker-compose up --build --pull` regularly to get security patches. The .NET team releases updates monthly, and critical security fixes come out as needed.

The container runs as a non-root user. LumaCore uses the built-in `app` user (UID 1654) that comes with the .NET base image. This limits the damage if the application is ever compromised.

Scan your images for vulnerabilities. Tools like `docker scout` (built into Docker Desktop) or Trivy can identify known security issues in your image's dependencies. Run these scans as part of your CI/CD pipeline.

## Image Details

For reference, here's what's inside the LumaCore Docker image:

The base image is `mcr.microsoft.com/dotnet/aspnet:10.0`, Microsoft's official ASP.NET runtime image. It's based on Debian and includes just enough to run .NET applications — no SDK, no extra tools.

The application runs as the `app` user with UID 1654. This is a non-root user that comes pre-configured in the .NET base images since .NET 8.

The working directory is `/app`, where all application files live. The container exposes port 5080 for HTTP traffic. The container HEALTHCHECK uses the `LumaCore.HealthCheck` tool, which in turn calls `/api/v1/health/live` every 30 seconds.

The image also contains `/lumacore.version` with the version string — useful for tagging when pushing to a registry.

## Next Steps

Now that LumaCore is running, you might want to explore:

- [Configuration Reference](configuration.md) — All available settings explained
- [Getting Started](../getting-started.md) — Learn how to use the API
