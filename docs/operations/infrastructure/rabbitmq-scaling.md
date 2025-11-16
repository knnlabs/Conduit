# RabbitMQ and MassTransit Scaling Guide

## Overview

This guide provides production-ready configuration and operational procedures for scaling ConduitLLM to handle 1,000+ async tasks per minute using RabbitMQ and MassTransit.

## Target Capacity

- **Async Tasks**: 1,000 tasks/minute (~17/second average)
- **Webhook Bursts**: 50-100 webhooks/second (3,000-6,000/minute peak)
- **Service Instances**: 3-5 Core API instances
- **Message Ordering**: Maintained per virtual key through partitioning

## Production Configuration

### Environment Variables

```yaml
# docker-compose.yml
services:
  api:
    environment:
      # MassTransit Configuration
      CONDUITLLM__RABBITMQ__HOST: rabbitmq
      CONDUITLLM__RABBITMQ__PORT: 5672
      CONDUITLLM__RABBITMQ__USERNAME: conduit
      CONDUITLLM__RABBITMQ__PASSWORD: ${RABBITMQ_PASSWORD}
      CONDUITLLM__RABBITMQ__VHOST: /
      
      # Optimized for throughput with memory safety
      CONDUITLLM__RABBITMQ__PREFETCHCOUNT: 25
      CONDUITLLM__RABBITMQ__PARTITIONCOUNT: 30
      CONDUITLLM__RABBITMQ__CONCURRENTMESSAGELIMIT: 50
      
      # Connection pooling
      CONDUITLLM__RABBITMQ__MAXCONNECTIONS: 5
      CONDUITLLM__RABBITMQ__MINCONNECTIONS: 2
      
      # Advanced settings
      CONDUITLLM__RABBITMQ__REQUESTEDHEARTBEAT: 30
      CONDUITLLM__RABBITMQ__PUBLISHERCONFIRMATION: true
      CONDUITLLM__RABBITMQ__CHANNELMAX: 500
      
  admin:
    environment:
      # Same RabbitMQ settings as above
      CONDUITLLM__RABBITMQ__HOST: rabbitmq
      CONDUITLLM__RABBITMQ__PREFETCHCOUNT: 25
      CONDUITLLM__RABBITMQ__PARTITIONCOUNT: 30
```

### RabbitMQ Server Configuration

Create `/etc/rabbitmq/rabbitmq.conf`:

```ini
# Connection limits
listeners.tcp.default = 5672
management.tcp.port = 15672
channel_max = 2000
connection_max = 2000

# Memory management (critical for production)
vm_memory_high_watermark.relative = 0.6
vm_memory_high_watermark.absolute = 3GB
disk_free_limit.relative = 2.0

# Performance tuning
heartbeat = 30
frame_max = 131072
channel_operation_timeout = 15000

# Persistence and reliability
queue_master_locator = min-masters
```

### Docker Compose for RabbitMQ

```yaml
rabbitmq:
  image: rabbitmq:3.13-management
  hostname: rabbitmq
  environment:
    RABBITMQ_DEFAULT_USER: conduit
    RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    RABBITMQ_VM_MEMORY_HIGH_WATERMARK: 0.6
  ports:
    - "5672:5672"
    - "15672:15672"
  volumes:
    - rabbitmq-data:/var/lib/rabbitmq
    - ./rabbitmq.conf:/etc/rabbitmq/rabbitmq.conf:ro
  deploy:
    resources:
      limits:
        memory: 4G
      reservations:
        memory: 2G
```

## Configuration Rationale

### PrefetchCount: 25
- **Why not 50?** Memory calculation: 25 messages × 10KB average × 5 instances = 1.25MB base memory
- Prevents memory exhaustion while maintaining good throughput
- Allows for 125 concurrent messages across 5 instances

### PartitionCount: 30
- Provides 30 parallel processing streams
- Each partition handles ~2-3 messages/second
- Total capacity: 60-90 messages/second (meets requirement with headroom)

### ConcurrentMessageLimit: 50
- Prevents thread pool exhaustion
- Balances CPU utilization across cores
- Leaves headroom for other application tasks

### Connection Pooling: 5 connections
- Reduces connection establishment overhead
- Provides redundancy for connection failures
- Balanced for 3-5 service instances

## Queue-Specific Configuration

### Webhook Delivery (High Volume)
- **PrefetchCount**: 100 (higher for I/O-bound operations)
- **ConcurrentLimit**: 75
- **Features**: Quorum queue, circuit breaker, rate limiting
- **Queue Limit**: 50,000 messages with reject-publish overflow

### Video/Image Generation (CPU Intensive)
- **PrefetchCount**: 25 (default)
- **ConcurrentLimit**: 50 (default)
- **Features**: Single active consumer for ordering, circuit breaker
- **Retry**: Incremental with longer delays

