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
| `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` | String | *Must be provided* | The backend authentication key used for service-to-service communication between backend services. This value should be kept secure. |
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

ConduitLLM provides comprehensive security configuration options:

### Authentication Keys

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` | String | *Must be provided* | Backend authentication key for service-to-service authentication (WebUI → Admin API, LLM API → Admin API). |
| `CONDUIT_INSECURE` | Boolean | `false` | **DANGER**: When set to `true`, disables all authentication for the WebUI. **ONLY works in Development environments**. The application will throw an error and refuse to start if this is enabled in Production or Staging environments. A prominent warning banner is displayed in the UI when enabled. |

### Failed Login Protection

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_MAX_FAILED_ATTEMPTS` | Integer | 5 | Maximum failed login attempts before temporarily banning an IP address. |
| `CONDUIT_IP_BAN_DURATION_MINUTES` | Integer | 30 | How long (in minutes) an IP address remains banned after exceeding failed login attempts. |

### IP Access Control

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_IP_FILTERING_ENABLED` | Boolean | `false` | Enable/disable IP filtering middleware. |
| `CONDUIT_IP_FILTER_MODE` | String | `permissive` | Filter mode: `permissive` (blacklist) or `restrictive` (whitelist). |
| `CONDUIT_IP_FILTER_ALLOW_PRIVATE` | Boolean | `true` | Automatically allow private/intranet IP addresses (RFC 1918 ranges). |
| `CONDUIT_IP_FILTER_WHITELIST` | String | *None* | Comma-separated list of allowed IPs or CIDR ranges (e.g., `192.168.1.0/24,10.0.0.0/8`). |
| `CONDUIT_IP_FILTER_BLACKLIST` | String | *None* | Comma-separated list of blocked IPs or CIDR ranges. |
| `CONDUIT_IP_FILTER_DEFAULT_ALLOW` | Boolean | `true` | Default action when no rules match. |
| `CONDUIT_IP_FILTER_BYPASS_ADMIN_UI` | Boolean | `true` | Bypass filtering for admin UI paths. |

The IP filtering middleware provides enterprise-grade access control:
- Whitelist/blacklist configuration with CIDR subnet support
- Automatic detection and handling of private/intranet IPs
- Integration with failed login tracking system
- Dynamic configuration via environment variables or Admin API

### Rate Limiting

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_RATE_LIMITING_ENABLED` | Boolean | `false` | Enable/disable rate limiting middleware to prevent DoS attacks. |
| `CONDUIT_RATE_LIMIT_MAX_REQUESTS` | Integer | 100 | Maximum number of requests allowed per time window. |
| `CONDUIT_RATE_LIMIT_WINDOW_SECONDS` | Integer | 60 | Time window in seconds for rate limiting. |
| `CONDUIT_RATE_LIMIT_EXCLUDED_PATHS` | String | `/health,/_blazor,/css,/js,/images` | Comma-separated list of paths excluded from rate limiting. |

### Security Headers

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_SECURITY_HEADERS_X_FRAME_OPTIONS_ENABLED` | Boolean | `true` | Enable X-Frame-Options header to prevent clickjacking. |
| `CONDUIT_SECURITY_HEADERS_X_FRAME_OPTIONS` | String | `DENY` | X-Frame-Options value (`DENY`, `SAMEORIGIN`). |
| `CONDUIT_SECURITY_HEADERS_CSP_ENABLED` | Boolean | `false` | Enable Content Security Policy header. |
| `CONDUIT_SECURITY_HEADERS_CSP` | String | `default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline';` | Content Security Policy directive. |
| `CONDUIT_SECURITY_HEADERS_HSTS_ENABLED` | Boolean | `true` | Enable HTTP Strict Transport Security (HTTPS only). |
| `CONDUIT_SECURITY_HEADERS_HSTS_MAX_AGE` | Integer | 31536000 | HSTS max age in seconds (default: 1 year). |
| `CONDUIT_SECURITY_HEADERS_X_CONTENT_TYPE_OPTIONS_ENABLED` | Boolean | `true` | Enable X-Content-Type-Options header to prevent MIME sniffing. |
| `CONDUIT_SECURITY_HEADERS_X_XSS_PROTECTION_ENABLED` | Boolean | `true` | Enable X-XSS-Protection header. |
| `CONDUIT_SECURITY_HEADERS_REFERRER_POLICY_ENABLED` | Boolean | `true` | Enable Referrer-Policy header. |
| `CONDUIT_SECURITY_HEADERS_REFERRER_POLICY` | String | `strict-origin-when-cross-origin` | Referrer-Policy value. |
| `CONDUIT_SECURITY_HEADERS_PERMISSIONS_POLICY_ENABLED` | Boolean | `true` | Enable Permissions-Policy header. |
| `CONDUIT_SECURITY_HEADERS_PERMISSIONS_POLICY` | String | `accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()` | Permissions-Policy directive. |

