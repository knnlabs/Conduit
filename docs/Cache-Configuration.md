# Cache Configuration

ConduitLLM uses a configurable caching system to improve performance. The cache service can be configured through either application settings or environment variables.

## Cache Types

ConduitLLM supports two types of caching:

1. **Memory Cache** - Default in-memory caching using .NET's `IMemoryCache`
2. **Redis Cache** - Distributed caching using Redis server, enabling multi-instance deployments with a shared cache

## Environment Variables

The following environment variables can be used to configure the caching behavior, particularly useful in containerized environments:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_CACHE_ENABLED` | Boolean | true | Enable or disable caching throughout the application. |
| `CONDUIT_CACHE_TYPE` | String | "Memory" | The type of cache to use. Valid values are "Memory" or "Redis". |
| `CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES` | Integer | 60 | The default absolute expiration time for cached items in minutes. After this time has elapsed, the cached item will be removed regardless of access patterns. Set to 0 to disable default absolute expiration. |
| `CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES` | Integer | 20 | The default sliding expiration time for cached items in minutes. If the cached item is not accessed within this time period, it will be removed. Set to 0 to disable default sliding expiration. |
| `CONDUIT_CACHE_USE_DEFAULT_EXPIRATION` | Boolean | true | Controls whether default expiration times are applied to cached items when not explicitly specified. If set to false, cached items will not expire automatically unless expiration is explicitly set when caching an item. |
| `CONDUIT_REDIS_CONNECTION_STRING` | String | null | Connection string for Redis server when using Redis cache. Example: "localhost:6379,password=password123". |
| `CONDUIT_REDIS_INSTANCE_NAME` | String | "conduit:" | Instance name prefix for Redis keys to isolate cache entries for this instance. |

## Configuration in appsettings.json

The cache can also be configured through the application settings file:

```json
{
  "Cache": {
    "IsEnabled": true,
    "CacheType": "Redis",
    "DefaultAbsoluteExpirationMinutes": 60,
    "DefaultSlidingExpirationMinutes": 20,
    "DefaultExpirationMinutes": 60,
    "RedisConnectionString": "localhost:6379,abortConnect=false,ssl=false",
    "RedisInstanceName": "conduit:"
  }
}
```

## Configuration Priority

When both environment variables and application settings are specified, the environment variables take precedence. This allows for runtime configuration in containerized environments without modifying the application settings.

## Docker Configuration Example

When running the application in Docker, you can set these environment variables in your docker-compose.yml or Dockerfile:

```yaml
# docker-compose.yml example
version: '3'
services:
  conduitllm:
    image: conduitllm
    environment:
      - CONDUIT_CACHE_ENABLED=true
      - CONDUIT_CACHE_TYPE=Redis
      - CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
      - CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
      - CONDUIT_REDIS_CONNECTION_STRING=redis:6379,abortConnect=false
      - CONDUIT_REDIS_INSTANCE_NAME=conduit:

  redis:
    image: redis:alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    restart: unless-stopped

volumes:
  redis-data:
```

Or in a Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
# ...
ENV CONDUIT_CACHE_ENABLED=true
ENV CONDUIT_CACHE_TYPE=Redis
ENV CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
ENV CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
ENV CONDUIT_REDIS_CONNECTION_STRING=redis:6379,abortConnect=false
ENV CONDUIT_REDIS_INSTANCE_NAME=conduit:
# ...
```

## Programmatic Configuration

The cache service is registered in the dependency injection container at application startup:

```csharp
// In Program.cs
builder.Services.AddCacheService(builder.Configuration);
```

The `AddCacheService` extension method configures the cache service with options from both the application configuration and environment variables.

## Redis Configuration Details

### Connection String Format

Redis connection strings follow the StackExchange.Redis format:

```
server:port,password=password123,ssl=true,abortConnect=false
```

Common parameters:

