# Docker Deployment

LumaCore can be deployed in two ways, depending on where it needs to run. Both use Docker Compose for simplicity.

For detailed configuration options, troubleshooting, and more, see [docs/deployment/docker.md](../../docs/deployment/docker.md).

## Local or Trusted Networks

For development on your own machine or in a trusted network where encryption isn't required, the **[`http-only`](./http-only)** setup is all you need. No TLS, no certificates — just start it up:

```bash
cd deploy/docker/http-only
cp .env.example .env
docker-compose up --build            # Watch output, Ctrl+C to stop
docker-compose up --build -d         # Run in background, check logs with: docker-compose logs -f
```

After a few seconds, LumaCore is running at http://localhost:5080. The `.env.example` contains sensible defaults for development — Swagger is enabled, logging is verbose.

This setup also works when your infrastructure already handles TLS — like a load balancer or Kubernetes ingress. LumaCore speaks HTTP internally while the upstream service takes care of encryption.

For production use, make sure to set `ASPNETCORE_ENVIRONMENT=Production` in your `.env` to disable Swagger and verbose logging.

## Public Servers with HTTPS

For internet-facing servers, there's the **[`https-acme`](./https-acme)** setup. It uses Caddy as a reverse proxy. The magic: Caddy automatically obtains TLS certificates from Let's Encrypt and renews them on its own. No Certbot, no cronjobs.

What you need:
- A domain pointing to your server
- Ports 80 and 443 open to the internet

```bash
cd deploy/docker/https-acme
cp .env.example .env
# Edit .env: set your DOMAIN and generate a secure JWT key
docker-compose up --build            # Watch output, Ctrl+C to stop
docker-compose up --build -d         # Run in background, check logs with: docker-compose logs -f
```

That's it. Open https://your-domain.com and you're done. Caddy handles certificate issuance, renewal, and HTTPS redirection automatically.

To generate a secure JWT key:

```bash
openssl rand -base64 32
```

Copy the result into your `.env` as `Jwt__SigningKey`.

## Stopping

To stop and remove the containers:

```bash
docker-compose down
```
