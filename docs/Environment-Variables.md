# ConduitLLM Environment Variables

This document provides comprehensive information about all environment variables used by ConduitLLM. Environment variables are particularly important when deploying the application in containerized environments like Docker.

## Table of Contents

- [Core Application Variables](#core-application-variables)
- [Cache Configuration](#cache-configuration)
- [Security Configuration](#security-configuration)
- [Database](#database)
- [Docker Configuration Examples](#docker-configuration-examples)

## Core Application Variables

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_MASTER_KEY` | String | *Generated on first start if you use ./start.sh* | The master key used for administrative operations and API endpoint security. This value should be kept secure. |
| `ASPNETCORE_ENVIRONMENT` | String | `Production` | Controls the .NET runtime environment. Set to `Development` for detailed error information or `Production` for optimized performance. |
| `ASPNETCORE_URLS` | String | `http://+:80` | The URL(s) on which the application will listen for HTTP requests inside the container. Use `http://+:80` for Docker. |
| `CONDUIT_API_BASE_URL` | String | `null` | Specifies the public base URL where the Conduit API and WebUI are accessible, especially when deployed behind a reverse proxy (e.g., `https://conduit.yourdomain.com`). This is used for generating correct absolute URLs. |

## Cache Configuration

The cache can be configured through environment variables to optimize application performance:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES` | Integer | 60 | The default absolute expiration time for cached items in minutes. After this time has elapsed, the cached item will be removed regardless of access patterns. Set to 0 to disable default absolute expiration. |
| `CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES` | Integer | 20 | The default sliding expiration time for cached items in minutes. If the cached item is not accessed within this time period, it will be removed. Set to 0 to disable default sliding expiration. |
| `CONDUIT_CACHE_USE_DEFAULT_EXPIRATION` | Boolean | true | Controls whether default expiration times are applied to cached items when not explicitly specified. If set to false, cached items will not expire automatically unless expiration is explicitly set when caching an item. |

For more detailed information about cache configuration, see the [Cache Configuration](./Cache-Configuration.md) document.

## Security Configuration

ConduitLLM provides environment variables for configuring security-related aspects:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_MASTER_KEY` | String | *Generated on first start if you use ./start.sh* | Master key for administrative operations. Required for certain API endpoints. |
| `CONDUIT_ENABLE_HTTPS_REDIRECTION` | Boolean | `true` | When true, HTTP requests are redirected to HTTPS. **Set to `false` when running behind a reverse proxy that handles HTTPS termination.** |
| `CONDUIT_CORS_ORIGINS` | String | `*` | Comma-separated list of allowed origins for CORS. Use `*` to allow all origins (not recommended for production). |

## Database

| Variable           | Description                                           | Example Value                |
|--------------------|-------------------------------------------------------|------------------------------|
| `CONDUIT_DB_PATH`  | Path or connection string to the database             | `/data/conduit.db` or `Host=localhost;Port=5432;Database=conduit;Username=postgres;Password=secret` |
| `CONDUIT_DB_TYPE`  | Database type to use (`Postgres`, `Sqlite`, etc.)     | `Postgres` or `Sqlite`       |

- `CONDUIT_DB_PATH` is required for both Sqlite (file path) and Postgres (connection string).
- `CONDUIT_DB_TYPE` determines which database provider is used by Conduit.

## Docker Configuration Examples

Below is an example of setting environment variables in a docker-compose.yml file:

```yaml
# docker-compose.yml example
version: '3'
services:
  conduitllm:
    image: conduitllm
    environment:
      # Core settings
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:80 # Listen on port 80 inside the container
      - CONDUIT_MASTER_KEY=your_secure_master_key_here
      - CONDUIT_API_BASE_URL=https://your-public-conduit-url.com # IMPORTANT: Set this to your public URL
      
      # Cache settings
      - CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
      - CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
      - CONDUIT_CACHE_USE_DEFAULT_EXPIRATION=true
      
      # Security settings
      - CONDUIT_ENABLE_HTTPS_REDIRECTION=false # HTTPS is handled by the reverse proxy
      - CONDUIT_CORS_ORIGINS=https://yourdomain.com,https://app.yourdomain.com
      
      # Database settings
      - CONDUIT_DB_PATH=/data/conduit.db
      - CONDUIT_DB_TYPE=Sqlite
    ports:
      - "80:80" # Expose container's HTTP port 80 to host's port 80
                # Assumes a reverse proxy forwards external HTTPS (443) traffic to this port (80)
    volumes:
      - ./data:/app/data # Persist database and configuration
```

Or in a Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV CONDUIT_API_BASE_URL="" # Set via docker run -e or docker-compose
ENV CONDUIT_MASTER_KEY="" # Set via docker run -e or docker-compose
ENV CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
ENV CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
ENV CONDUIT_CACHE_USE_DEFAULT_EXPIRATION=true
ENV CONDUIT_ENABLE_HTTPS_REDIRECTION=false # Assume reverse proxy handles HTTPS
ENV CONDUIT_DB_PATH=/data/conduit.db
ENV CONDUIT_DB_TYPE=Sqlite

EXPOSE 80 # Expose the HTTP port
VOLUME /data # Define volume for persistent data
ENTRYPOINT ["dotnet", "ConduitLLM.WebUI.dll"]
```

## Configuration Priority

When both environment variables and application settings (appsettings.json) are specified, environment variables take precedence. This allows for runtime configuration in containerized environments without modifying the application settings.
