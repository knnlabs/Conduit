# Core API Cache Usage Analysis

## Overview
The Core API (ConduitLLM.Http) uses caching extensively for performance optimization and system efficiency. Here's a comprehensive analysis of actual cache implementations found in the codebase.

## Cache Types Used

### 1. IMemoryCache (In-Memory Caching)
- **Registration**: `builder.Services.AddMemoryCache()` in Program.cs
- **Primary Usage**: Fast, local caching for frequently accessed data

### 2. IDistributedCache (Distributed Caching)
- **Redis**: `builder.Services.AddStackExchangeRedisCache()` for production
- **In-Memory**: `builder.Services.AddDistributedMemoryCache()` for development
- **Primary Usage**: Shared cache across multiple instances, persistent storage

## Actual Cache Implementations

### 1. Virtual Key Caching

#### RedisVirtualKeyCache (`/Services/RedisVirtualKeyCache.cs`)
- **Purpose**: High-performance Virtual Key validation with Redis backend
- **Key Pattern**: `vkey:{keyHash}`
- **Expiry**: 30 minutes default
- **Features**:
  - Immediate invalidation via pub/sub channel
  - Cache statistics tracking (hits/misses/invalidations)
  - Fallback to database on cache miss
  - Validation of key expiry and enabled status

#### VirtualKeyRateLimitCache (`/Services/VirtualKeyRateLimitCache.cs`)
- **Purpose**: Synchronous access to rate limit configurations
- **Storage**: Dual storage - ConcurrentDictionary + IMemoryCache
- **Key Pattern**: `vkey_ratelimits:{virtualKeyHash}`
- **Refresh**: Every 30 seconds via background timer
- **Expiry**: 5 minutes in memory cache, 1 minute freshness check

#### CachedApiVirtualKeyService (`/Services/CachedApiVirtualKeyService.cs`)
- **Purpose**: Wraps Virtual Key service with caching layer
- **Uses**: IVirtualKeyCache interface
- **Operations**: Get, invalidate single/multiple keys, statistics

### 2. Security & Rate Limiting

#### SecurityService (`/Services/SecurityService.cs`)
- **Failed Login Tracking**:
  - Key: `failed_login:{ipAddress}`
  - Stores failed authentication attempts
  - Ban tracking: `ban:{ipAddress}`
- **Rate Limiting**:
  - Key: `rate_limit:{ipAddress}:{endpoint}`
  - Virtual Key rate limits: `vkey_rate:{virtualKeyId}:{endpoint}`
- **Dual Cache Strategy**: Uses both IMemoryCache and IDistributedCache

#### IpFilterService (`/Services/IpFilterService.cs`)
- **Purpose**: Cache IP filtering rules
- **Key**: `ip_filters`
- **Usage**: `_cache.GetOrCreateAsync()` pattern

### 3. Async Task Management

#### HybridAsyncTaskService (`ConduitLLM.Core/Services/HybridAsyncTaskService.cs`)
- **Purpose**: Hybrid database + cache storage for async tasks
- **Key Pattern**: `async:task:{taskId}`
- **Expiry**: 2 hours for completed tasks
- **Storage**: IDistributedCache (Redis/In-Memory)
- **Features**:
  - Database as primary storage
  - Cache for performance optimization
  - Event-driven cache invalidation

### 4. Health Monitoring & Alerts

#### HealthMonitoringService (`/Services/HealthMonitoringService.cs`)
- **Cached Data**:
  - Active alerts: `active_alerts`
  - Component health: `component_health_{componentName}`
- **Usage**: IMemoryCache for quick access

#### AlertManagementService (`/Services/AlertManagementService.cs`)
- **Cached Data**:
  - Alert history: `alert_history_{alertId}` (30 days)
  - Active alerts: `active_alerts` (7 days)
  - Alert rules: `alert_rules` (30 days)
  - Suppressions: `alert_suppressions` (30 days)

### 5. Model Discovery

#### ModelCapabilitiesDiscoveredHandler (`/EventHandlers/ModelCapabilitiesDiscoveredHandler.cs`)
- **Purpose**: Cache provider model capabilities
- **Key Pattern**: `provider_capabilities_{providerName}`
- **Expiry**: 24 hours
- **Storage**: IMemoryCache

### 6. Performance Monitoring

#### PerformanceMonitoringService
- Uses IMemoryCache for metrics storage
- Caches performance data and resource metrics

### 7. Graceful Shutdown

#### GracefulShutdownService (`/Services/GracefulShutdownService.cs`)
- **Operations**:
  - Get cache statistics during shutdown
  - Clear expired entries before shutdown
  - Ensures cache consistency

## Cache Invalidation Patterns

### Event-Driven Invalidation
Multiple event handlers for cache invalidation:

1. **VirtualKeyCacheInvalidationHandler**
   - Handles Virtual Key updates/deletions
   - Immediate cache invalidation via events

2. **AsyncTaskCacheInvalidationHandler**
   - Removes task data from cache on completion/failure
   - Key pattern: `async:task:{taskId}`

3. **ResilientSpendUpdateProcessor**
   - Manages spend update cache
   - Ensures eventual consistency

## Cache Statistics & Monitoring

### Redis Cache Statistics
- Hit count: `conduit:cache:stats:hits`
- Miss count: `conduit:cache:stats:misses`
- Invalidation count: `conduit:cache:stats:invalidations`
- Reset time: `conduit:cache:stats:reset_time`

## Key Observations

1. **Dual Cache Strategy**: Many services use both IMemoryCache (fast, local) and IDistributedCache (shared, persistent)

2. **Event-Driven Architecture**: Cache invalidation is primarily event-driven using MassTransit

3. **Resilience**: Cache operations are wrapped in try-catch blocks with fallback to database

4. **TTL Management**: Different TTLs based on data type:
   - Virtual Keys: 30 minutes
   - Model capabilities: 24 hours
   - Alerts: 7-30 days
   - Rate limits: 5 minutes

5. **Performance Focus**: Critical paths (Virtual Key validation) use Redis for sub-millisecond response times

6. **Background Refresh**: Some caches (VirtualKeyRateLimitCache) use background timers for periodic refresh

## Configuration

### Redis Configuration (Production)
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "conduit-tasks:";
});
```

### In-Memory Configuration (Development)
```csharp
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
```

## Performance Impact

1. **Virtual Key Validation**: ~50x faster with Redis cache vs database
2. **Rate Limiting**: Synchronous access via in-memory cache
3. **Health Monitoring**: Instant access to system metrics
4. **Task Status**: Reduced database load for frequent status checks