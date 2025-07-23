---
sidebar_position: 1
title: Environment Variables
description: Complete reference for all Conduit environment variables
---

# Environment Variables

Conduit can be configured using environment variables, which is particularly useful for Docker and production deployments. This guide provides a comprehensive reference for all available environment variables.

## Core Configuration

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` | Master key for admin access | (Required) | `your-secure-master-key` |
| `CONDUIT_HOST` | Host to bind the HTTP server | `localhost` | `0.0.0.0` |
| `CONDUIT_PORT` | Port for the HTTP server | `5000` | `8080` |
| `CONDUIT_WEBUI_PORT` | Port for the Web UI | `5001` | `8081` |
| `CONDUIT_ENVIRONMENT` | Application environment | `Development` | `Production` |
| `CONDUIT_LOG_LEVEL` | Logging verbosity | `Information` | `Debug`, `Warning`, `Error` |

## Database Configuration

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CONDUIT_DATABASE_TYPE` | Database type | `SQLite` | `PostgreSQL`, `SQLite` |
| `CONDUIT_DATABASE_PATH` | Path for SQLite database | `./data/conduit.db` | `/data/conduit.db` |
| `CONDUIT_CONNECTION_STRING` | Database connection string | (Generated) | `Data Source=/data/conduit.db` |
| `CONDUIT_DB_HOST` | PostgreSQL hostname | `localhost` | `db.example.com` |
| `CONDUIT_DB_PORT` | PostgreSQL port | `5432` | `5433` |
| `CONDUIT_DB_NAME` | PostgreSQL database name | `conduit` | `mydb` |
| `CONDUIT_DB_USER` | PostgreSQL username | `postgres` | `dbuser` |
| `CONDUIT_DB_PASSWORD` | PostgreSQL password | (Empty) | `password123` |

## Cache Configuration

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CONDUIT_CACHE_ENABLED` | Enable response caching | `false` | `true` |
| `CONDUIT_CACHE_TYPE` | Cache provider | `InMemory` | `Redis` |
| `CONDUIT_REDIS_ENABLED` | Enable Redis cache | `false` | `true` |
| `CONDUIT_REDIS_CONNECTION` | Redis connection string | `localhost:6379` | `redis:6379,password=xyz` |
| `CONDUIT_CACHE_TTL` | Default cache TTL in seconds | `3600` | `7200` |
| `CONDUIT_CACHE_MAX_SIZE` | Max size for in-memory cache (MB) | `1024` | `2048` |

## Security Configuration

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CONDUIT_RATE_LIMIT_ENABLED` | Enable global rate limiting | `true` | `false` |
| `CONDUIT_RATE_LIMIT_WINDOW` | Rate limit window in seconds | `60` | `120` |
| `CONDUIT_RATE_LIMIT_MAX` | Max requests per window | `100` | `500` |
| `CONDUIT_CORS_ENABLED` | Enable CORS | `true` | `false` |
| `CONDUIT_CORS_ORIGINS` | Allowed CORS origins | `*` | `https://app.example.com` |
| `CONDUIT_SECURE_HEADERS` | Enable security headers | `true` | `false` |

## Router Configuration

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CONDUIT_ROUTER_STRATEGY` | Default routing strategy | `Simple` | `Priority`, `LeastCost` |
| `CONDUIT_FALLBACK_ENABLED` | Enable fallback routing | `true` | `false` |
| `CONDUIT_HEALTH_CHECK_ENABLED` | Enable provider health checks | `true` | `false` |
| `CONDUIT_HEALTH_CHECK_INTERVAL` | Health check interval in seconds | `300` | `60` |

## Provider Configuration

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CONDUIT_OPENAI_API_KEY` | OpenAI API key | (Empty) | `sk-...` |
| `CONDUIT_ANTHROPIC_API_KEY` | Anthropic API key | (Empty) | `sk-ant-...` |
| `CONDUIT_AZURE_OPENAI_API_KEY` | Azure OpenAI API key | (Empty) | `...` |
| `CONDUIT_AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint | (Empty) | `https://...` |
| `CONDUIT_GOOGLE_API_KEY` | Google API key | (Empty) | `...` |
| `CONDUIT_AWS_ACCESS_KEY` | AWS access key ID | (Empty) | `AKIA...` |
| `CONDUIT_AWS_SECRET_KEY` | AWS secret access key | (Empty) | `...` |
| `CONDUIT_AWS_REGION` | AWS region | `us-east-1` | `eu-west-1` |

## Using Environment Variables

### Docker Compose

```yaml
version: '3'
services:
  conduit-api:
    image: ghcr.io/knnlabs/conduit-http:latest
    environment:
      - CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-secure-key
      - CONDUIT_HOST=0.0.0.0
      - CONDUIT_PORT=5000
      - CONDUIT_CACHE_ENABLED=true
      - CONDUIT_REDIS_CONNECTION=redis:6379
```

### Docker Run

```bash
docker run -p 5000:5000 \
  -e CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-secure-key \
  -e CONDUIT_HOST=0.0.0.0 \
  -e CONDUIT_CACHE_ENABLED=true \
  ghcr.io/knnlabs/conduit-http:latest
```

### Environment File

You can also use a `.env` file:

```
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-secure-key
CONDUIT_HOST=0.0.0.0
CONDUIT_PORT=5000
CONDUIT_CACHE_ENABLED=true
```

## Next Steps

- Learn about [Cache Configuration](cache-configuration) for performance optimization
- Explore [Budget Management](budget-management) for cost control
- See the [WebUI Guide](webui-usage) for UI-based configuration