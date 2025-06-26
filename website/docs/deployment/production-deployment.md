---
sidebar_position: 2
title: Production Deployment
description: Deploy Conduit for production with high availability, scaling, and monitoring
---

# Production Deployment

This guide covers deploying Conduit for production environments with high availability, horizontal scaling, and comprehensive monitoring.

## Architecture Overview

Production Conduit deployment consists of:

- **Multiple Core API instances** (load balanced)
- **Admin API instance(s)** (can be load balanced)
- **WebUI instances** (optional, can be load balanced)
- **RabbitMQ cluster** for event messaging
- **Redis cluster** for caching and SignalR
- **PostgreSQL cluster** with read replicas
- **Load balancer** (Nginx, HAProxy, or cloud LB)

## Prerequisites

- Kubernetes cluster or Docker Swarm
- PostgreSQL with read replicas
- Redis cluster or managed Redis service
- RabbitMQ cluster or managed message queue
- Load balancer with SSL termination
- Monitoring infrastructure (Prometheus, Grafana)

## High-Level Deployment

### 1. Infrastructure Setup

```yaml
# kubernetes/namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: conduit-production
---
# kubernetes/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: conduit-config
  namespace: conduit-production
data:
  DATABASE_URL: "postgresql://conduit:password@postgres-cluster:5432/conduit"
  REDIS_URL: "redis://redis-cluster:6379/0"
  REDIS_URL_SIGNALR: "redis://redis-cluster:6379/2"
  RABBITMQ_HOST: "rabbitmq-cluster"
  RABBITMQ_PORT: "5672"
  RABBITMQ_USERNAME: "conduit"
  RABBITMQ_PASSWORD: "secure-password"
```

### 2. Core API Deployment

```yaml
# kubernetes/core-api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: conduit-core-api
  namespace: conduit-production
spec:
  replicas: 3
  selector:
    matchLabels:
      app: conduit-core-api
  template:
    metadata:
      labels:
        app: conduit-core-api
    spec:
      containers:
      - name: conduit-core-api
        image: conduit/core-api:latest
        ports:
        - containerPort: 5000
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: DATABASE_URL
          valueFrom:
            configMapKeyRef:
              name: conduit-config
              key: DATABASE_URL
        - name: REDIS_URL
          valueFrom:
            configMapKeyRef:
              name: conduit-config
              key: REDIS_URL
        - name: CONDUITLLM__RABBITMQ__HOST
          valueFrom:
            configMapKeyRef:
              name: conduit-config
              key: RABBITMQ_HOST
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5000
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: conduit-core-api-service
  namespace: conduit-production
spec:
  selector:
    app: conduit-core-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 5000
  type: ClusterIP
```

### 3. Admin API Deployment

```yaml
# kubernetes/admin-api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: conduit-admin-api
  namespace: conduit-production
spec:
  replicas: 2
  selector:
    matchLabels:
      app: conduit-admin-api
  template:
    metadata:
      labels:
        app: conduit-admin-api
    spec:
      containers:
      - name: conduit-admin-api
        image: conduit/admin-api:latest
        ports:
        - containerPort: 5002
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: DATABASE_URL
          valueFrom:
            configMapKeyRef:
              name: conduit-config
              key: DATABASE_URL
        - name: CONDUITLLM__RABBITMQ__HOST
          valueFrom:
            configMapKeyRef:
              name: conduit-config
              key: RABBITMQ_HOST
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
```

## Event-Driven Scaling Configuration

### RabbitMQ Production Configuration

```bash
# High-throughput RabbitMQ configuration
CONDUITLLM__RABBITMQ__HOST=rabbitmq-cluster
CONDUITLLM__RABBITMQ__PORT=5672
CONDUITLLM__RABBITMQ__USERNAME=conduit
CONDUITLLM__RABBITMQ__PASSWORD=secure-password
CONDUITLLM__RABBITMQ__VHOST=/
CONDUITLLM__RABBITMQ__PREFETCHCOUNT=25
CONDUITLLM__RABBITMQ__PARTITIONCOUNT=30
CONDUITLLM__RABBITMQ__CONCURRENTMESSAGELIMIT=50
CONDUITLLM__RABBITMQ__MAXCONNECTIONS=5
CONDUITLLM__RABBITMQ__MINCONNECTIONS=2
CONDUITLLM__RABBITMQ__REQUESTEDHEARTBEAT=30
CONDUITLLM__RABBITMQ__PUBLISHERCONFIRMATION=true
```

