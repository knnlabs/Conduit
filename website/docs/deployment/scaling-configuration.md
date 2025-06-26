---
sidebar_position: 3
title: Scaling Configuration
description: Configure Conduit for horizontal and vertical scaling to handle high-throughput workloads
---

# Scaling Configuration

This guide covers scaling Conduit to handle high-throughput workloads, from hundreds to thousands of requests per minute, with horizontal and vertical scaling strategies.

## Scaling Overview

Conduit supports multiple scaling approaches:

- **Vertical Scaling**: Increase resources for existing instances
- **Horizontal Scaling**: Add more service instances
- **Database Scaling**: Connection pooling and read replicas
- **Event Bus Scaling**: RabbitMQ clustering and partitioning
- **Cache Scaling**: Redis clustering and optimization

## Performance Targets

| Throughput Level | Requests/Minute | Scaling Strategy |
|-----------------|-----------------|------------------|
| **Small** | < 100 | Single instance |
| **Medium** | 100-1,000 | Vertical scaling |
| **Large** | 1,000-10,000 | Horizontal scaling |
| **Enterprise** | 10,000+ | Full clustering |

## Horizontal Scaling Configuration

### Core API Scaling

```yaml
# kubernetes/core-api-hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: conduit-core-api-hpa
  namespace: conduit-production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: conduit-core-api
  minReplicas: 3
  maxReplicas: 20
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  - type: Pods
    pods:
      metric:
        name: http_requests_per_second
      target:
        type: AverageValue
        averageValue: "100"
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 25
        periodSeconds: 60
```

### Admin API Scaling

```yaml
# kubernetes/admin-api-hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: conduit-admin-api-hpa
  namespace: conduit-production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: conduit-admin-api
  minReplicas: 2
  maxReplicas: 5
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

### Load Balancer Configuration

```nginx
# nginx-scaled.conf
upstream conduit_core_api {
    least_conn;
    
    # Primary instances
    server conduit-core-api-1:5000 max_fails=3 fail_timeout=30s weight=10;
    server conduit-core-api-2:5000 max_fails=3 fail_timeout=30s weight=10;
    server conduit-core-api-3:5000 max_fails=3 fail_timeout=30s weight=10;
    
    # Auto-scaled instances (added/removed dynamically)
    server conduit-core-api-4:5000 max_fails=3 fail_timeout=30s weight=10 backup;
    server conduit-core-api-5:5000 max_fails=3 fail_timeout=30s weight=10 backup;
    
    # Connection settings for high throughput
    keepalive 32;
    keepalive_requests 1000;
    keepalive_timeout 60s;
}

server {
    listen 443 ssl http2;
    server_name api.conduit.yourdomain.com;
    
    # Connection limits for scaling
    limit_conn_zone $binary_remote_addr zone=addr:10m;
    limit_req_zone $binary_remote_addr zone=api:10m rate=100r/s;
    
    location /v1/ {
        limit_conn addr 10;
        limit_req zone=api burst=50 nodelay;
        
        proxy_pass http://conduit_core_api;
        proxy_http_version 1.1;
        proxy_set_header Connection "";
        
        # Optimized for high throughput
        proxy_buffering on;
        proxy_buffer_size 4k;
        proxy_buffers 8 4k;
        proxy_busy_buffers_size 8k;
        
        # Connection pooling
        proxy_socket_keepalive on;
        proxy_connect_timeout 5s;
        proxy_send_timeout 10s;
        proxy_read_timeout 30s;
    }
}
```

## Vertical Scaling Configuration

### Resource Allocation

```yaml
# Baseline configuration (100-500 req/min)
resources:
  requests:
    memory: "512Mi"
    cpu: "500m"
  limits:
    memory: "1Gi"
    cpu: "1000m"

# Medium load configuration (500-2000 req/min)  
resources:
  requests:
    memory: "1Gi"
    cpu: "1000m"
  limits:
    memory: "2Gi"
    cpu: "2000m"

# High load configuration (2000+ req/min)
resources:
  requests:
    memory: "2Gi"
    cpu: "2000m"
  limits:
    memory: "4Gi"
    cpu: "4000m"
