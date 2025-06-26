---
sidebar_position: 4
title: RabbitMQ Setup
description: Configure RabbitMQ for high-throughput event processing in Conduit
---

# RabbitMQ Setup

RabbitMQ is essential for Conduit's event-driven architecture, enabling horizontal scaling and high-throughput processing of 1,000+ async tasks per minute.

## Why RabbitMQ?

Conduit uses RabbitMQ for:
- **Event Processing**: Domain events for data consistency
- **Async Task Processing**: Image/video generation, webhook delivery
- **Horizontal Scaling**: Multiple service instances coordination
- **Reliability**: Message durability and delivery guarantees

## RabbitMQ vs In-Memory

| Feature | In-Memory | RabbitMQ |
|---------|-----------|----------|
| **Deployment** | Single instance only | Multi-instance |
| **Throughput** | Limited | 1,000+ tasks/minute |
| **Reliability** | Lost on restart | Persistent |
| **Scaling** | Vertical only | Horizontal |
| **Use Case** | Development/Testing | Production |

## Installation Options

### Option 1: Docker Compose (Recommended for Development)

```yaml
# docker-compose.yml
version: '3.8'
services:
  rabbitmq:
    image: rabbitmq:3.12-management
    container_name: conduit-rabbitmq
    ports:
      - "5672:5672"    # AMQP port
      - "15672:15672"  # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: conduit
      RABBITMQ_DEFAULT_PASS: conduit-password
      RABBITMQ_DEFAULT_VHOST: /
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
      - ./rabbitmq.conf:/etc/rabbitmq/rabbitmq.conf
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  rabbitmq_data:
```

### Option 2: Kubernetes Deployment

```yaml
# kubernetes/rabbitmq-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: rabbitmq
  namespace: conduit-production
spec:
  replicas: 1
  selector:
    matchLabels:
      app: rabbitmq
  template:
    metadata:
      labels:
        app: rabbitmq
    spec:
      containers:
      - name: rabbitmq
        image: rabbitmq:3.12-management
        ports:
        - containerPort: 5672
        - containerPort: 15672
        env:
        - name: RABBITMQ_DEFAULT_USER
          value: "conduit"
        - name: RABBITMQ_DEFAULT_PASS
          valueFrom:
            secretKeyRef:
              name: rabbitmq-secret
              key: password
        volumeMounts:
        - name: rabbitmq-config
          mountPath: /etc/rabbitmq/rabbitmq.conf
          subPath: rabbitmq.conf
        - name: rabbitmq-data
          mountPath: /var/lib/rabbitmq
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
      volumes:
      - name: rabbitmq-config
        configMap:
          name: rabbitmq-config
      - name: rabbitmq-data
        persistentVolumeClaim:
          claimName: rabbitmq-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: rabbitmq-service
  namespace: conduit-production
spec:
  selector:
    app: rabbitmq
  ports:
  - name: amqp
    port: 5672
    targetPort: 5672
  - name: management
    port: 15672
    targetPort: 15672
  type: ClusterIP
```

### Option 3: Managed RabbitMQ Services

**AWS Amazon MQ:**
```bash
# AWS CLI command to create managed RabbitMQ
aws mq create-broker \
  --broker-name conduit-production \
  --deployment-mode SINGLE_INSTANCE \
  --engine-type RABBITMQ \
  --engine-version 3.12.13 \
  --host-instance-type mq.t3.micro \
  --users Username=conduit,Password=secure-password
```

**Google Cloud Pub/Sub Alternative:**
```bash
# Create Pub/Sub topics for event processing
gcloud pubsub topics create conduit-events
gcloud pubsub subscriptions create conduit-subscription --topic=conduit-events
```

## Production Configuration

### RabbitMQ Configuration File

```ini
# rabbitmq.conf - Production optimized settings
# Memory and disk limits
vm_memory_high_watermark.relative = 0.6
disk_free_limit.relative = 1.0

# Connection limits
num_acceptors.tcp = 10
handshake_timeout = 10000
heartbeat = 60

# Queue settings
default_user = conduit
default_pass = secure-password
default_vhost = /
default_user_tags.administrator = true

# Logging
log.console = true
log.console.level = info

# Clustering (for multi-node setup)
cluster_formation.peer_discovery_backend = rabbit_peer_discovery_classic_config
cluster_formation.classic_config.nodes.1 = rabbit@rabbitmq-1
cluster_formation.classic_config.nodes.2 = rabbit@rabbitmq-2
cluster_formation.classic_config.nodes.3 = rabbit@rabbitmq-3

# Performance tuning
channel_max = 2048
frame_max = 131072
heartbeat = 60
```

### Conduit RabbitMQ Configuration