### Spend Updates (Strict Ordering)
- **PrefetchCount**: 10 (low for sequential processing)
- **ConcurrentLimit**: 1 (ensures order)
- **Features**: Single active consumer, immediate retry
- **Queue Limit**: 10,000 messages

## Monitoring and Alerts

### Health Check Endpoints

```bash
# Basic health
curl http://localhost:5000/health

# RabbitMQ specific metrics
curl http://localhost:5000/health | jq '.checks[] | select(.name=="rabbitmq_comprehensive")'
```

### Key Metrics to Monitor

1. **Queue Depths**
   - Warning: > 1,000 messages
   - Critical: > 5,000 messages

2. **Memory Usage**
   - Warning: > 75% of high watermark
   - Critical: Memory alarm triggered

3. **Consumer Lag**
   - Track message age in queues
   - Alert if messages older than 5 minutes

4. **Connection Pool**
   - Monitor active vs idle connections
   - Alert if pool exhausted

### RabbitMQ Management API

Access metrics via: `http://rabbitmq-host:15672/api/overview`

```bash
# Get queue statistics
curl -u conduit:password http://localhost:15672/api/queues | jq '.[].messages'

# Monitor specific queue
curl -u conduit:password http://localhost:15672/api/queues/%2F/webhook-delivery
```

## Operational Procedures

### Scaling Up

1. **Increase Service Instances**
   ```bash
   docker-compose up -d --scale api=5
   ```

2. **Adjust Configuration**
   - Increase PartitionCount to 50 for more parallelism
   - Keep PrefetchCount conservative (25-30)
   - Monitor memory usage closely

### Handling High Load

1. **Temporary Burst Protection**
   ```yaml
   # Increase webhook prefetch temporarily
   CONDUITLLM__WEBHOOKPREFETCHCOUNT: 150
   ```

2. **Circuit Breaker Activation**
   - Monitors failure rates
   - Trips at 15-20% failure rate
   - Auto-resets after 5-10 minutes

### Troubleshooting

#### High Memory Usage
```bash
# Check RabbitMQ memory
rabbitmqctl status | grep memory

# Reduce prefetch if needed
docker-compose exec api bash -c "export CONDUITLLM__RABBITMQ__PREFETCHCOUNT=15"
docker-compose restart api
```

#### Queue Buildup
```bash
# Check queue depths
rabbitmqctl list_queues name messages consumers

# Increase consumers
docker-compose up -d --scale api=5
```

#### Connection Issues
```bash
# Check connection pool status
curl http://localhost:5000/health | jq '.checks[] | select(.name=="database_pool")'

# Reset connections
docker-compose restart api
```

## Performance Testing

### Load Test Script
```bash
#!/bin/bash
# Simulate 1000 tasks/minute

for i in {1..1000}; do
  curl -X POST http://localhost:5000/v1/webhook \
    -H "Authorization: Bearer $API_KEY" \
    -H "Content-Type: application/json" \
    -d '{"url":"https://example.com/hook","event":"test"}' &
  
  # Space out requests (60 seconds / 1000 = 60ms)
  sleep 0.06
done
```

### Monitoring During Load Test
```bash
# Watch queue depths
watch -n 1 'rabbitmqctl list_queues name messages messages_ready messages_unacknowledged'

# Monitor consumer performance
watch -n 1 'curl -s http://localhost:5000/health | jq ".checks[] | select(.name==\"rabbitmq_comprehensive\") | .data.queue_metrics"'
```

## Best Practices

1. **Memory Management**
   - Calculate: PrefetchCount × MessageSize × Instances
   - Leave 40% headroom for RabbitMQ operations
   - Monitor for memory alarms

2. **Connection Management**
   - Use connection pooling
   - Set appropriate heartbeat intervals
   - Monitor connection churn

3. **Error Handling**
   - Implement circuit breakers
   - Use exponential backoff for retries
   - Monitor dead letter queues

4. **Deployment**
   - Start with conservative settings
   - Scale gradually while monitoring
   - Use canary deployments for config changes

## Resource Requirements

### Per Core API Instance
- **Memory**: 512MB-1GB application + prefetch buffer
- **CPU**: 2-4 cores for optimal concurrency
- **Network**: 10-20 Mbps sustained

### RabbitMQ Server
- **Memory**: 4-8GB (depending on queue depths)
- **CPU**: 4-8 cores
- **Disk**: Fast SSD for message persistence
- **Network**: 100+ Mbps for high throughput

## Scaling Limits

### Current Configuration Supports:
- 1,000-1,500 async tasks/minute sustained
- 6,000 webhooks/minute burst
- 5 concurrent service instances
- 100,000 total queued messages

### To Scale Beyond:
1. Increase PartitionCount to 50-100
2. Add RabbitMQ cluster nodes
3. Implement sharding by virtual key
4. Consider Kafka for extreme scale (10,000+ msg/sec)