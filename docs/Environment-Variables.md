# ConduitLLM Environment Variables

This document provides comprehensive information about all environment variables used by ConduitLLM. Environment variables are particularly important when deploying the application in containerized environments like Docker.

ConduitLLM consists of three main services:
1. **WebUI** - The web interface for managing the system
2. **Admin API** - Administrative API for configuration and monitoring
3. **LLM API** - API for handling LLM requests

Each service has its own set of environment variables for configuration.

## Table of Contents

- [Core Application Variables](#core-application-variables)
- [Cache Configuration](#cache-configuration)
- [Security Configuration](#security-configuration)
- [Database](#database)
- [WebUI Configuration](#webui-configuration)
- [Admin API Configuration](#admin-api-configuration)
- [LLM API Configuration](#llm-api-configuration)
- [Docker Configuration Examples](#docker-configuration-examples)

## Core Application Variables

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_MASTER_KEY` | String | *Must be provided* | The master key used for administrative operations and API endpoint security. This value should be kept secure. |
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
| `CONDUIT_MASTER_KEY` | String | *Must be provided* | Master key for administrative operations. Required for certain API endpoints. |
| `CONDUIT_ENABLE_HTTPS_REDIRECTION` | Boolean | `true` | When true, HTTP requests are redirected to HTTPS. **Set to `false` when running behind a reverse proxy that handles HTTPS termination.** |
| `CONDUIT_CORS_ORIGINS` | String | `*` | Comma-separated list of allowed origins for CORS. Use `*` to allow all origins (not recommended for production). |

## Database

These variables are used by the Admin API service for database configuration:

| Variable | Description | Example Value |
|----------|-------------|--------------|
| `DB_PROVIDER` | Database provider: `sqlite` or `postgres` | `sqlite` |
| `CONDUIT_SQLITE_PATH` | SQLite database file path | `/data/conduit.db` |
| `CONDUIT_POSTGRES_CONNECTION_STRING` | PostgreSQL connection string | `Host=localhost;Port=5432;Database=conduitllm;Username=postgres;Password=secret` |

- `DB_PROVIDER` determines which database backend is used. Supported values: `sqlite` or `postgres` (all lowercase).
- For SQLite, set `CONDUIT_SQLITE_PATH`.
- For PostgreSQL, set `CONDUIT_POSTGRES_CONNECTION_STRING`.

> **Note**: With the microservices architecture, only the Admin API service needs direct database access. The WebUI and LLM API services communicate with the Admin API instead of accessing the database directly.

## WebUI Configuration

The following environment variables are specific to the WebUI service:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_ADMIN_API_URL` | String | `http://localhost:5001` | The base URL of the Admin API service. |
| `CONDUIT_LLM_API_URL` | String | `http://localhost:5002` | The base URL of the LLM API service. |
| `CONDUIT_MASTER_KEY` | String | *Must be provided* | The master key used for authentication with the Admin API. |
| `CONDUIT_USE_ADMIN_API` | Boolean | `true` | When true, WebUI uses the Admin API client and adapters; when false, it uses direct repository access (legacy mode). |
| `CONDUIT_ADMIN_TIMEOUT_SECONDS` | Integer | 30 | Timeout in seconds for API requests to the Admin service. |
| `CONDUIT_WEBUI_PORT` | Integer | 5000 | The port on which the WebUI service listens. |

## Admin API Configuration

The following environment variables are specific to the Admin API service:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_MASTER_KEY` | String | *Must be provided* | The master key used for securing the Admin API endpoints. |
| `CONDUIT_ADMIN_API_PORT` | Integer | 5001 | The port on which the Admin API service listens. |
| `CONDUIT_ADMIN_LOG_LEVEL` | String | `Information` | The logging level for the Admin API service (`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`). |

## LLM API Configuration

The following environment variables are specific to the LLM API service:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_ADMIN_API_URL` | String | `http://localhost:5001` | The base URL of the Admin API service. |
| `CONDUIT_MASTER_KEY` | String | *Must be provided* | The master key used for authentication with the Admin API. |
| `CONDUIT_LLM_API_PORT` | Integer | 5002 | The port on which the LLM API service listens. |
| `CONDUIT_LLM_LOG_LEVEL` | String | `Information` | The logging level for the LLM API service. |

## Docker Configuration Examples

Below is an example of setting environment variables in a docker-compose.yml file with all three services:

```yaml
# docker-compose.yml example with all three services
version: '3'
services:
  # WebUI service - Frontend UI at port 5000
  webui:
    image: conduitllm-webui
    container_name: conduit-webui
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
      - CONDUIT_MASTER_KEY=your_secure_master_key_here
      - CONDUIT_ADMIN_API_URL=http://admin:5001
      - CONDUIT_LLM_API_URL=http://api:5002
      - CONDUIT_USE_ADMIN_API=true
      - CONDUIT_ADMIN_TIMEOUT_SECONDS=30
      - CONDUIT_ENABLE_HTTPS_REDIRECTION=false
      - CONDUIT_CORS_ORIGINS=*
    ports:
      - "5000:5000"
    depends_on:
      - admin
      - api

  # Admin API service - Admin API and database access at port 5001
  admin:
    image: conduitllm-admin
    container_name: conduit-admin
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5001
      - CONDUIT_MASTER_KEY=your_secure_master_key_here
      - CONDUIT_ENABLE_HTTPS_REDIRECTION=false
      - CONDUIT_CORS_ORIGINS=*
      # Cache settings
      - CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
      - CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
      - CONDUIT_CACHE_USE_DEFAULT_EXPIRATION=true
      # Database settings (SQLite example)
      - DB_PROVIDER=sqlite
      - CONDUIT_SQLITE_PATH=/data/conduit.db
      # Database settings (PostgreSQL example)
      # - DB_PROVIDER=postgres
      # - CONDUIT_POSTGRES_CONNECTION_STRING=Host=postgres:5432;Database=conduitllm;Username=postgres;Password=secret
    ports:
      - "5001:5001"
    volumes:
      - ./data:/data
    # If using PostgreSQL, uncomment:
    # depends_on:
    #   - postgres

  # LLM API service - LLM endpoints at port 5002
  api:
    image: conduitllm-api
    container_name: conduit-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5002
      - CONDUIT_MASTER_KEY=your_secure_master_key_here
      - CONDUIT_ADMIN_API_URL=http://admin:5001
      - CONDUIT_ENABLE_HTTPS_REDIRECTION=false
      - CONDUIT_CORS_ORIGINS=*
    ports:
      - "5002:5002"
    depends_on:
      - admin

  # Optional Redis service for caching
  redis:
    image: redis:alpine
    container_name: conduit-redis
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  # Optional PostgreSQL service for database (if using postgres)
  # postgres:
  #   image: postgres:15-alpine
  #   container_name: conduit-postgres
  #   environment:
  #     - POSTGRES_USER=postgres
  #     - POSTGRES_PASSWORD=secret
  #     - POSTGRES_DB=conduitllm
  #   ports:
  #     - "5432:5432"
  #   volumes:
  #     - postgres-data:/var/lib/postgresql/data

volumes:
  redis-data:
  # postgres-data:
```

For individual Dockerfiles, here's an example for each service:

### WebUI Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/webui/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV CONDUIT_MASTER_KEY=""
ENV CONDUIT_ADMIN_API_URL=http://localhost:5001
ENV CONDUIT_LLM_API_URL=http://localhost:5002
ENV CONDUIT_USE_ADMIN_API=true
ENV CONDUIT_ADMIN_TIMEOUT_SECONDS=30
ENV CONDUIT_ENABLE_HTTPS_REDIRECTION=false

EXPOSE 5000
ENTRYPOINT ["dotnet", "ConduitLLM.WebUI.dll"]
```

### Admin API Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/admin/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5001
ENV CONDUIT_MASTER_KEY=""
ENV CONDUIT_ENABLE_HTTPS_REDIRECTION=false
ENV DB_PROVIDER=sqlite
ENV CONDUIT_SQLITE_PATH=/data/conduit.db

EXPOSE 5001
VOLUME /data
ENTRYPOINT ["dotnet", "ConduitLLM.Admin.dll"]
```

### LLM API Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/http/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5002
ENV CONDUIT_MASTER_KEY=""
ENV CONDUIT_ADMIN_API_URL=http://localhost:5001
ENV CONDUIT_ENABLE_HTTPS_REDIRECTION=false

EXPOSE 5002
ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]
```

## Configuration Priority

When both environment variables and application settings (appsettings.json) are specified, environment variables take precedence. This allows for runtime configuration in containerized environments without modifying the application settings.