### Distributed Security Tracking

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING` | Boolean | `false` | Enable distributed security tracking with Redis for multi-instance deployments. |

### Network Security

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_ENABLE_HTTPS_REDIRECTION` | Boolean | `true` | When true, HTTP requests are redirected to HTTPS. **Set to `false` when running behind a reverse proxy that handles HTTPS termination.** |
| `CONDUIT_CORS_ORIGINS` | String | `*` | Comma-separated list of allowed origins for CORS. Use `*` to allow all origins (not recommended for production). |
| `CONDUIT_REDIS_CONNECTION_STRING` | String | *None* | Redis connection string for Data Protection key persistence and distributed security tracking. When provided, ASP.NET Core Data Protection keys are stored in Redis for distributed scenarios. Format: `redis-host:6379` or full connection string. |

### Data Protection Keys

ASP.NET Core Data Protection is used to protect sensitive data like authentication cookies and anti-forgery tokens. By default, these keys are stored in the file system, which can cause issues in distributed scenarios:
- Keys are lost when containers restart
- Different container instances can't share keys
- Users may experience authentication issues

When `CONDUIT_REDIS_CONNECTION_STRING` is provided, Data Protection keys are persisted to Redis, solving these issues. If Redis is unavailable, the system falls back to file system storage.

## Database

These variables are used by all services for database configuration:

| Variable | Description | Example Value |
|----------|-------------|--------------|
| `DATABASE_URL` | PostgreSQL connection URL (required) | `postgresql://postgres:password@localhost:5432/conduitllm` |

- PostgreSQL is the only supported database provider.
- The connection string must be in URL format starting with `postgresql://` or `postgres://`.
- Connection retry logic with exponential backoff is implemented to handle temporary connection issues.

> **Note**: With the microservices architecture, both Core API and Admin API services need database access. The WebUI service communicates with the Admin API instead of accessing the database directly.

## WebUI Configuration

The following environment variables are specific to the WebUI service:

> **Important:** As of May 2025, the WebUI operates in Admin API mode by default. Legacy direct database access mode is deprecated and will be removed in October 2025. Ensure `CONDUIT_USE_ADMIN_API` is set to `true` (default) and configure `CONDUIT_ADMIN_API_BASE_URL` to point to your Admin API service.

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_ADMIN_API_BASE_URL` | String | `http://localhost:5002` | The base URL of the Admin API service. This is the primary variable for configuring Admin API connection. |
| `CONDUIT_ADMIN_API_URL` | String | `http://localhost:5001` | Legacy alias for `CONDUIT_ADMIN_API_BASE_URL`. Use the BASE_URL version for new deployments. |
| `CONDUIT_LLM_API_URL` | String | `http://localhost:5002` | The base URL of the LLM API service. |
| `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` | String | *Must be provided* | The backend authentication key used for authentication with the Admin API. |
| `CONDUIT_USE_ADMIN_API` | Boolean | `true` | When true (default), WebUI uses the Admin API client and adapters; when explicitly set to false, it uses direct repository access (legacy mode, will be deprecated in October 2025). |
| `CONDUIT_DISABLE_DIRECT_DB_ACCESS` | Boolean | `false` | When true, completely disables direct database access mode, forcing Admin API mode regardless of other settings. Used to prevent legacy mode completely. |
| `CONDUIT_ADMIN_TIMEOUT_SECONDS` | Integer | 30 | Timeout in seconds for API requests to the Admin service. |
| `CONDUIT_WEBUI_PORT` | Integer | 5000 | The port on which the WebUI service listens. |
| `CONDUIT_INSECURE` | Boolean | `false` | **DANGER**: When set to `true`, disables all authentication for the WebUI. **ONLY works in Development environments**. The application will throw an error and refuse to start if this is enabled in Production or Staging environments. |

### Clerk Authentication

