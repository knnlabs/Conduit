# Eventual Consistency in AsyncTask Service

## Overview

The `HybridAsyncTaskService` implements an eventual consistency model with self-healing mechanisms to ensure reliable task management while maintaining high performance and resilience to failures.

## Architecture

### Write Path (CreateTaskAsync)
1. **Database First** - Task is persisted to PostgreSQL/SQLite (critical operation)
2. **Best-Effort Cache** - Task status is cached in Redis with retry logic
3. **Best-Effort Events** - AsyncTaskCreated event is published for subscribers

### Read Path (GetTaskStatusAsync)
1. **Cache First** - Attempt to read from Redis cache
2. **Database Fallback** - On cache miss or failure, read from database
3. **Self-Healing** - Re-populate cache after database read

## Resilience Features

### 1. Database-First Approach
- Ensures data durability - the task is always persisted
- Database write is the only critical operation that can fail the request
- All other operations are best-effort

### 2. Cache Resilience
- **Retry Logic**: 3 attempts with exponential backoff (100ms, 200ms, 400ms)
- **Graceful Degradation**: Cache failures don't break the service
- **Self-Healing**: Cache misses automatically repopulate from database
- **Logging**: All cache failures are logged for monitoring

### 3. Event Publishing Resilience
- **Optional**: Service works without event bus
- **Non-Blocking**: Event failures don't affect task creation
- **Logged**: Failed events are logged for investigation

### 4. Read Path Self-Healing
- **Cache Failures**: Automatically fallback to database
- **Deserialization Errors**: Fallback to database on corrupt cache data
- **Re-caching**: Successful database reads update the cache
- **Consistency Monitoring**: Logs when cache has invalid data

## Consistency Guarantees

### What IS Guaranteed
1. **Durability**: Once CreateTaskAsync returns, the task exists in the database
2. **Read Consistency**: GetTaskStatusAsync always returns the latest data (from cache or database)
3. **Self-Healing**: System automatically recovers from cache inconsistencies

### What is NOT Guaranteed
1. **Immediate Cache Consistency**: Cache may lag behind database briefly
2. **Event Delivery**: Events may be lost if the bus is unavailable
3. **Cross-Service Consistency**: Other services may have stale data until events are processed

## Monitoring and Observability

### Key Log Messages
- `"Created async task {TaskId} in database"` - Task persisted successfully
- `"Failed to cache task {TaskId}, will self-heal on next read"` - Cache write failed
- `"Failed to publish AsyncTaskCreated event for task {TaskId}"` - Event publish failed
- `"Cache read failed for task {TaskId}, falling back to database"` - Cache read failed
- `"Task {TaskId} cache-database consistency issue detected"` - Cache had invalid data

### Metrics to Monitor
1. **Cache Hit Rate** - Should be high (>90%) under normal conditions
2. **Cache Operation Failures** - Should be rare
3. **Event Publishing Failures** - Indicates event bus issues
4. **Database Fallback Rate** - High rate indicates cache problems

## Trade-offs

### Advantages
- **High Availability**: Service remains operational despite cache/event failures
- **Performance**: Cache-first reads provide low latency
- **Simplicity**: No complex distributed transaction coordination
- **Self-Healing**: System recovers automatically from most failures

### Disadvantages
- **Eventual Consistency**: Brief periods where cache and database differ
- **Event Loss Possible**: Events are fire-and-forget
- **Additional Complexity**: More logging and monitoring required

## Best Practices

1. **Monitor Cache Health**: Set up alerts for high cache failure rates
2. **Event Bus Reliability**: Use durable queues for critical event flows
3. **Database Performance**: Ensure database can handle fallback load
4. **Operational Procedures**: Document how to diagnose consistency issues

## Future Enhancements

While the current approach is sufficient, these patterns could be considered if requirements change:

1. **Outbox Pattern**: Store events in database for guaranteed delivery
2. **Read-Through Cache**: Automatic cache population on miss
3. **Write-Through Cache**: Update cache and database together
4. **Distributed Tracing**: Correlate operations across services