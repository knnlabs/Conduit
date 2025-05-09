---
sidebar_position: 2
title: Cache Configuration
description: Learn how to configure and optimize caching in Conduit
---

# Cache Configuration

Conduit includes a powerful caching system that can significantly reduce costs and improve response times by storing and reusing LLM responses for identical requests.

## Why Use Caching?

- **Cost Reduction**: Avoid paying for repeated identical requests
- **Reduced Latency**: Cached responses are returned instantly
- **Improved Reliability**: Cached responses work even if providers are down
- **Consistent Responses**: Ensures the same output for the same input

## Caching Providers

Conduit supports two caching providers:

### In-Memory Cache

- Simple to set up, no additional dependencies
- Stored in application memory
- Lost when the application restarts
- Limited by available RAM

### Redis Cache

- Persistent across application restarts
- Shared across multiple Conduit instances
- Higher capacity and better performance for production
- Requires a Redis server

## Configuring Caching

### Via Web UI

1. Navigate to **Configuration > Caching**
2. Enable caching by toggling the switch
3. Select the cache provider (In-Memory or Redis)
4. Configure provider-specific settings:
   - **In-Memory**: Maximum cache size (MB)
   - **Redis**: Connection string, password, etc.
5. Set the default Time-To-Live (TTL) for cache entries
6. Save the configuration

### Via Environment Variables

For In-Memory cache:
```
CONDUIT_CACHE_ENABLED=true
CONDUIT_CACHE_TYPE=InMemory
CONDUIT_CACHE_MAX_SIZE=1024
CONDUIT_CACHE_TTL=3600
```

For Redis cache:
```
CONDUIT_CACHE_ENABLED=true
CONDUIT_CACHE_TYPE=Redis
CONDUIT_REDIS_CONNECTION=redis:6379,password=your-password
CONDUIT_CACHE_TTL=3600
```

## Cache Control

### Request-Level Cache Control

You can control caching behavior at the request level:

```json
{
  "model": "my-gpt4",
  "messages": [{"role": "user", "content": "Hello!"}],
  "cache_control": {
    "no_cache": false,
    "ttl": 7200
  }
}
```

The `cache_control` object supports:
- `no_cache`: Set to `true` to bypass the cache
- `ttl`: Override the default TTL in seconds

### Response Headers

Conduit includes cache-related headers in responses:

- `X-Cache`: `HIT` or `MISS` indicating cache status
- `X-Cache-Key`: The hash key used for the cache (if debugging is enabled)
- `X-Cache-TTL`: Remaining TTL in seconds (for cache hits)

## Cache Keys

Conduit generates cache keys based on:
- The model requested
- The complete messages array
- Selected request parameters that affect the output

Parameters like temperature, top_p, and max_tokens are included in the cache key since they affect the response, while parameters like stream or user are excluded.

## Cache Management

### Monitoring Cache Performance

The Web UI provides cache performance metrics:

1. Navigate to **Dashboard > Cache**
2. View statistics:
   - Hit rate
   - Miss rate
   - Item count
   - Memory usage
   - Average response time savings

### Clearing the Cache

You can clear the cache via the Web UI:

1. Navigate to **Configuration > Caching**
2. Click **Clear Cache**
3. Confirm the action

Or via API:
```bash
curl -X POST http://localhost:5000/admin/cache/clear \
  -H "Authorization: Bearer your-master-key"
```

## Best Practices

- **Set Appropriate TTLs**: Balance freshness vs. performance
- **Use Redis in Production**: For persistence and scaling
- **Enable for High-Volume Endpoints**: Focus on frequently repeated requests
- **Monitor Cache Performance**: Adjust settings based on hit rates
- **Consider Disabling for Critical Requests**: When absolute freshness is required

## Next Steps

- Learn about [Budget Management](budget-management) for cost control
- Explore [Environment Variables](environment-variables) for deployment configuration
- See the [WebUI Guide](webui-usage) for UI-based configuration