```bash
# Core API and Admin API RabbitMQ Configuration
export CONDUITLLM__RABBITMQ__HOST=rabbitmq-service
export CONDUITLLM__RABBITMQ__PORT=5672
export CONDUITLLM__RABBITMQ__USERNAME=conduit
export CONDUITLLM__RABBITMQ__PASSWORD=secure-password
export CONDUITLLM__RABBITMQ__VHOST=/

# High-throughput settings (1,000+ tasks/minute)
export CONDUITLLM__RABBITMQ__PREFETCHCOUNT=25
export CONDUITLLM__RABBITMQ__PARTITIONCOUNT=30
export CONDUITLLM__RABBITMQ__CONCURRENTMESSAGELIMIT=50

# Connection pooling
export CONDUITLLM__RABBITMQ__MAXCONNECTIONS=5
export CONDUITLLM__RABBITMQ__MINCONNECTIONS=2

# Reliability settings
export CONDUITLLM__RABBITMQ__REQUESTEDHEARTBEAT=30
export CONDUITLLM__RABBITMQ__PUBLISHERCONFIRMATION=true
```

## Queue Architecture

### Queue Structure

Conduit creates the following queues automatically:

```
Exchanges:
├── conduit.domain-events (Topic)
├── conduit.webhook-delivery (Direct) 
├── conduit.image-generation (Direct)
├── conduit.video-generation (Direct)
└── conduit.audio-processing (Direct)

Queues:
├── conduit.virtual-key-events (Partitioned: 30 partitions)
├── conduit.provider-events (Partitioned: 10 partitions)
├── conduit.spend-updates (Partitioned: 30 partitions)
├── conduit.webhook-delivery (Quorum queue)
├── conduit.image-generation (Standard)
├── conduit.video-generation (Standard)
└── conduit.audio-processing (Standard)
```

### Queue-Specific Configuration

**Webhook Delivery Queue:**
```bash
# High-throughput webhook configuration
CONDUITLLM__RABBITMQ__WEBHOOK__PREFETCHCOUNT=100
CONDUITLLM__RABBITMQ__WEBHOOK__CONCURRENTMESSAGELIMIT=75
CONDUITLLM__RABBITMQ__WEBHOOK__BATCHSIZE=100
```

**Image/Video Generation Queues:**
```bash
# Media generation configuration
CONDUITLLM__RABBITMQ__MEDIA__PREFETCHCOUNT=10
CONDUITLLM__RABBITMQ__MEDIA__CONCURRENTMESSAGELIMIT=5
CONDUITLLM__RABBITMQ__MEDIA__CIRCUITBREAKER=true
```

**Spend Update Queue:**
```bash
# Ordered spend processing
CONDUITLLM__RABBITMQ__SPEND__PREFETCHCOUNT=10
CONDUITLLM__RABBITMQ__SPEND__CONCURRENTMESSAGELIMIT=1
CONDUITLLM__RABBITMQ__SPEND__ORDEREDPROCESSING=true
```

## Performance Optimization

### Connection Pooling

```csharp
// Conduit automatically configures connection pooling
services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(host, "/", h =>
        {
            h.Username(username);
            h.Password(password);
            h.RequestedHeartbeat(TimeSpan.FromSeconds(30));
            h.RequestedConnectionTimeout(TimeSpan.FromSeconds(30));
            h.UseCluster(c =>
            {
                c.Node("rabbitmq-1");
                c.Node("rabbitmq-2");
                c.Node("rabbitmq-3");
            });
        });
        
        cfg.ConcurrentMessageLimit = 50;
        cfg.PrefetchCount = 25;
    });
});
```

### Circuit Breaker Configuration

```bash
# Circuit breaker settings
CONDUITLLM__RABBITMQ__CIRCUITBREAKER__ENABLED=true
CONDUITLLM__RABBITMQ__CIRCUITBREAKER__THRESHOLD=0.15
CONDUITLLM__RABBITMQ__CIRCUITBREAKER__DURATION=60000
CONDUITLLM__RABBITMQ__CIRCUITBREAKER__SAMPLING=10000
```

### Rate Limiting

```bash
# Rate limiting per consumer type
CONDUITLLM__RABBITMQ__RATELIMIT__WEBHOOK=100     # 100 msg/sec
CONDUITLLM__RABBITMQ__RATELIMIT__MEDIA=10        # 10 msg/sec
CONDUITLLM__RABBITMQ__RATELIMIT__SPEND=50        # 50 msg/sec
```

## Monitoring and Health Checks

### Health Check Configuration

```csharp
// Health checks for RabbitMQ
services.AddHealthChecks()
    .AddRabbitMQ(connectionString: rabbitMqConnection, 
                 name: "rabbitmq",
                 failureStatus: HealthStatus.Unhealthy,
                 tags: new[] { "messaging", "rabbitmq" });
```

### Key Metrics to Monitor

```bash
# Queue depth monitoring
rabbitmqctl list_queues name messages consumers

# Connection monitoring  
rabbitmqctl list_connections name state

# Memory usage
rabbitmqctl status | grep -i memory

# Performance metrics
rabbitmqctl list_queues name message_stats
```

### Prometheus Metrics

```yaml
# RabbitMQ Prometheus Plugin
rabbitmq-plugins enable rabbitmq_prometheus

# Metrics endpoint available at:
# http://rabbitmq:15692/metrics
```