- `server`: Redis server hostname or IP address
- `port`: Redis server port (default: 6379)
- `password`: Optional authentication password
- `ssl`: Enable SSL encryption (true/false)
- `abortConnect`: Whether to abort connection if Redis is unavailable (default: true)
- `connectTimeout`: Connection timeout in milliseconds
- `syncTimeout`: Synchronous operation timeout in milliseconds
- `allowAdmin`: Allow administrative operations (required for some commands, default: false)

### Redis Instance Name

The `RedisInstanceName` setting (or `CONDUIT_REDIS_INSTANCE_NAME` environment variable) defines a prefix for all Redis keys created by the application. This helps isolate cache entries when multiple applications share a Redis instance. The default is "conduit:".

All keys created by the Conduit cache service will have this prefix, e.g., `conduit:llm-response:gpt4:12345`.

### Key Naming Conventions

Conduit uses a consistent key naming convention for Redis keys:

- `{prefix}{category}:{identifier}`

For example:
- `conduit:chat:session123` - Chat session data
- `conduit:embedding:text456` - Embedding result
- `conduit:model:anthropic` - Model configuration

### Redis Security Best Practices

When using Redis in production:

1. **Authentication**: Always set a strong Redis password
2. **Network Security**: Restrict Redis access using firewall rules
3. **TLS Encryption**: Enable SSL/TLS for Redis connections when possible
4. **Dedicated Users**: Use Redis ACLs (Access Control Lists) for granular permission control
5. **No Internet Exposure**: Never expose Redis directly to the internet

### Monitoring Redis Cache

The Conduit Web UI provides a Redis cache monitoring panel that shows:

- Connection status
- Memory usage
- Hit/miss ratio
- Active connections
- Operations per second
- Cache size (number of keys)

For external monitoring, consider tools like Redis Commander, RedisInsight, or Prometheus with Redis Exporter.

## Distributed Cache Statistics

When running multiple Conduit instances with Redis, the system automatically collects and aggregates cache statistics across all instances.

### Configuration

Enable distributed statistics in `appsettings.json`:

```json
{
  "CacheStatistics": {
    "Enabled": true,
    "FlushInterval": "00:00:10",
    "AggregationInterval": "00:01:00",
    "RetentionPeriod": "24:00:00",
    "MaxResponseTimeSamples": 1000
  },
  "StatisticsAlertThresholds": {
    "MaxInstanceMissingTime": "00:01:00",
    "MaxAggregationLatency": "00:00:00.500",
    "MaxDriftPercentage": 5.0,
    "MaxRedisMemoryBytes": 1073741824,
    "MinActiveInstances": 1
  }
}
```

### Health Monitoring

Access cache statistics health through the standard health check endpoint:

```bash
curl http://localhost:5000/health/cache_statistics
```

The health check monitors:
- Active instances reporting statistics
- Redis connectivity for statistics storage
- Statistics accuracy across instances
- Aggregation performance

### Horizontal Scaling Considerations

The distributed statistics system supports:

- **Auto-discovery**: Instances automatically register when they start
- **Graceful shutdown**: Statistics are preserved when instances stop
- **Accurate aggregation**: Statistics are collected locally and aggregated globally
- **Performance**: Handles 1000+ instances with sub-100ms aggregation latency

### Redis Key Structure for Statistics

Statistics are stored using the following Redis keys:

```
conduit:cache:stats:{instanceId}:{region}     # Per-instance statistics
conduit:cache:instances                        # Active instance tracking
conduit:cache:stats:aggregated:{region}       # Cached aggregated results
```

### Monitoring Dashboard

A Grafana dashboard is available at `monitoring/grafana/dashboards/cache-statistics-monitoring.json` which displays:

- Cache hit rate by region
- Active instances count
- Aggregation latency
- Response time percentiles
- Redis memory usage for statistics
- Active alerts

## Troubleshooting

Common Redis connection issues:

1. **Connection Refused**: Verify the Redis server is running and accessible from the Conduit server
2. **Authentication Failed**: Check the password in the connection string
3. **Timeout**: Increase the connection timeout in the connection string
4. **SSL Handshake Failed**: Verify SSL/TLS settings match the Redis server configuration

When Redis connection fails, Conduit will fall back to in-memory caching to maintain functionality.