```

### JVM/Runtime Optimization

```bash
# .NET Runtime optimization for high throughput
DOTNET_gcServer=1
DOTNET_gcConcurrent=1
DOTNET_gcRetainVM=1
DOTNET_ThreadPool_ForceMinWorkerThreads=100
DOTNET_ThreadPool_ForceMaxWorkerThreads=1000

# Garbage collection tuning
DOTNET_GCHeapHardLimit=2147483648  # 2GB heap limit
DOTNET_GCHighMemPercent=90
DOTNET_GCConserveMemory=5
```

## Database Scaling

### Connection Pool Configuration

```bash
# High-throughput database configuration
CONDUITLLM__DATABASE__MAXPOOLSIZE=200
CONDUITLLM__DATABASE__MINPOOLSIZE=20
CONDUITLLM__DATABASE__CONNECTIONTIMEOUT=30
CONDUITLLM__DATABASE__COMMANDTIMEOUT=30
CONDUITLLM__DATABASE__CONNECTIONLIFETIME=300
CONDUITLLM__DATABASE__CONNECTIONIDLETIMEOUT=60

# Database-specific optimizations
CONDUITLLM__DATABASE__INCLUDEERRORDETAIL=false
CONDUITLLM__DATABASE__ENABLESERVICEPROVIDERVALIDATION=false
CONDUITLLM__DATABASE__ENABLESENSITIVEDATALOGGING=false
```

### PostgreSQL Scaling Configuration

```sql
-- PostgreSQL performance tuning for high concurrency
ALTER SYSTEM SET max_connections = 400;
ALTER SYSTEM SET shared_buffers = '512MB';
ALTER SYSTEM SET effective_cache_size = '2GB';
ALTER SYSTEM SET work_mem = '8MB';
ALTER SYSTEM SET maintenance_work_mem = '128MB';
ALTER SYSTEM SET checkpoint_completion_target = 0.9;
ALTER SYSTEM SET wal_buffers = '16MB';
ALTER SYSTEM SET default_statistics_target = 100;
ALTER SYSTEM SET random_page_cost = 1.1;
ALTER SYSTEM SET effective_io_concurrency = 200;

-- Connection and query optimization
ALTER SYSTEM SET log_min_duration_statement = 1000;  -- Log slow queries
ALTER SYSTEM SET log_lock_waits = on;
ALTER SYSTEM SET deadlock_timeout = 1000;

SELECT pg_reload_conf();
```

### Read Replica Configuration

```bash
# Read replica for analytics and reporting
CONDUITLLM__DATABASE__READREPLICA__CONNECTIONSTRING=postgresql://conduit:password@postgres-read:5432/conduit
CONDUITLLM__DATABASE__READREPLICA__ENABLED=true
CONDUITLLM__DATABASE__READREPLICA__OPERATIONS=Analytics,Reporting,HealthChecks
```

## Redis Scaling

### Redis Cluster Configuration

```bash
# Redis cluster for high availability and scaling
REDIS_URL=redis://redis-cluster:6379/0
REDIS_URL_SIGNALR=redis://redis-cluster:6379/2

# Connection pool settings
CONDUITLLM__REDIS__CONNECTIONSTRING=redis-cluster:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000,connectRetry=3
CONDUITLLM__REDIS__COMMANDTIMEOUT=5000
CONDUITLLM__REDIS__CONNECTTIMEOUT=5000
CONDUITLLM__REDIS__RETRYDELAY=1000

# Connection pooling
CONDUITLLM__REDIS__CONNECTIONPOOLSIZE=50
CONDUITLLM__REDIS__CONNECTIONMULTIPLEXER=true
```

### Cache Configuration for Scale

```bash
# Cache TTL optimization for high throughput
CONDUITLLM__CACHE__VIRTUALKEY__TTL=300      # 5 minutes
CONDUITLLM__CACHE__PROVIDER__TTL=3600       # 1 hour  
CONDUITLLM__CACHE__MODEL__TTL=7200          # 2 hours
CONDUITLLM__CACHE__NAVIGATION__TTL=300      # 5 minutes