**Key Metrics:**
- `rabbitmq_queue_messages` - Queue depth
- `rabbitmq_queue_messages_ready` - Ready messages
- `rabbitmq_queue_messages_unacknowledged` - Processing messages
- `rabbitmq_connections` - Active connections
- `rabbitmq_channels` - Active channels

## Scaling Strategies

### Vertical Scaling

```yaml
# Increase RabbitMQ resources
resources:
  requests:
    memory: "1Gi"
    cpu: "500m"
  limits:
    memory: "2Gi"
    cpu: "1000m"

# Increase connection limits
CONDUITLLM__RABBITMQ__MAXCONNECTIONS=10
CONDUITLLM__RABBITMQ__CONCURRENTMESSAGELIMIT=100
```

### Horizontal Scaling

```yaml
# Scale Conduit service replicas
apiVersion: apps/v1
kind: Deployment
metadata:
  name: conduit-core-api
spec:
  replicas: 5  # Increased from 3
```

### RabbitMQ Clustering

```yaml
# Multi-node RabbitMQ cluster
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: rabbitmq-cluster
spec:
  replicas: 3
  serviceName: rabbitmq-cluster
  template:
    spec:
      containers:
      - name: rabbitmq
        image: rabbitmq:3.12-management
        env:
        - name: RABBITMQ_ERLANG_COOKIE
          value: "rabbitmq-cluster-cookie"
        - name: RABBITMQ_USE_LONGNAME
          value: "true"
```

## Troubleshooting

### Common Issues

**Queue Buildup:**
```bash
# Check queue consumers
rabbitmqctl list_queues name consumers

# Purge queue if needed (development only)
rabbitmqctl purge_queue conduit.webhook-delivery

# Increase consumer count
export CONDUITLLM__RABBITMQ__CONCURRENTMESSAGELIMIT=100
```

**Connection Issues:**
```bash
# Check RabbitMQ logs
docker logs conduit-rabbitmq

# Verify connectivity
telnet rabbitmq-host 5672

# Check authentication
rabbitmqctl authenticate_user conduit password
```

**Performance Issues:**
```bash
# Check memory usage
rabbitmqctl status | grep -A5 -B5 memory

# Monitor message rates
rabbitmqctl list_queues name message_stats

# Check connection limits
rabbitmqctl list_connections name state
```

### Debug Logging

```bash
# Enable debug logging in Conduit
export CONDUITLLM__LOGGING__LOGLEVEL=Debug

# Enable RabbitMQ debug logging
rabbitmqctl set_log_level debug
```

## Security Configuration

### Authentication and Authorization

```bash
# Create dedicated user for Conduit
rabbitmqctl add_user conduit secure-password
rabbitmqctl set_user_tags conduit administrator
rabbitmqctl set_permissions -p / conduit ".*" ".*" ".*"

# Remove default guest user
rabbitmqctl delete_user guest
```

### TLS/SSL Configuration

```bash
# Enable TLS in RabbitMQ
export CONDUITLLM__RABBITMQ__SSL__ENABLED=true
export CONDUITLLM__RABBITMQ__SSL__SERVERNAME=rabbitmq.yourdomain.com
export CONDUITLLM__RABBITMQ__SSL__CERTIFICATEPATH=/etc/ssl/certs/rabbitmq.crt
export CONDUITLLM__RABBITMQ__SSL__CERTIFICATEPASSWORD=cert-password
```

## Backup and Recovery

### Configuration Backup

```bash
# Export RabbitMQ definitions
rabbitmqctl export_definitions backup.json

# Restore definitions
rabbitmqctl import_definitions backup.json
```

### Data Backup

```bash
# Backup RabbitMQ data directory
tar -czf rabbitmq-backup-$(date +%Y%m%d).tar.gz /var/lib/rabbitmq/

# Restore from backup
tar -xzf rabbitmq-backup-20240101.tar.gz -C /
```

## Migration from In-Memory

### Automatic Detection

Conduit automatically detects RabbitMQ configuration:

```bash
# When RabbitMQ is configured, Conduit switches automatically
[Conduit] Event bus configured with RabbitMQ transport (multi-instance mode)

# When not configured, uses in-memory
[Conduit] Event bus configured with in-memory transport (single-instance mode)
```

### Migration Steps

1. **Deploy RabbitMQ** using one of the installation options
2. **Update Configuration** with RabbitMQ connection details
3. **Restart Services** - Conduit will automatically detect and use RabbitMQ
4. **Verify Operation** - Check logs for successful RabbitMQ connection
5. **Monitor Performance** - Ensure queues are processing correctly

## Next Steps

- **Scaling Configuration**: Learn about [advanced scaling strategies](scaling-configuration)
- **Monitoring**: Set up [comprehensive monitoring](monitoring-health)
- **Production Deployment**: Complete [production setup](production-deployment)
- **Performance Tuning**: Optimize for your specific workload