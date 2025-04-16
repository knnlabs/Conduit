# Cache Configuration

ConduitLLM uses a configurable caching system to improve performance. The cache service can be configured through either application settings or environment variables.

## Environment Variables

The following environment variables can be used to configure the caching behavior, particularly useful in containerized environments:

| Environment Variable | Type | Default | Description |
|---------------------|------|---------|-------------|
| `CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES` | Integer | 60 | The default absolute expiration time for cached items in minutes. After this time has elapsed, the cached item will be removed regardless of access patterns. Set to 0 to disable default absolute expiration. |
| `CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES` | Integer | 20 | The default sliding expiration time for cached items in minutes. If the cached item is not accessed within this time period, it will be removed. Set to 0 to disable default sliding expiration. |
| `CONDUIT_CACHE_USE_DEFAULT_EXPIRATION` | Boolean | true | Controls whether default expiration times are applied to cached items when not explicitly specified. If set to false, cached items will not expire automatically unless expiration is explicitly set when caching an item. |

## Configuration in appsettings.json

The cache can also be configured through the application settings file:

```json
{
  "Cache": {
    "DefaultAbsoluteExpirationMinutes": 60,
    "DefaultSlidingExpirationMinutes": 20,
    "UseDefaultExpirationTimes": true
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
      - CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
      - CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
      - CONDUIT_CACHE_USE_DEFAULT_EXPIRATION=true
```

Or in a Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
# ...
ENV CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES=120
ENV CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES=30
ENV CONDUIT_CACHE_USE_DEFAULT_EXPIRATION=true
# ...
```

## Programmatic Configuration

The cache service is registered in the dependency injection container at application startup:

```csharp
// In Program.cs
builder.Services.AddCacheService(builder.Configuration);
```

The `AddCacheService` extension method configures the cache service with options from both the application configuration and environment variables.
