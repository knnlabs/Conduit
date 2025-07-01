# =============================================================================
# ConduitLLM Admin API Service Dockerfile
# =============================================================================
# Secure, optimized multi-stage build for the Conduit Admin API
# Features enhanced security, minimal attack surface, and production readiness
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Dependencies Cache
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dependencies

WORKDIR /src

# Copy project files for optimal layer caching
COPY ConduitLLM.Admin/*.csproj ./ConduitLLM.Admin/
COPY ConduitLLM.Configuration/*.csproj ./ConduitLLM.Configuration/
COPY ConduitLLM.Core/*.csproj ./ConduitLLM.Core/
COPY ConduitLLM.Providers/*.csproj ./ConduitLLM.Providers/
COPY ConduitLLM.Security/*.csproj ./ConduitLLM.Security/

# Restore with locked mode for reproducible builds
RUN dotnet restore "ConduitLLM.Admin/ConduitLLM.Admin.csproj" \
    --runtime linux-musl-x64 \
    --locked-mode \
    || dotnet restore "ConduitLLM.Admin/ConduitLLM.Admin.csproj" \
    --runtime linux-musl-x64

# -----------------------------------------------------------------------------
# Stage 2: Build
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

WORKDIR /src

# Copy cached dependencies
COPY --from=dependencies /src ./
COPY --from=dependencies /root/.nuget /root/.nuget

# Copy source code
COPY . .

# Build and publish with optimizations
WORKDIR /src/ConduitLLM.Admin
RUN dotnet publish "ConduitLLM.Admin.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --runtime linux-musl-x64 \
    --self-contained false \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:PublishReadyToRun=true \
    -p:DebugType=None \
    -p:DebugSymbols=false

# -----------------------------------------------------------------------------
# Stage 3: Runtime
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime

# Install packages and setup user in single layer
RUN apk add --no-cache \
        curl \
        ca-certificates \
        tzdata \
    && addgroup -g 1000 -S conduit \
    && adduser -u 1000 -S conduit -G conduit \
    && mkdir -p /app /data /logs \
    && chown -R conduit:conduit /app /data /logs

# Security: Remove unnecessary packages
RUN apk del --no-cache apk-tools

ENV TZ=UTC

WORKDIR /app

# Copy application with proper ownership
COPY --from=build --chown=conduit:conduit /app/publish .

# Configure runtime
ENV ASPNETCORE_URLS="http://+:8080" \
    ASPNETCORE_ENVIRONMENT="Production" \
    DOTNET_RUNNING_IN_CONTAINER="true" \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="false" \
    # Security: Disable .NET telemetry
    DOTNET_CLI_TELEMETRY_OPTOUT="1" \
    # Performance
    DOTNET_TieredPGO="1" \
    DOTNET_TC_QuickJitForLoops="1" \
    DOTNET_ReadyToRun="0" \
    # Admin-specific defaults
    CONDUIT_ADMIN_LOG_PATH="/logs" \
    CONDUIT_ADMIN_DATA_PATH="/data"

# Security: Run as non-root
USER conduit

# Only expose the port we actually use
EXPOSE 8080

# Health check for container orchestration
HEALTHCHECK --interval=30s --timeout=5s --start-period=45s --retries=3 \
    CMD curl -f http://localhost:8080/health/ready || exit 1

# Proper signal handling with exec form
ENTRYPOINT ["dotnet", "ConduitLLM.Admin.dll"]

# =============================================================================
# Metadata
# =============================================================================
ARG BUILD_DATE
ARG VERSION
ARG VCS_REF

LABEL org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.title="Conduit Admin API" \
      org.opencontainers.image.description="Administrative API for Conduit LLM Gateway" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.vendor="KNN Labs" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.source="https://github.com/knnlabs/Conduit" \
      org.opencontainers.image.documentation="https://github.com/knnlabs/Conduit/tree/main/ConduitLLM.Admin" \
      maintainer="KNN Labs <support@knnlabs.com>"