# Cache size limits
CONDUITLLM__CACHE__MAXMEMORY=1073741824     # 1GB
CONDUITLLM__CACHE__MAXMEMORYPOLICY=allkeys-lru
CONDUITLLM__CACHE__MAXENTRIES=1000000       # 1M entries
```

## RabbitMQ Scaling

### High-Throughput RabbitMQ Configuration

```bash
# Core scaling configuration
CONDUITLLM__RABBITMQ__PREFETCHCOUNT=50           # Increased from 25
CONDUITLLM__RABBITMQ__PARTITIONCOUNT=50          # Increased from 30  
CONDUITLLM__RABBITMQ__CONCURRENTMESSAGELIMIT=100 # Increased from 50

# Connection pooling for scale
CONDUITLLM__RABBITMQ__MAXCONNECTIONS=10          # Increased from 5
CONDUITLLM__RABBITMQ__MINCONNECTIONS=5           # Increased from 2
CONDUITLLM__RABBITMQ__CONNECTIONPOOLSIZE=20

# Queue-specific scaling
CONDUITLLM__RABBITMQ__WEBHOOK__PREFETCHCOUNT=200
CONDUITLLM__RABBITMQ__WEBHOOK__CONCURRENTMESSAGELIMIT=150
CONDUITLLM__RABBITMQ__WEBHOOK__BATCHSIZE=200

CONDUITLLM__RABBITMQ__MEDIA__PREFETCHCOUNT=20
CONDUITLLM__RABBITMQ__MEDIA__CONCURRENTMESSAGELIMIT=10
CONDUITLLM__RABBITMQ__MEDIA__PARALLELPROCESSING=true
```

### RabbitMQ Cluster Configuration

```yaml
# kubernetes/rabbitmq-cluster.yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: rabbitmq-cluster
  namespace: conduit-production
spec:
  serviceName: rabbitmq-cluster
  replicas: 3
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
        env:
        - name: RABBITMQ_ERLANG_COOKIE
          value: "conduit-cluster-cookie"
        - name: RABBITMQ_USE_LONGNAME
          value: "true"
        - name: RABBITMQ_NODENAME
          value: "rabbit@$(HOSTNAME).rabbitmq-cluster.conduit-production.svc.cluster.local"
        - name: RABBITMQ_DEFAULT_USER
          value: "conduit"
        - name: RABBITMQ_DEFAULT_PASS
          valueFrom:
            secretKeyRef:
              name: rabbitmq-secret
              key: password
        ports:
        - containerPort: 5672
        - containerPort: 15672
        - containerPort: 25672
        volumeMounts:
        - name: rabbitmq-data
          mountPath: /var/lib/rabbitmq
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
  volumeClaimTemplates:
  - metadata:
      name: rabbitmq-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 20Gi
```

## HTTP Client Scaling

### Connection Pool Optimization

```csharp
// HTTP client configuration for high throughput
services.Configure<HttpClientSettings>(options =>
{
    options.MaxConnectionsPerServer = 100;      // Increased from 50
    options.ConnectionLifetime = TimeSpan.FromMinutes(5);
    options.PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2);
    options.RequestTimeout = TimeSpan.FromSeconds(30);
    options.KeepAlivePingDelay = TimeSpan.FromSeconds(30);
    options.KeepAlivePingTimeout = TimeSpan.FromSeconds(20);
    options.EnableHttp2 = true;
    options.Http2MaxStreamsPerConnection = 100;
});
```

### Provider-Specific Scaling

```bash
# Provider connection limits
CONDUITLLM__PROVIDERS__OPENAI__MAXCONNECTIONS=50
CONDUITLLM__PROVIDERS__ANTHROPIC__MAXCONNECTIONS=30
CONDUITLLM__PROVIDERS__GOOGLE__MAXCONNECTIONS=40

