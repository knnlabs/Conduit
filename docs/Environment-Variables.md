# ConduitLLM Environment Variables

This document provides comprehensive information about all environment variables used by ConduitLLM. Environment variables are particularly important when deploying the application in containerized environments like Docker.

## Table of Contents

- [Core Application Variables](#core-application-variables)
- [Cache Configuration](#cache-configuration)
- [Security Configuration](#security-configuration)
- [Docker Configuration Examples](#docker-configuration-examples)

## Core Application Variables

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_MASTER_KEY` | String | *Generated on first start* | The master key used for administrative operations and API endpoint security. This value should be kept secure. |
| `ASPNETCORE_ENVIRONMENT` | String | `Production` | Controls the .NET runtime environment. Set to `Development` for detailed error information or `Production` for optimized performance. |
| `ASPNETCORE_URLS` | String | `http://localhost:5000` | The URL(s) on which the application will listen for requests. |

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
| `CONDUIT_MASTER_KEY` | String | *Generated on first start* | Master key for administrative operations. Required for certain API endpoints. |
| `CONDUIT_ENABLE_HTTPS_REDIRECTION` | Boolean | `true` | When true, HTTP requests are redirected to HTTPS. Set to false in development or behind a proxy that handles HTTPS. |
| `CONDUIT_CORS_ORIGINS` | String | `*` | Comma-separated list of allowed origins for CORS. Use `*` to allow all origins (not recommended for production). |

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
      - ASPNETCORE_URLS=http://+:80
      - CONDUIT_MASTER_KEY=your_secure_master_key_here
      
      # Cache settings
      - CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
      - CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
      - CONDUIT_CACHE_USE_DEFAULT_EXPIRATION=true
      
      # Security settings
      - CONDUIT_ENABLE_HTTPS_REDIRECTION=false
      - CONDUIT_CORS_ORIGINS=https://yourdomain.com,https://app.yourdomain.com
    ports:
      - "80:80"
    volumes:
      - ./data:/app/data
```

Or in a Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
ENV CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
ENV CONDUIT_CACHE_USE_DEFAULT_EXPIRATION=true

EXPOSE 80
ENTRYPOINT ["dotnet", "ConduitLLM.WebUI.dll"]
```

## Configuration Priority

When both environment variables and application settings (appsettings.json) are specified, environment variables take precedence. This allows for runtime configuration in containerized environments without modifying the application settings.
