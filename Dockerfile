# ==============================================================================
# LumaCore Dockerfile
# ==============================================================================
# This Dockerfile builds the complete LumaCore stack using a multi-stage build:
#   - LumaCore.Api (backend)
#   - LumaCore.Ui.Web (Blazor WebAssembly frontend)
#   - LumaCore.HealthCheck (native health check tool)
#
# Features:
#   - Multi-stage build (SDK for build, ASP.NET runtime for execution)
#   - Non-root user (built-in 'app' user) for security
#   - Native .NET health check tool (no curl/wget dependency)
#   - Optimized layer caching for faster rebuilds
#   - Single container serves both API and SPA
#
# Build:   docker build -t lumacore:latest .
# Run:     docker run -p 5080:5080 lumacore:latest
# ==============================================================================

# ------------------------------------------------------------------------------
# Stage 1: Build
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy solution and project files first (for better layer caching)
COPY ["LumaCore.sln", "./"]
COPY ["global.json", "./"]
COPY ["version.json", "./"]
COPY ["src/Directory.Build.props", "src/"]
COPY ["src/Directory.Build.targets", "src/"]
COPY ["src/LumaCore.Core/LumaCore.Core.csproj", "src/LumaCore.Core/"]
COPY ["src/LumaCore.Api/LumaCore.Api.csproj", "src/LumaCore.Api/"]
COPY ["src/LumaCore.HealthCheck/LumaCore.HealthCheck.csproj", "src/LumaCore.HealthCheck/"]
COPY ["src/LumaCore.Ui.Web/LumaCore.Ui.Web.csproj", "src/LumaCore.Ui.Web/"]

# Restore all dependencies (cached unless project files change)
RUN dotnet restore "LumaCore.sln"

# Copy all source code
COPY . .

# Extract version using Nerdbank.GitVersioning (requires .git folder)
# Falls back to 0.0.0-local if .git is missing (e.g., ZIP download)
RUN dotnet tool install -g nbgv && \
    (/root/.dotnet/tools/nbgv get-version -v NuGetPackageVersion > /tmp/version.txt || echo "0.0.0-local" > /tmp/version.txt)

# Build and publish the API project (without embedded UI - we copy it separately)
# SkipArtifactNaming bypasses the versioned publish path from Directory.Build.targets
WORKDIR "/src/src/LumaCore.Api"
RUN dotnet publish "LumaCore.Api.csproj" \
    -c Release \
    -o /app/api \
    --no-restore \
    /p:UseAppHost=false \
    /p:IncludeBlazorUi=false \
    /p:SkipArtifactNaming=true

# Build and publish the health check tool
WORKDIR "/src/src/LumaCore.HealthCheck"
RUN dotnet publish "LumaCore.HealthCheck.csproj" \
    -c Release \
    -o /app/healthcheck \
    --no-restore \
    /p:UseAppHost=false \
    /p:SkipArtifactNaming=true

# Build and publish the Blazor WebAssembly UI
WORKDIR "/src/src/LumaCore.Ui.Web"
RUN dotnet publish "LumaCore.Ui.Web.csproj" \
    -c Release \
    -o /app/ui \
    --no-restore \
    /p:SkipArtifactNaming=true

# ------------------------------------------------------------------------------
# Stage 2: Runtime
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/api .

# Copy version file to image root
COPY --from=build /tmp/version.txt /lumacore.version

# Copy Blazor WebAssembly UI into API's wwwroot
# This enables the API to serve the SPA via UseBlazorFrameworkFiles() and UseStaticFiles()
COPY --from=build /app/ui/wwwroot ./wwwroot

# Copy health check tool to separate directory
COPY --from=build /app/healthcheck /app/healthcheck

# Create directories for data and certificates with proper ownership
# Use the built-in 'app' user (UID 1654, available since .NET 8)
RUN mkdir -p /app/data /app/certs && \
    chown -R app:app /app

# Switch to non-root user
USER app

# Expose HTTP port (default: 5080)
# For HTTPS, mount certificates and configure via environment variables
EXPOSE 5080

# Health check using native .NET tool (no curl/wget required)
# Uses the LumaCore.HealthCheck tool which returns exit code 0 for healthy, 1 for unhealthy
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD ["dotnet", "/app/healthcheck/LumaCore.HealthCheck.dll"]

# Set ASP.NET Core environment and URL binding
# Note: Since .NET 8, the default port is 8080. We explicitly bind to 5080.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5080

# Container metadata (OCI standard labels)
LABEL org.opencontainers.image.title="LumaCore"
LABEL org.opencontainers.image.description="AI persona framework for hosting local AI companions"
LABEL org.opencontainers.image.vendor="LumaCoreTech"
LABEL org.opencontainers.image.source="https://github.com/LumaCoreTech/LumaCore"
LABEL org.opencontainers.image.licenses="MIT"

# Run the application
ENTRYPOINT ["dotnet", "LumaCore.Api.dll"]

# ==============================================================================
# Usage Examples:
# ==============================================================================
#
# Basic HTTP (development):
#   docker run -p 5080:5080 lumacore:latest
#
# With environment variables:
#   docker run \
#     -p 5080:5080 \
#     -e Jwt__SigningKey="your-secret-key-min-32-characters!" \
#     lumacore:latest
#
# With mounted configuration:
#   docker run \
#     -p 5080:5080 \
#     -v $(pwd)/appsettings.Production.json:/app/appsettings.Production.json:ro \
#     lumacore:latest
#
# With HTTPS and certificates:
#   docker run \
#     -p 5443:5443 \
#     -v $(pwd)/certs:/app/certs:ro \
#     -e Https__Certificate__Path=/app/certs/certificate.pfx \
#     -e Https__Certificate__Password="cert-password" \
#     lumacore:latest
#
# ==============================================================================