The WebUI uses Clerk for authentication. Configure the following environment variables:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` | String | *Must be provided* | Your Clerk publishable key from the Clerk dashboard. |
| `CLERK_SECRET_KEY` | String | *Must be provided* | Your Clerk secret key from the Clerk dashboard. |

**Note**: AutoLogin should only be used in development or single-user environments. It bypasses the login screen entirely when enabled.

## Admin API Configuration

The following environment variables are specific to the Admin API service:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` | String | *Must be provided* | The backend authentication key used for securing the Admin API endpoints. |
| `CONDUIT_ADMIN_API_PORT` | Integer | 5001 | The port on which the Admin API service listens. |
| `CONDUIT_ADMIN_LOG_LEVEL` | String | `Information` | The logging level for the Admin API service (`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`). |

## LLM API Configuration

The following environment variables are specific to the LLM API service:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_ADMIN_API_URL` | String | `http://localhost:5001` | The base URL of the Admin API service. |
| `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` | String | *Must be provided* | The backend authentication key used for authentication with the Admin API. |
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
      - CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your_secure_backend_key_here
      - NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_xxxxx
      - CLERK_SECRET_KEY=sk_test_xxxxx
      - CONDUIT_ADMIN_API_URL=http://admin:5001
      - CONDUIT_LLM_API_URL=http://api:5002
      - CONDUIT_USE_ADMIN_API=true
      - CONDUIT_ADMIN_TIMEOUT_SECONDS=30
      - CONDUIT_ENABLE_HTTPS_REDIRECTION=false
      - CONDUIT_CORS_ORIGINS=*
      - CONDUIT_REDIS_CONNECTION_STRING=redis:6379
      # Security Settings
      - CONDUIT_MAX_FAILED_ATTEMPTS=5
      - CONDUIT_IP_BAN_DURATION_MINUTES=30
      - CONDUIT_IP_FILTERING_ENABLED=true
      - CONDUIT_IP_FILTER_MODE=permissive
      - CONDUIT_IP_FILTER_ALLOW_PRIVATE=true
      - CONDUIT_RATE_LIMITING_ENABLED=true
      - CONDUIT_RATE_LIMIT_MAX_REQUESTS=100
      - CONDUIT_RATE_LIMIT_WINDOW_SECONDS=60
      - CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING=true
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
      - CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your_secure_backend_key_here
      - CONDUIT_ENABLE_HTTPS_REDIRECTION=false
      - CONDUIT_CORS_ORIGINS=*
      - CONDUIT_REDIS_CONNECTION_STRING=redis:6379
      # Cache settings
      - CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
      - CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
      - CONDUIT_CACHE_USE_DEFAULT_EXPIRATION=true
      # Database settings (PostgreSQL required)
      - DATABASE_URL=postgresql://postgres:secret@postgres:5432/conduitllm
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
      - CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your_secure_backend_key_here
      - CONDUIT_ADMIN_API_URL=http://admin:5001
      - CONDUIT_ENABLE_HTTPS_REDIRECTION=false
      - CONDUIT_CORS_ORIGINS=*
      - CONDUIT_REDIS_CONNECTION_STRING=redis:6379
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
ENV CONDUIT_API_TO_API_BACKEND_AUTH_KEY=""
ENV NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=""
ENV CLERK_SECRET_KEY=""
ENV CONDUIT_ADMIN_API_URL=http://localhost:5001
ENV CONDUIT_LLM_API_URL=http://localhost:5002
ENV CONDUIT_USE_ADMIN_API=true
ENV CONDUIT_ADMIN_TIMEOUT_SECONDS=30
ENV CONDUIT_ENABLE_HTTPS_REDIRECTION=false
ENV CONDUIT_MAX_FAILED_ATTEMPTS=5
ENV CONDUIT_IP_BAN_DURATION_MINUTES=30

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
ENV CONDUIT_API_TO_API_BACKEND_AUTH_KEY=""
ENV CONDUIT_ENABLE_HTTPS_REDIRECTION=false
ENV DATABASE_URL=""

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
ENV CONDUIT_API_TO_API_BACKEND_AUTH_KEY=""
ENV CONDUIT_ADMIN_API_URL=http://localhost:5001
ENV CONDUIT_ENABLE_HTTPS_REDIRECTION=false

EXPOSE 5002
ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]
```

## Configuration Priority

When both environment variables and application settings (appsettings.json) are specified, environment variables take precedence. This allows for runtime configuration in containerized environments without modifying the application settings.