# Rate limiting per provider
CONDUITLLM__PROVIDERS__OPENAI__RATELIMIT=1000    # req/min
CONDUITLLM__PROVIDERS__ANTHROPIC__RATELIMIT=500  # req/min
CONDUITLLM__PROVIDERS__GOOGLE__RATELIMIT=800     # req/min

# Circuit breaker settings
CONDUITLLM__CIRCUITBREAKER__FAILURETHRESHOLD=0.1  # 10% failure rate
CONDUITLLM__CIRCUITBREAKER__TIMEOUT=60000         # 60s timeout
CONDUITLLM__CIRCUITBREAKER__RETRYDELAY=5000       # 5s retry delay
```

## Auto-Scaling Triggers

### Custom Metrics Auto-Scaling

```yaml
# kubernetes/custom-metrics-hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: conduit-custom-metrics-hpa
  namespace: conduit-production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: conduit-core-api
  minReplicas: 3
  maxReplicas: 50
  metrics:
  - type: External
    external:
      metric:
        name: rabbitmq_queue_messages
        selector:
          matchLabels:
            queue: "conduit.webhook-delivery"
      target:
        type: Value
        value: "500"
  - type: External
    external:
      metric:
        name: http_requests_per_second
      target:
        type: Value
        value: "100"
  - type: External
    external:
      metric:
        name: database_connection_pool_utilization
      target:
        type: Value
        value: "70"
```

### KEDA Auto-Scaling

```yaml
# keda/rabbitmq-scaler.yaml
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: conduit-rabbitmq-scaler
  namespace: conduit-production
spec:
  scaleTargetRef:
    name: conduit-core-api
  minReplicaCount: 3
  maxReplicaCount: 20
  triggers:
  - type: rabbitmq
    metadata:
      host: "amqp://conduit:password@rabbitmq-service:5672/"
      queueName: "conduit.webhook-delivery"
      queueLength: "10"
  - type: prometheus
    metadata:
      serverAddress: http://prometheus:9090
      metricName: http_requests_per_second
      threshold: "100"
      query: rate(http_requests_total[1m])
```

## Performance Optimization

### Application-Level Optimizations

```csharp
// Startup.cs optimizations for high throughput
public void ConfigureServices(IServiceCollection services)
{
    // Connection pooling
    services.Configure<KestrelServerOptions>(options =>
    {
        options.Limits.MaxConcurrentConnections = 1000;
        options.Limits.MaxConcurrentUpgradedConnections = 1000;
        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
        options.Limits.MinRequestBodyDataRate = null;
        options.Limits.MinResponseDataRate = null;
    });
    
    // Thread pool optimization
    ThreadPool.SetMinThreads(100, 100);
    ThreadPool.SetMaxThreads(1000, 1000);
    
    // JSON serialization optimization
    services.Configure<JsonOptions>(options =>
    {
        options.SerializerOptions.DefaultBufferSize = 65536;
    });
    
    // HTTP client optimization
    services.AddHttpClient()
        .ConfigureHttpClientDefaults(builder =>
        {
            builder.UseSocketsHttpHandler()
                .ConfigureHttpHandlerHttpPool(pool =>
                {
                    pool.MaxConnectionsPerServer = 100;
                    pool.ConnectionLifetime = TimeSpan.FromMinutes(5);
                });
        });
}
```

### Memory Management

```bash
# Memory optimization for high throughput
DOTNET_GCHeapHardLimit=4294967296        # 4GB heap limit
DOTNET_GCHighMemPercent=85
DOTNET_GCConserveMemory=5

# Large object heap optimization
DOTNET_GCLOHThreshold=8192
DOTNET_GCRetainVM=1

# Server garbage collection
DOTNET_gcServer=1
DOTNET_gcConcurrent=1
```

## Monitoring Scaled Deployments

### Scaling Metrics

```prometheus
# Auto-scaling metrics
hpa_current_replicas{hpa="conduit-core-api-hpa"}
hpa_desired_replicas{hpa="conduit-core-api-hpa"}
hpa_max_replicas{hpa="conduit-core-api-hpa"}

# Resource utilization
container_cpu_usage_seconds_total{pod=~"conduit-core-api-.*"}
container_memory_usage_bytes{pod=~"conduit-core-api-.*"}

