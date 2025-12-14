# Deployment Documentation

This section covers deploying LumaCore to production environments.

---

## Available Guides

### [Docker Deployment](docker.md)

Complete guide to running LumaCore with Docker. Covers:

- Docker Compose setups (HTTP-only and HTTPS with Caddy)
- Environment variable configuration
- Health checks and monitoring
- Security best practices

**Start here** for any production deployment.

### [Configuration Reference](configuration.md)

All configuration options for LumaCore. Covers:

- Environment variables
- appsettings.json structure
- JWT settings
- Feature-specific configuration

**Reference document** — consult when configuring specific features.

---

## Quick Start

```bash
cd deploy/docker/http-only
cp .env.example .env
# Edit .env with your settings
docker-compose up -d
```

For HTTPS with automatic certificates, use `deploy/docker/https-acme/` instead.

See [Docker Deployment](docker.md) for details.

---

## Related Documentation

- [Getting Started](../getting-started.md) — Local development setup
- [Architecture](../architecture/README.md) — Understanding LumaCore's design

---

© 2025 LumaCoreTech • MIT License
