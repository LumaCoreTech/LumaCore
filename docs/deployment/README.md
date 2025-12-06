# Deployment Documentation

This directory contains guides for deploying LumaCore to production environments.

## Quick Start

1. **Build your Docker image** - [Docker Deployment Guide](docker.md)
2. **Choose your deployment option:**
   - **Cloud Platforms** (Azure/AWS/GCP) → [Cloud Platform Deployments](cloud-platforms.md)
   - **Self-Managed Server** (Nginx/Traefik/Caddy) → [Reverse Proxy Guide](reverse-proxy.md)
   - **Simple VM** (Direct HTTPS) → [Reverse Proxy Guide](reverse-proxy.md#option-1-direct-https-kestrel)

## Documents

### [docker.md](docker.md)
Complete guide to containerizing LumaCore with Docker. Covers:
- Building production-ready Docker images
- Docker Compose for local development
- Environment variable configuration
- Health checks and monitoring
- Security best practices

**Start here if:** You need to create a Docker image for deployment.

### [cloud-platforms.md](cloud-platforms.md)
Step-by-step deployment guides for cloud platforms with managed infrastructure. Covers:
- Azure App Service (complete workflow)
- AWS Elastic Beanstalk (complete workflow)
- Google Cloud Run (complete workflow)
- Custom domain setup
- Platform comparison
- Troubleshooting

**Start here if:** You're deploying to Azure, AWS, or Google Cloud.

### [reverse-proxy.md](reverse-proxy.md)
Comprehensive guide to HTTPS and reverse proxy deployment. Covers:
- Three deployment options explained
- Direct HTTPS with Kestrel
- Self-managed reverse proxies (Nginx, Traefik, Caddy)
- Forwarded headers configuration
- Certificate management (PFX vs PEM, Let's Encrypt)
- Security best practices

**Start here if:** You're running your own server/VPS or need to understand reverse proxy concepts.

### Reverse Proxy Guides

**Proxy-specific configuration guides:**

#### [nginx-guide.md](nginx-guide.md) 🚧
Complete Nginx configuration guide (coming soon). Will cover:
- Full server block configuration
- SSL/TLS setup with Let's Encrypt
- Path-based routing and load balancing

**Status:** Placeholder with basic configuration available now.

#### [traefik-guide.md](traefik-guide.md) 🚧
Complete Traefik configuration guide (coming soon). Will cover:
- File-based and Docker label configuration
- Automatic HTTPS with Let's Encrypt
- Middleware and routing

**Status:** Placeholder with basic configuration available now.

#### [caddy-guide.md](caddy-guide.md) 🚧
Complete Caddy configuration guide (coming soon). Will cover:
- Caddyfile configuration
- Automatic HTTPS (built-in!)
- Load balancing and reverse proxy

**Status:** Placeholder with basic configuration available now.

### [configuration.md](configuration.md)
Configuration reference for production deployments. Covers:
- Environment variables
- appsettings.json structure
- JWT configuration
- Database connections
- Logging configuration

**Reference document** - consult when configuring specific features.

## Decision Tree

Not sure which guide to follow? Use this decision tree:

```
┌─ Do you have a Docker image ready?
│  ├─ No → Start with docker.md
│  └─ Yes ↓
│
├─ Where are you deploying?
│  ├─ Azure App Service → cloud-platforms.md#azure-app-service
│  ├─ AWS Elastic Beanstalk → cloud-platforms.md#aws-elastic-beanstalk
│  ├─ Google Cloud Run → cloud-platforms.md#google-cloud-run
│  ├─ Kubernetes → (Coming soon: kubernetes.md)
│  └─ Your own server/VPS ↓
│
├─ Do you need advanced features?
│  │  (Multiple services, rate limiting, caching, load balancing)
│  │
│  ├─ Yes → reverse-proxy.md#option-3-self-managed-reverse-proxy
│  └─ No → reverse-proxy.md#option-1-direct-https-kestrel
```

## Common Deployment Scenarios

### Scenario 1: Startup MVP (Cloud)
**Goal:** Get to production fast with minimal cost

**Path:**
1. Build Docker image ([docker.md](docker.md))
2. Deploy to Google Cloud Run ([cloud-platforms.md](cloud-platforms.md#google-cloud-run))
3. Add custom domain

**Why:** Google Cloud Run scales to zero (low cost), automatic HTTPS, simplest deployment.

### Scenario 2: Enterprise (Cloud)
**Goal:** Robust, scalable, integrated with existing Azure/AWS infrastructure

**Path:**
1. Build Docker image ([docker.md](docker.md))
2. Deploy to Azure App Service or AWS Elastic Beanstalk ([cloud-platforms.md](cloud-platforms.md))
3. Configure auto-scaling
4. Set up monitoring (Application Insights / CloudWatch)

**Why:** Enterprise features, tight integration with existing cloud services.

### Scenario 3: Self-Hosted (On-Premise)
**Goal:** Full control, data sovereignty, run on own hardware

**Path:**
1. Build Docker image ([docker.md](docker.md))
2. Set up Nginx reverse proxy ([reverse-proxy.md](reverse-proxy.md#option-3-self-managed-reverse-proxy))
3. Configure Let's Encrypt for certificates
4. Set up monitoring (Prometheus + Grafana)

**Why:** Complete control, no cloud vendor lock-in, data stays on-premise.

## Prerequisites

Before deploying LumaCore, ensure you have:

**Required:**
- ✅ Docker installed (for building images)
- ✅ Valid domain name (for HTTPS)
- ✅ Basic command-line knowledge

**Platform-specific:**
- **Azure:** Azure CLI (`az`), active subscription
- **AWS:** AWS CLI (`aws`) + EB CLI (`eb`), active account
- **Google Cloud:** gcloud SDK, active project
- **Self-hosted:** Server with root access, Nginx/Traefik/Caddy installed

## Support

For issues or questions:
- Review [Troubleshooting](cloud-platforms.md#troubleshooting) sections
- Check [GitHub Issues](https://github.com/LumaCoreTech/LumaCore/issues)
- See main [Getting Started](../getting-started.md) guide

## Related Documentation

- [Getting Started](../getting-started.md) - API overview and local development
- [Architecture](../architecture/README.md) - Understanding LumaCore's design
- [Feature Patterns](../architecture/feature-pattern.md) - Code organization principles