### Redis Cluster Configuration

```bash
# Redis for caching and SignalR backplane
REDIS_URL=redis://redis-cluster:6379/0
REDIS_URL_SIGNALR=redis://redis-cluster:6379/2
CONDUITLLM__REDIS__CONNECTIONSTRING=redis-cluster:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000
```

## Database Scaling

### PostgreSQL Production Configuration

```sql
-- Database performance settings
ALTER SYSTEM SET max_connections = 200;
ALTER SYSTEM SET shared_buffers = '256MB';
ALTER SYSTEM SET effective_cache_size = '1GB';
ALTER SYSTEM SET work_mem = '4MB';
ALTER SYSTEM SET maintenance_work_mem = '64MB';
SELECT pg_reload_conf();
```

### Connection Pooling

```bash
# PostgreSQL connection pool settings
CONDUITLLM__DATABASE__MAXPOOLSIZE=100
CONDUITLLM__DATABASE__MINPOOLSIZE=10
CONDUITLLM__DATABASE__CONNECTIONTIMEOUT=30
CONDUITLLM__DATABASE__COMMANDTIMEOUT=30
CONDUITLLM__DATABASE__CONNECTIONLIFETIME=300
```

## Load Balancer Configuration

### Nginx Configuration

```nginx
# nginx.conf
upstream conduit_core_api {
    least_conn;
    server conduit-core-api-1:5000 max_fails=3 fail_timeout=30s;
    server conduit-core-api-2:5000 max_fails=3 fail_timeout=30s;
    server conduit-core-api-3:5000 max_fails=3 fail_timeout=30s;
}

upstream conduit_admin_api {
    least_conn;
    server conduit-admin-api-1:5002 max_fails=3 fail_timeout=30s;
    server conduit-admin-api-2:5002 max_fails=3 fail_timeout=30s;
}

upstream conduit_webui {
    least_conn;
    server conduit-webui-1:5001 max_fails=3 fail_timeout=30s;
    server conduit-webui-2:5001 max_fails=3 fail_timeout=30s;
}

server {
    listen 443 ssl http2;
    server_name api.conduit.yourdomain.com;
    
    ssl_certificate /etc/ssl/certs/conduit.crt;
    ssl_certificate_key /etc/ssl/private/conduit.key;
    
    # Core API
    location /v1/ {
        proxy_pass http://conduit_core_api;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # WebSocket support for SignalR
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        
        # Timeouts for long-running requests
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
    
    # SignalR hubs
    location /hubs/ {
        proxy_pass http://conduit_core_api;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
    
    # Health checks
    location /health {
        proxy_pass http://conduit_core_api;
        access_log off;
    }
}

server {
    listen 443 ssl http2;
    server_name admin.conduit.yourdomain.com;
    
    ssl_certificate /etc/ssl/certs/conduit.crt;
    ssl_certificate_key /etc/ssl/private/conduit.key;
    
    # Admin API
    location /api/ {
        proxy_pass http://conduit_admin_api;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    
    # WebUI
    location / {
        proxy_pass http://conduit_webui;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

## Monitoring and Observability

### Prometheus Configuration

```yaml
# prometheus/prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'conduit-core-api'
    static_configs:
      - targets: ['conduit-core-api:5000']
    metrics_path: /metrics
    scrape_interval: 15s
    
  - job_name: 'conduit-admin-api'
    static_configs:
      - targets: ['conduit-admin-api:5002']
    metrics_path: /metrics
    scrape_interval: 15s

  - job_name: 'rabbitmq'
    static_configs:
      - targets: ['rabbitmq-cluster:15692']
    metrics_path: /metrics
    
  - job_name: 'redis'
    static_configs:
      - targets: ['redis-cluster:6379']
