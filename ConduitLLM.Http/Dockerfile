# =============================================================================
# ConduitLLM HTTP API Service Dockerfile
# =============================================================================
# High-performance, secure, multi-stage Docker build for the Conduit HTTP API
# Optimized for production deployments with minimal attack surface
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Dependencies Cache
# -----------------------------------------------------------------------------
# Separate stage for NuGet restore to maximize Docker layer caching
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dependencies

WORKDIR /src

# Copy only project files first for optimal caching
COPY ConduitLLM.Http/*.csproj ./ConduitLLM.Http/
COPY ConduitLLM.Core/*.csproj ./ConduitLLM.Core/
COPY ConduitLLM.Configuration/*.csproj ./ConduitLLM.Configuration/
COPY ConduitLLM.Providers/*.csproj ./ConduitLLM.Providers/
COPY ConduitLLM.Admin/*.csproj ./ConduitLLM.Admin/
COPY ConduitLLM.Security/*.csproj ./ConduitLLM.Security/
COPY ConduitLLM.WebUI/*.csproj ./ConduitLLM.WebUI/

# Handle optional projects gracefully
COPY ConduitLLM.Common/*.csproj ./ConduitLLM.Common/ 2>/dev/null || true

# Restore dependencies with retry logic for network resilience
RUN dotnet restore "ConduitLLM.Http/ConduitLLM.Http.csproj" \
    --runtime linux-musl-x64 \
    --locked-mode \
    || dotnet restore "ConduitLLM.Http/ConduitLLM.Http.csproj" \
    --runtime linux-musl-x64

# -----------------------------------------------------------------------------
# Stage 2: Build
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

WORKDIR /src

# Copy restored packages from dependencies stage
COPY --from=dependencies /src ./
COPY --from=dependencies /root/.nuget /root/.nuget

# Copy source code
COPY . .

# Build with optimizations
RUN dotnet publish "ConduitLLM.Http/ConduitLLM.Http.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --runtime linux-musl-x64 \
    --self-contained false \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:PublishReadyToRun=true

# -----------------------------------------------------------------------------
# Stage 3: Runtime
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime

# Install required packages and create non-root user in a single layer
RUN apk add --no-cache \
        curl \
        ca-certificates \
        tzdata \
    && addgroup -g 1000 -S conduit \
    && adduser -u 1000 -S conduit -G conduit \
    && mkdir -p /app /data \
    && chown -R conduit:conduit /app /data

# Set timezone to UTC
ENV TZ=UTC

WORKDIR /app

# Copy published application
COPY --from=build --chown=conduit:conduit /app/publish .

# Configure ASP.NET Core
ENV ASPNETCORE_URLS="http://+:8080" \
    ASPNETCORE_ENVIRONMENT="Production" \
    DOTNET_RUNNING_IN_CONTAINER="true" \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="false" \
    # Performance optimizations
    DOTNET_TieredPGO="1" \
    DOTNET_TC_QuickJitForLoops="1" \
    DOTNET_ReadyToRun="0"

# Switch to non-root user
USER conduit

# Expose port
EXPOSE 8080

# Health check with proper timeouts and intervals
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health/ready || exit 1

# Use exec form to ensure proper signal handling
ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]

# =============================================================================
# Build Arguments & Labels
# =============================================================================
ARG BUILD_DATE
ARG VERSION
ARG VCS_REF

LABEL org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.title="Conduit HTTP API" \
      org.opencontainers.image.description="High-performance LLM gateway HTTP API service" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.vendor="KNN Labs" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.source="https://github.com/knnlabs/Conduit" \
      maintainer="KNN Labs <support@knnlabs.com>"