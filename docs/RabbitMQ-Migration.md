# RabbitMQ Migration for Horizontal Scaling

## Overview

Conduit now supports RabbitMQ as a message transport for true horizontal scaling across multiple Core API and Admin API instances. The system automatically detects RabbitMQ configuration and switches from in-memory to distributed messaging.

## Architecture

### Message Flow

```
Admin API (Publishers) → RabbitMQ Exchange → Topic Routing → Core API (Consumers)
```

### Exchange and Queue Design

- **Exchange**: `conduit-events` (topic exchange)
- **Queues**:
  - `virtual-key-events` - Handles virtual key updates, deletions, and spend events
  - `provider-events` - Handles provider credential updates and deletions
  - `image-generation` - Handles async image generation requests

### Routing Keys

- `virtual-key.updated` - Virtual key property changes
- `virtual-key.deleted` - Virtual key deletions
- `virtual-key.spend-requested` - Spend update requests
- `virtual-key.spend-updated` - Spend confirmations
- `provider.updated` - Provider credential changes
- `provider.deleted` - Provider deletions
- `provider.capabilities` - Model capability discoveries
- `image.requested` - Image generation requests
- `image.progress` - Progress updates
- `image.completed` - Completion notifications
- `image.failed` - Failure notifications

## Configuration

### Docker Compose

RabbitMQ service is pre-configured in `docker-compose.yml`:

```yaml
rabbitmq:
  image: rabbitmq:3-management-alpine
  environment:
    RABBITMQ_DEFAULT_USER: conduit
    RABBITMQ_DEFAULT_PASS: conduitpass
    RABBITMQ_DEFAULT_VHOST: /
  ports:
    - "5672:5672"   # AMQP port
    - "15672:15672" # Management UI
```

### Environment Variables

Configure both Core API and Admin API with:

```bash
CONDUITLLM__RABBITMQ__HOST=rabbitmq
CONDUITLLM__RABBITMQ__PORT=5672
CONDUITLLM__RABBITMQ__USERNAME=conduit
CONDUITLLM__RABBITMQ__PASSWORD=conduitpass
CONDUITLLM__RABBITMQ__VHOST=/
CONDUITLLM__RABBITMQ__PREFETCHCOUNT=10
CONDUITLLM__RABBITMQ__PARTITIONCOUNT=10
```

### Automatic Detection

The system automatically uses RabbitMQ when:
1. `CONDUITLLM__RABBITMQ__HOST` is set
2. Host is not "localhost" (indicating production deployment)

Otherwise, it falls back to in-memory transport for development.

## Deployment Scenarios

### Single Instance (Development)

No RabbitMQ configuration needed - uses in-memory transport:

```bash
docker compose up
```

### Multi-Instance (Production)

1. Ensure RabbitMQ environment variables are set
2. Scale services as needed:

```bash
# Scale Core API to 3 instances
docker compose up --scale api=3

# Scale Admin API to 2 instances  
docker compose up --scale admin=2
```

## Ordered Processing

Events are processed in order per entity through:

1. **Partition Keys**: Each event includes a partition key (e.g., VirtualKeyId)
2. **Single Consumer per Queue**: Each queue processes messages sequentially
3. **Prefetch=1**: Consumers process one message at a time

This prevents race conditions when multiple instances update the same entity.

## Health Monitoring

### RabbitMQ Health Check

The system includes automatic health checks for RabbitMQ connectivity:

```json
GET /health/ready

{
  "status": "Healthy",
  "checks": [
    {
      "name": "rabbitmq",
      "status": "Healthy",
      "description": "RabbitMQ connection established to rabbitmq:5672"
    }
  ]
}
```

### Management UI

Access RabbitMQ management at http://localhost:15672
- Username: conduit
- Password: conduitpass

Monitor:
- Queue depths
- Message rates
- Consumer connections
- Exchange bindings

## Migration Process

### From In-Memory to RabbitMQ

1. **No Code Changes Required** - Just set environment variables
2. **Automatic Queue Creation** - Queues and exchanges created on startup
3. **Graceful Degradation** - Services continue to work if RabbitMQ is unavailable

### Rollback

To rollback to in-memory transport:
1. Remove RabbitMQ environment variables
2. Restart services

## Performance Tuning

### Prefetch Count

Controls how many messages a consumer can process concurrently:
- Lower values (1-5): Better ordering guarantees, lower throughput
- Higher values (10-50): Better throughput, may process out of order

### Partition Count

Controls the number of logical partitions for event distribution:
- More partitions: Better parallelism for high-volume scenarios
- Fewer partitions: Simpler deployment, adequate for most use cases

### Connection Pool

RabbitMQ connections are pooled and reused:
- Heartbeat: 60 seconds (detects broken connections)
- Automatic recovery: Enabled (reconnects after network issues)

## Troubleshooting

### Connection Issues

Check logs for:
```
[Conduit] Event bus configured with RabbitMQ transport (multi-instance mode) - Host: rabbitmq:5672
```

If connection fails:
1. Verify RabbitMQ is running: `docker compose ps rabbitmq`
2. Check credentials match
3. Ensure network connectivity between services

### Message Processing

Monitor message processing:
```bash
# View queue details
docker compose exec rabbitmq rabbitmqctl list_queues

# Check consumer connections
docker compose exec rabbitmq rabbitmqctl list_consumers
```

### Performance Issues

If messages back up:
1. Scale Core API instances
2. Increase prefetch count
3. Check for slow event handlers

## Best Practices

1. **Keep Event Handlers Fast** - Avoid blocking operations
2. **Use Proper Logging** - Log event processing for debugging
3. **Monitor Queue Depths** - Set alerts for queue buildup
4. **Test Failover** - Verify system handles RabbitMQ restarts
5. **Secure Production** - Use strong passwords and TLS in production