# Connection pool metrics
database_connections_active / database_connections_total
redis_connections_active / redis_connections_total
```

### Scaling Alerts

```yaml
# alerts/scaling.yml
groups:
  - name: conduit-scaling
    rules:
      - alert: HPAMaxReplicasReached
        expr: hpa_current_replicas{hpa="conduit-core-api-hpa"} >= hpa_max_replicas{hpa="conduit-core-api-hpa"}
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "HPA has reached maximum replicas"
          description: "Auto-scaler cannot scale further - consider increasing max replicas"
          
      - alert: ScalingEventsFailed
        expr: increase(hpa_scaling_events_total{result="failed"}[5m]) > 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Auto-scaling events are failing"
          description: "{{$value}} scaling events have failed in the last 5 minutes"
```

## Capacity Planning

### Resource Planning Matrix

| Load Level | Core API Replicas | CPU per Pod | Memory per Pod | Database Connections | RabbitMQ Partitions |
|------------|-------------------|-------------|----------------|---------------------|-------------------|
| **Low** (< 500 req/min) | 2-3 | 500m | 1Gi | 50 | 10 |
| **Medium** (500-2000 req/min) | 3-5 | 1000m | 2Gi | 100 | 20 |
| **High** (2000-5000 req/min) | 5-10 | 2000m | 4Gi | 200 | 30 |
| **Very High** (5000+ req/min) | 10-20 | 4000m | 8Gi | 400 | 50 |

### Growth Planning

```bash
# Scaling decision points
if req_per_minute > 1000 && cpu_utilization > 70:
    scale_horizontally()
    
if database_connections > 80%:
    increase_connection_pool()
    consider_read_replicas()
    
if rabbitmq_queue_depth > 1000:
    increase_consumers()
    add_partitions()
    
if memory_usage > 85%:
    increase_pod_memory()
    optimize_cache_settings()
```

## Troubleshooting Scaling Issues

### Common Scaling Problems

**Pods Not Scaling:**
```bash
# Check HPA status
kubectl describe hpa conduit-core-api-hpa

# Check metrics availability
kubectl top pods -n conduit-production

# Verify resource requests/limits
kubectl describe pod conduit-core-api-xxx
```

**Database Connection Exhaustion:**
```bash
# Monitor connection usage
kubectl logs deployment/conduit-core-api | grep -i "connection"

# Check database metrics
curl -s http://prometheus:9090/api/v1/query?query=database_connections_active
```

**RabbitMQ Queue Buildup:**
```bash
# Check consumer status
kubectl exec deployment/rabbitmq -- rabbitmqctl list_consumers

# Monitor queue depths
kubectl exec deployment/rabbitmq -- rabbitmqctl list_queues name messages
```

## Best Practices

### Scaling Strategy

1. **Start Conservative**: Begin with minimum viable scaling configuration
2. **Monitor Closely**: Watch all metrics during scaling events
3. **Scale Gradually**: Avoid sudden large scaling jumps
4. **Test Scaling**: Verify auto-scaling works under load
5. **Plan Capacity**: Monitor growth trends and plan ahead

### Resource Management

1. **Set Resource Limits**: Always define CPU/memory limits
2. **Use Resource Requests**: Ensure scheduler knows requirements
3. **Monitor Utilization**: Track actual vs requested resources
4. **Optimize Regularly**: Review and adjust based on usage patterns

### Operational Excellence

1. **Automate Scaling**: Use HPA and KEDA for automatic scaling
2. **Monitor Performance**: Track all scaling-related metrics
3. **Plan for Failure**: Ensure scaling works during incidents
4. **Document Procedures**: Maintain scaling runbooks and procedures

## Next Steps

- **Production Deployment**: Deploy your [scaled production environment](production-deployment)
- **Monitoring**: Set up [comprehensive monitoring](monitoring-health) for scaled deployments
- **RabbitMQ**: Configure [RabbitMQ clustering](rabbitmq-setup) for high availability
- **Performance Testing**: Validate scaling behavior under load