# Distributed Monitoring System

This document describes the distributed monitoring system implemented to address GitHub issue #816, replacing in-memory storage with Redis-based centralized monitoring across multiple service instances.

## Overview

The distributed monitoring system provides:
- **Redis-based persistence** for metrics that survive service restarts
- **Cross-instance aggregation** for unified monitoring views
- **Alert deduplication** using distributed locks and fingerprinting
- **Graceful error handling** when Redis is unavailable
- **Backward compatibility** with existing monitoring interfaces

## Architecture

### Core Components

1. **DistributedPerformanceMonitoringService** - Redis-based performance metrics
2. **DistributedAlertManagementService** - Alert deduplication and distributed management
3. **DistributedSignalRMetricsService** - Centralized SignalR connection tracking

### Redis Data Structures

- **Streams**: Time-series metrics storage (`request_metrics_stream`, `database_ops_stream`)
- **Hashes**: Aggregated endpoint and connection pool metrics
- **Sets**: Active service instance tracking
- **Locks**: Distributed alert deduplication using unique fingerprints

## Configuration

### Environment Variables

The system automatically uses existing Redis configuration:

```bash
# Option 1: Redis URL (recommended)
REDIS_URL=redis://redis:6379

# Option 2: Legacy connection string
CONDUIT_REDIS_CONNECTION_STRING=redis:6379

# Redis instance name for namespacing
CONDUIT_REDIS_INSTANCE_NAME=conduit:
```

### Development Setup

Redis is already configured and running in the development environment:
- **Container**: `conduit-redis-1`
- **Port**: `6379`
- **Health**: Monitored with health checks

### Dependency Injection

The system is automatically registered when Redis is available:

```csharp
// Distributed services are registered as both distributed and original interfaces
services.AddSingleton<IDistributedPerformanceMonitoringService, DistributedPerformanceMonitoringService>();
services.AddSingleton<IPerformanceMonitoringService>(provider => 
    provider.GetRequiredService<IDistributedPerformanceMonitoringService>());
```

## Error Handling

### Redis Unavailable

When Redis is unavailable:
- **Metric recording methods** log errors but don't throw exceptions
- **Services continue to function** without distributed features
- **Heartbeat updates** fail gracefully with logged warnings
- **Alert processing** continues locally with deduplication disabled

### Connection Recovery

- Services automatically reconnect when Redis becomes available
- Instance heartbeats resume normal operation
- Metrics aggregation resumes with fresh data

## Features

### Alert Deduplication

- **Fingerprint-based**: Alerts are deduplicated using SHA256 fingerprints
- **Distributed locks**: Prevent race conditions across multiple instances
- **Timeout handling**: Locks expire after 5 minutes to prevent deadlocks
- **Occurrence counting**: Duplicate alerts increment occurrence counters

### Instance Management

- **Automatic registration**: Services register themselves on startup
- **Heartbeat monitoring**: Regular heartbeats with 2-minute expiration
- **Cleanup**: Stale instances are automatically removed
- **Unique identification**: `{MachineName}_{ProcessId}_{UniqueId}` format

### Metrics Aggregation

- **Cross-instance**: Combines metrics from all active service instances
- **Real-time**: Redis streams provide real-time metric updates
- **Retention**: Configurable retention periods for historical data
- **Performance**: Atomic operations using Lua scripts for consistency

## Monitoring and Observability

### Prometheus Integration

All distributed services expose Prometheus metrics:
- `conduit_signalr_connections_active_distributed`
- `conduit_signalr_connections_total_distributed`
- Performance metrics with instance labels

### Logging

Comprehensive logging at appropriate levels:
- **ERROR**: Redis operation failures, critical issues
- **WARNING**: New alerts, configuration issues
- **DEBUG**: Alert deduplication, heartbeat updates

## Migration from In-Memory

### Automatic Migration

No manual migration is required:
1. Original interfaces are preserved for backward compatibility
2. Distributed services implement original interfaces
3. Existing code continues to work unchanged
4. Redis provides immediate persistence and cross-instance visibility

### Benefits

- **Data persistence**: Metrics survive service restarts
- **Consistency**: Unified view across all service instances
- **Scalability**: Supports horizontal scaling without data loss
- **Reliability**: Distributed alert deduplication prevents spam

## Troubleshooting

### Common Issues

1. **Redis Connection Failures**
   - Check `REDIS_URL` or `CONDUIT_REDIS_CONNECTION_STRING` configuration
   - Verify Redis container is running and accessible
   - Check firewall settings and network connectivity

2. **Missing Metrics**
   - Verify services are registered and started
   - Check instance heartbeats in Redis: `redis-cli SMEMBERS perf_monitoring_instances`
   - Review error logs for Redis operation failures

3. **Duplicate Alerts**
   - Confirm alert deduplication is working: check Redis locks
   - Verify fingerprint generation is consistent
   - Check distributed lock expiration settings

### Debug Commands

```bash
# Check active instances
redis-cli SMEMBERS perf_monitoring_instances

# View recent metrics
redis-cli XRANGE request_metrics_stream - + COUNT 10

# Check alert locks
redis-cli KEYS alert_lock:*

# Monitor real-time operations
redis-cli MONITOR
```

## Performance Considerations

- **Memory usage**: Redis stores metrics with configurable retention
- **Network overhead**: Minimal due to efficient Redis operations
- **Latency**: Sub-millisecond impact on metric recording
- **Throughput**: Supports thousands of metrics per second per instance

## Security

- Uses existing Redis security configuration
- No additional authentication required
- Data is stored in plaintext (appropriate for internal metrics)
- Network security follows existing Redis deployment patterns