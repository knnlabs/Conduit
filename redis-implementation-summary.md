# Redis Cache Implementation Summary

This document provides an overview of the Redis cache implementation for Conduit that has been completed.

## Implemented Components

1. **Redis Cache Service (`RedisCacheService.cs`)**
   - Implements the `ICacheService` interface using Redis
   - Handles all key operations: Get, Set, Remove, GetOrCreateAsync, RemoveByPrefix
   - Uses StackExchange.Redis for Redis operations
   - Handles serialization of objects to/from Redis
   - Implements proper error handling and logging
   - Thread-safe implementation with semaphore for concurrent operations

2. **Redis Connection Factory (`RedisConnectionFactory.cs`)**
   - Manages Redis connections with proper lifecycle management
   - Implements connection pooling to improve performance
   - Handles reconnection logic and error handling
   - Subscribes to Redis connection events for diagnostics
   - Supports custom connection strings for flexibility

3. **Cache Service Factory (`CacheServiceFactory.cs`)**
   - Creates the appropriate cache implementation based on configuration
   - Supports Memory, Redis, and Null (disabled) cache types
   - Handles graceful fallback to memory cache if Redis connection fails
   - Provides a consistent interface regardless of underlying cache implementation

4. **Service Registration Extensions (`ServiceCollectionExtensions.cs`)**
   - Registers all Redis dependencies in the DI container
   - Configures appropriate Redis connection options
   - Handles fallback to memory cache when Redis is not available
   - Properly integrates with existing services

5. **UI Components**
   - `CachingSettings.razor` provides UI for configuring cache settings
   - Support for testing Redis connections from the UI
   - Visualizing Redis metrics and status
   - Cache statistics and performance monitoring

6. **Redis Metrics Service (`RedisCacheMetricsService.cs`)**
   - Provides Redis-specific metrics like memory usage, connection status, etc.
   - Supports monitoring Redis health and performance
   - Integrates with the UI for visualization

7. **Tests**
   - Unit tests for RedisCacheService functionality
   - Integration tests for Redis connections (skipped when Redis isn't available)
   - Performance comparison tests between Memory and Redis caches

## Configuration Options

The Redis cache can be configured through:

1. **Environment Variables**
   - `CONDUIT_CACHE_ENABLED` - Enable/disable caching
   - `CONDUIT_CACHE_TYPE` - "Memory" or "Redis"
   - `CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES` - Default expiration time
   - `CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES` - Default sliding expiration
   - `CONDUIT_REDIS_CONNECTION_STRING` - Redis connection string
   - `CONDUIT_REDIS_INSTANCE_NAME` - Redis instance name prefix

2. **appsettings.json Configuration**
   ```json
   {
     "Cache": {
       "IsEnabled": true,
       "CacheType": "Redis",
       "DefaultExpirationMinutes": 60,
       "RedisConnectionString": "localhost:6379,abortConnect=false",
       "RedisInstanceName": "conduit:"
     }
   }
   ```

3. **WebUI Settings Page**
   - Cache type selection (Memory/Redis)
   - Redis connection string configuration
   - Cache behavior configuration
   - Redis connection testing

## Implementation Details

### Key Features

1. **Graceful Fallback**: If Redis is unavailable, the system automatically falls back to in-memory caching instead of failing.

2. **Connection Pooling**: Uses StackExchange.Redis's connection multiplexer for efficient connection management.

3. **Thread Safety**: All Redis operations are thread-safe with proper semaphore usage for concurrent operations.

4. **Monitoring**: Extensive metrics and monitoring for Redis cache performance.

5. **Flexibility**: Support for different Redis configurations and deployment scenarios.

### Usage

The Redis cache implementation is used transparently by the application. The `ICacheService` abstraction ensures that code does not need to be changed to use Redis vs Memory cache.

```csharp
// Example usage remains the same regardless of cache implementation
var result = await _cacheService.GetOrCreateAsync<MyType>("my-key", 
    async () => await _service.GetExpensiveDataAsync(),
    TimeSpan.FromMinutes(30));
```

### Error Handling

The implementation includes robust error handling:

1. Connection failures are logged and result in fallback to memory cache
2. Redis operation errors are caught and logged
3. Serialization errors are properly handled and don't crash the application

## Docker Support

The Redis implementation supports Docker deployment scenarios:

1. **Redis in same Docker Compose network**:
   ```yaml
   services:
     conduit:
       image: conduitllm
       environment:
         - CONDUIT_CACHE_ENABLED=true
         - CONDUIT_CACHE_TYPE=Redis
         - CONDUIT_REDIS_CONNECTION_STRING=redis:6379
         
     redis:
       image: redis:alpine
       ports:
         - "6379:6379"
       volumes:
         - redis-data:/data
   ```

2. **External Redis server**:
   ```
   CONDUIT_REDIS_CONNECTION_STRING=my-redis-server.example.com:6379,password=mypassword,ssl=true
   ```

## Testing

The implementation includes comprehensive tests:

1. **Unit tests** for all Redis functionality
2. **Integration tests** that can be run when Redis is available
3. **Performance comparison tests** to measure Redis vs Memory cache

## Performance Considerations

- Redis performs slightly slower than in-memory cache due to network overhead
- Redis provides better scalability for multi-instance deployments
- Memory usage is more efficient with Redis for large datasets
- Redis provides data persistence across application restarts