```

### Key Metrics to Monitor

```yaml
# alerts.yml
groups:
  - name: conduit-core
    rules:
      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.1
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "High error rate in Conduit Core API"
          
      - alert: HighLatency
        expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High latency in Conduit Core API"
          
      - alert: RabbitMQQueueDepth
        expr: rabbitmq_queue_messages > 1000
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "RabbitMQ queue depth is high"
          
      - alert: DatabaseConnections
        expr: postgres_connections_active / postgres_connections_max > 0.8
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "PostgreSQL connection pool utilization is high"
```

## Security Configuration

### Network Policies

```yaml
# kubernetes/network-policy.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: conduit-network-policy
  namespace: conduit-production
spec:
  podSelector:
    matchLabels:
      app: conduit-core-api
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 5000
  egress:
  - to:
    - namespaceSelector:
        matchLabels:
          name: database
    ports:
    - protocol: TCP
      port: 5432
```

### SSL/TLS Configuration

```bash
# Environment variables for HTTPS
CONDUITLLM__KESTREL__ENDPOINTS__HTTPS__URL=https://0.0.0.0:5443
CONDUITLLM__KESTREL__ENDPOINTS__HTTPS__CERTIFICATE__PATH=/etc/ssl/certs/conduit.pfx
CONDUITLLM__KESTREL__ENDPOINTS__HTTPS__CERTIFICATE__PASSWORD=certificate-password
```

## Backup and Disaster Recovery

### Automated Backup Strategy

```bash
#!/bin/bash
# backup-script.sh

# Database backup
pg_dump -h postgres-cluster -U conduit -d conduit_production > conduit-db-$(date +%Y%m%d).sql

# Configuration backup via Admin API
curl -H "Authorization: Bearer $ADMIN_API_KEY" \
  https://admin.conduit.yourdomain.com/api/admin/export/configuration > config-backup-$(date +%Y%m%d).json

# Upload to S3
aws s3 cp conduit-db-$(date +%Y%m%d).sql s3://conduit-backups/
aws s3 cp config-backup-$(date +%Y%m%d).json s3://conduit-backups/
```

### Disaster Recovery Plan

1. **Database Recovery**: Restore from latest backup
2. **Configuration Recovery**: Import via Admin API
3. **Service Recovery**: Redeploy from container registry
4. **Validation**: Run health checks and integration tests

## Performance Optimization

### Resource Allocation

```yaml
# Recommended resource allocation
resources:
  core-api:
    requests:
      memory: "512Mi"
      cpu: "500m"
    limits:
      memory: "1Gi"
      cpu: "1000m"
      
  admin-api:
    requests:
      memory: "256Mi"
      cpu: "250m"
    limits:
      memory: "512Mi"
      cpu: "500m"
      
  webui:
    requests:
      memory: "128Mi"
      cpu: "100m"
    limits:
      memory: "256Mi"
      cpu: "250m"
```

### Scaling Policies

```yaml
# kubernetes/hpa.yaml
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
  maxReplicas: 10
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
```

## Troubleshooting Production Issues

### Common Production Issues

**High Memory Usage:**
```bash
# Check memory allocation
kubectl top pods -n conduit-production

# Analyze memory leaks
kubectl logs -n conduit-production deployment/conduit-core-api | grep -i "memory\|gc"
```

**Database Connection Issues:**
```bash
# Check connection pool status
kubectl exec -n conduit-production deployment/conduit-core-api -- curl localhost:5000/health/database

# Monitor connection counts
kubectl logs -n conduit-production deployment/conduit-core-api | grep -i "connection"
```

**RabbitMQ Queue Buildup:**
```bash
# Check queue depths
kubectl exec -n conduit-production deployment/rabbitmq -- rabbitmqctl list_queues name messages

# Monitor consumer performance
kubectl logs -n conduit-production deployment/conduit-core-api | grep -i "consumer"
```

## Next Steps

- **Scaling Configuration**: Learn about [advanced scaling](scaling-configuration)
- **RabbitMQ Setup**: Configure [RabbitMQ for production](rabbitmq-setup)
- **Monitoring**: Set up [comprehensive monitoring](monitoring-health)
- **Performance Tuning**: Optimize for your specific workload