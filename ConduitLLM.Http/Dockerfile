# HTTP API Dockerfile - Simplified for reliability
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything - simple and works
COPY . .

# Simple restore without problematic flags
RUN dotnet restore "ConduitLLM.Http/ConduitLLM.Http.csproj"

# Build and publish
RUN dotnet publish "ConduitLLM.Http/ConduitLLM.Http.csproj" \
    -c Release \
    -o /app/publish

# Runtime stage - Debian-based for reliability
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]