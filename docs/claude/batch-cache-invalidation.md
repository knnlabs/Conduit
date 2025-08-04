# Batch Cache Invalidation Configuration

## Overview

The batch cache invalidation system optimizes Redis operations by batching multiple cache invalidation requests together, reducing network overhead and improving performance. This system can reduce Redis operations by 80%+ during bulk operations.

## Configuration

The batch cache invalidation system is configured using environment variables. All configuration keys follow the ASP.NET Core convention where `__` (double underscore) represents hierarchy levels.

### Environment Variables

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CacheInvalidation__BatchingEnabled` | Enable/disable batch processing | `true` | `"true"` or `"false"` |
| `CacheInvalidation__BatchWindow` | Time window for collecting requests before processing | `100ms` | `"50ms"`, `"200ms"`, `"1s"` |
| `CacheInvalidation__MaxBatchSize` | Maximum number of items in a single batch | `100` | `"50"`, `"200"`, `"500"` |
| `CacheInvalidation__EnableCoalescing` | Remove duplicate keys within a batch | `true` | `"true"` or `"false"` |

### Docker Compose Configuration

The environment variables are configured in the docker-compose files:

#### Production Settings (docker-compose.yml)
```yaml
services:
  api:
    environment:
      # Batch Cache Invalidation configuration
      CacheInvalidation__BatchingEnabled: "true"
      CacheInvalidation__BatchWindow: "100ms"
      CacheInvalidation__MaxBatchSize: "100"
      CacheInvalidation__EnableCoalescing: "true"
```

#### Development Settings (docker-compose.dev.yml)
```yaml
services:
  api:
    environment:
      # Batch Cache Invalidation configuration (development settings)
      CacheInvalidation__BatchingEnabled: "true"
      CacheInvalidation__BatchWindow: "50ms"  # Faster for development
      CacheInvalidation__MaxBatchSize: "50"   # Smaller batches for easier debugging
      CacheInvalidation__EnableCoalescing: "true"
```

## How It Works

1. **Request Queuing**: Cache invalidation requests are queued instead of being processed immediately
2. **Batch Triggers**: Batches are processed when either:
   - The batch window time expires (e.g., 100ms)
   - The batch size limit is reached (e.g., 100 items)
   - A critical priority event is received (e.g., key deletion)
3. **Coalescing**: If enabled, duplicate keys within a batch are removed
4. **Batch Processing**: All items in the batch are processed using Redis pipelining
5. **Multi-Instance Support**: Batch invalidations are published via Redis pub/sub

## Performance Benefits

- **Reduced Network Calls**: 100 individual Redis operations → 1 batch operation
- **Lower Latency**: Event processing from ~5ms → ~0.5ms per event
- **Better Resource Usage**: Fewer connections, less CPU for serialization
- **Automatic Deduplication**: Coalescing removes redundant operations

## Priority Levels

The system supports different priority levels for cache invalidation:

- **Critical**: Security/billing events (e.g., VirtualKeyDeleted) - processed immediately
- **High**: Active operation changes (e.g., key disabled, spend updates)
- **Normal**: Regular updates (e.g., model cost changes)
- **Low**: Non-critical updates

Critical priority events bypass batching and are processed immediately.

## Monitoring

The batch cache invalidation service provides comprehensive statistics:

- Total items queued
- Total items processed
- Total items coalesced (deduplicated)
- Average batch processing time
- Coalescing rate (percentage of duplicates removed)
- Per-cache-type statistics

## Tuning Guidelines

### High Throughput Scenarios
For systems processing thousands of events per minute:
```bash
CacheInvalidation__BatchWindow="200ms"
CacheInvalidation__MaxBatchSize="500"
CacheInvalidation__EnableCoalescing="true"
```

### Low Latency Requirements
For systems requiring minimal delay:
```bash
CacheInvalidation__BatchWindow="20ms"
CacheInvalidation__MaxBatchSize="20"
CacheInvalidation__EnableCoalescing="true"
```

### Development/Debugging
For easier debugging and observation:
```bash
CacheInvalidation__BatchWindow="50ms"
CacheInvalidation__MaxBatchSize="10"
CacheInvalidation__EnableCoalescing="false"
```

## Disabling Batch Processing

To disable batch processing and revert to individual invalidations:
```bash
CacheInvalidation__BatchingEnabled="false"
```

This might be useful for:
- Debugging cache invalidation issues
- Systems with very low event volumes
- During migration or testing

## Implementation Details

The batch cache invalidation system consists of:

1. **IBatchCacheInvalidationService**: Core service interface
2. **BatchCacheInvalidationService**: Background service implementation
3. **IRedisBatchOperations**: Redis pipeline operations
4. **IBatchInvalidatable**: Interface for caches supporting batch operations

Supported cache types:
- Virtual Key Cache
- Model Cost Cache
- Provider Cache
- Model Mapping Cache
- Global Settings Cache
- IP Filter Cache

## Related Documentation

- [Event-Driven Architecture](event-driven-architecture.md)
- [Redis Configuration](redis-configuration.md)
- [Performance Tuning](performance-tuning.md)