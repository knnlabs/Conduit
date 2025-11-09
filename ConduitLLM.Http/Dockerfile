# HTTP API Dockerfile - Optimized for selective cache invalidation
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.2 AS build
WORKDIR /src

# ===== CACHED LAYERS (Keep these fast) =====
# Copy project files for dependency resolution
COPY *.sln .
COPY Directory.Build.props ./ 
COPY */*.csproj ./
RUN find . -name "*.csproj" -exec dirname {} \; | xargs -I {} mkdir -p {}

# Copy project files to correct locations
COPY ConduitLLM.Http/*.csproj ./ConduitLLM.Http/
COPY ConduitLLM.Configuration/*.csproj ./ConduitLLM.Configuration/
COPY ConduitLLM.Core/*.csproj ./ConduitLLM.Core/
COPY ConduitLLM.Security/*.csproj ./ConduitLLM.Security/
COPY ConduitLLM.Providers/*.csproj ./ConduitLLM.Providers/
COPY ConduitLLM.Admin/*.csproj ./ConduitLLM.Admin/
COPY Tests/ConduitLLM.Tests/*.csproj ./Tests/ConduitLLM.Tests/
COPY Tests/ConduitLLM.IntegrationTests/*.csproj ./Tests/ConduitLLM.IntegrationTests/
COPY Tests/ConduitLLM.Benchmarks/*.csproj ./Tests/ConduitLLM.Benchmarks/

# Restore dependencies (cached unless project files change)
RUN dotnet restore "ConduitLLM.Http/ConduitLLM.Http.csproj"

# ===== CACHE INVALIDATION POINT =====
# This argument forces rebuild from here down when .NET code changes
ARG CACHEBUST=1

# Copy all source code (this and everything after will rebuild)
COPY . .

# Build and publish
RUN dotnet publish "ConduitLLM.Http/ConduitLLM.Http.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage - Debian-based for reliability
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-rc.2
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Use the existing non-root user (ubuntu with UID 1000 in .NET 10.0 images)
RUN chown -R 1000:1000 /app
USER 1000

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]