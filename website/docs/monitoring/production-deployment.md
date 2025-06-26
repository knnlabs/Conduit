# Production Deployment

This guide covers deploying ConduitLLM in production environments using Kubernetes, Docker, and cloud platforms with proper monitoring, scaling, and security configurations.

## Overview

Production deployment considerations:
- High availability and fault tolerance
- Horizontal scaling capabilities
- Comprehensive monitoring and alerting
- Security hardening
- Performance optimization
- Disaster recovery

## Docker Deployment

### Production Docker Image

Multi-stage Dockerfile for optimized production image:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy and restore dependencies
COPY ["ConduitLLM.Http/ConduitLLM.Http.csproj", "ConduitLLM.Http/"]
COPY ["ConduitLLM.Core/ConduitLLM.Core.csproj", "ConduitLLM.Core/"]
COPY ["ConduitLLM.Providers/ConduitLLM.Providers.csproj", "ConduitLLM.Providers/"]
RUN dotnet restore "ConduitLLM.Http/ConduitLLM.Http.csproj"

# Copy and build
COPY . .
WORKDIR "/src/ConduitLLM.Http"
RUN dotnet build "ConduitLLM.Http.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "ConduitLLM.Http.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app

# Install dependencies for health checks
RUN apk add --no-cache curl

# Create non-root user
RUN addgroup -g 1000 conduit && \
    adduser -u 1000 -G conduit -s /bin/sh -D conduit

# Copy published app
COPY --from=publish /app/publish .
RUN chown -R conduit:conduit /app

# Security hardening
RUN chmod -R 550 /app

USER conduit
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]
```

### Docker Compose Production

```yaml
version: '3.8'

services:
  api:
    image: conduit:latest
    restart: always
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION}
      - ConnectionStrings__Redis=${REDIS_CONNECTION}
      - Monitoring__Prometheus__Enabled=true
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '1'
          memory: 1G
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  admin:
    image: conduit-admin:latest
    restart: always
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - MasterKey=${ADMIN_MASTER_KEY}
    ports:
      - "8081:8080"
    depends_on:
      - api

  postgres:
    image: postgres:16-alpine
    restart: always
    environment:
      - POSTGRES_DB=conduit
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_INITDB_ARGS=--auth-host=scram-sha-256
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${DB_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 1G

  redis:
    image: redis:7-alpine
    restart: always
    command: >
      redis-server
      --requirepass ${REDIS_PASSWORD}
      --maxmemory 512mb
      --maxmemory-policy allkeys-lru
      --save 900 1
      --save 300 10
      --save 60 10000
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "--auth", "${REDIS_PASSWORD}", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  prometheus:
    image: prom/prometheus:latest
    restart: always
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=30d'
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus
    ports:
      - "9090:9090"

  grafana:
    image: grafana/grafana:latest
    restart: always
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_PASSWORD}
      - GF_INSTALL_PLUGINS=redis-datasource
    volumes:
      - grafana_data:/var/lib/grafana
      - ./grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./grafana/datasources:/etc/grafana/provisioning/datasources
    ports:
      - "3000:3000"
    depends_on:
      - prometheus

volumes:
  postgres_data:
  redis_data:
  prometheus_data:
  grafana_data:

networks:
  default:
    driver: bridge
    ipam:
      config:
        - subnet: 172.20.0.0/16
```

## Kubernetes Deployment

### Namespace and ConfigMap

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: conduit

---
apiVersion: v1
kind: ConfigMap
metadata:
  name: conduit-config
  namespace: conduit
data:
  appsettings.Production.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "HealthChecks": {
        "Enabled": true
      },
      "Monitoring": {
        "Prometheus": {
          "Enabled": true,
          "Endpoint": "/metrics"
        }
      }
    }
```

### Secrets

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: conduit-secrets
  namespace: conduit
type: Opaque
data:
  db-connection: <base64-encoded-connection-string>
  redis-connection: <base64-encoded-redis-connection>
  master-key: <base64-encoded-master-key>
```

### API Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: conduit-api
  namespace: conduit
spec:
  replicas: 3
  selector:
    matchLabels:
      app: conduit-api
  template:
    metadata:
      labels:
        app: conduit-api
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      affinity:
        podAntiAffinity:
          requiredDuringSchedulingIgnoredDuringExecution:
          - labelSelector:
              matchExpressions:
              - key: app
                operator: In
                values:
                - conduit-api
            topologyKey: kubernetes.io/hostname
      
      containers:
      - name: api
        image: your-registry/conduit:latest
        imagePullPolicy: Always
        ports:
        - containerPort: 8080
          name: http
        
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: conduit-secrets
              key: db-connection
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: conduit-secrets
              key: redis-connection
        
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "2000m"
        
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 20
          periodSeconds: 10
          timeoutSeconds: 10
          failureThreshold: 3
        
        volumeMounts:
        - name: config
          mountPath: /app/appsettings.Production.json
          subPath: appsettings.Production.json
      
      volumes:
      - name: config
        configMap:
          name: conduit-config
```

### Service and Ingress

```yaml
apiVersion: v1
kind: Service
metadata:
  name: conduit-api
  namespace: conduit
spec:
  selector:
    app: conduit-api
  ports:
  - port: 80
    targetPort: 8080
    name: http
  type: ClusterIP

---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: conduit-api
  namespace: conduit
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/rate-limit: "100"
    nginx.ingress.kubernetes.io/proxy-body-size: "50m"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "300"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - api.conduit.example.com
    secretName: conduit-tls
  rules:
  - host: api.conduit.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: conduit-api
            port:
              number: 80
```

### Horizontal Pod Autoscaler

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: conduit-api-hpa
  namespace: conduit
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: conduit-api
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
        name: conduit_active_requests
      target:
        type: AverageValue
        averageValue: "100"
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 10
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 100
        periodSeconds: 60
      - type: Pods
        value: 4
        periodSeconds: 60
```

### Database Setup

```yaml
apiVersion: postgresql.cnpg.io/v1
kind: Cluster
metadata:
  name: postgres-cluster
  namespace: conduit
spec:
  instances: 3
  
  postgresql:
    parameters:
      max_connections: "200"
      shared_buffers: "256MB"
      effective_cache_size: "1GB"
      
  bootstrap:
    initdb:
      database: conduit
      owner: conduit
      secret:
        name: postgres-credentials
  
  monitoring:
    enabled: true
    customQueries:
      - name: "conduit_queries"
        query: |
          SELECT query, calls, mean_exec_time
          FROM pg_stat_statements
          WHERE query LIKE '%conduit%'
        
  storage:
    size: 100Gi
    storageClass: fast-ssd
```

## Monitoring Stack

### Prometheus Configuration

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: prometheus-config
  namespace: conduit
data:
  prometheus.yml: |
    global:
      scrape_interval: 15s
      evaluation_interval: 15s
    
    scrape_configs:
    - job_name: 'conduit-api'
      kubernetes_sd_configs:
      - role: pod
        namespaces:
          names:
          - conduit
      relabel_configs:
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
        action: keep
        regex: true
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
        action: replace
        target_label: __metrics_path__
        regex: (.+)
      - source_labels: [__address__, __meta_kubernetes_pod_annotation_prometheus_io_port]
        action: replace
        regex: ([^:]+)(?::\d+)?;(\d+)
        replacement: $1:$2
        target_label: __address__
    
    rule_files:
    - '/etc/prometheus/rules/*.yml'
    
    alerting:
      alertmanagers:
      - static_configs:
        - targets:
          - alertmanager:9093
```

### Grafana Dashboards

Deploy pre-configured dashboards:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: grafana-dashboards
  namespace: conduit
data:
  conduit-overview.json: |
    {
      "dashboard": {
        "title": "ConduitLLM Overview",
        "panels": [
          {
            "title": "Request Rate",
            "targets": [
              {
                "expr": "rate(conduit_llm_requests_total[5m])"
              }
            ]
          },
          {
            "title": "Response Time",
            "targets": [
              {
                "expr": "histogram_quantile(0.95, conduit_llm_request_duration_seconds_bucket)"
              }
            ]
          }
        ]
      }
    }
```

## Security Hardening

### Network Policies

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: conduit-api-netpol
  namespace: conduit
spec:
  podSelector:
    matchLabels:
      app: conduit-api
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    - podSelector:
        matchLabels:
          app: prometheus
    ports:
    - protocol: TCP
      port: 8080
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: postgres
    ports:
    - protocol: TCP
      port: 5432
  - to:
    - podSelector:
        matchLabels:
          app: redis
    ports:
    - protocol: TCP
      port: 6379
  - to:
    - namespaceSelector: {}
      podSelector:
        matchLabels:
          k8s-app: kube-dns
    ports:
    - protocol: UDP
      port: 53
```

### Pod Security Policy

```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: conduit-api-pdb
  namespace: conduit
spec:
  minAvailable: 2
  selector:
    matchLabels:
      app: conduit-api
```

## Disaster Recovery

### Backup Strategy

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: postgres-backup
  namespace: conduit
spec:
  schedule: "0 2 * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: backup
            image: postgres:16-alpine
            command:
            - /bin/bash
            - -c
            - |
              pg_dump $DATABASE_URL | gzip > /backup/conduit-$(date +%Y%m%d-%H%M%S).sql.gz
              # Upload to S3
              aws s3 cp /backup/conduit-*.sql.gz s3://conduit-backups/
              # Keep only last 30 days
              find /backup -name "*.sql.gz" -mtime +30 -delete
            env:
            - name: DATABASE_URL
              valueFrom:
                secretKeyRef:
                  name: conduit-secrets
                  key: db-connection
            volumeMounts:
            - name: backup
              mountPath: /backup
          volumes:
          - name: backup
            persistentVolumeClaim:
              claimName: backup-pvc
          restartPolicy: OnFailure
```

## Performance Optimization

### Connection Pooling

```csharp
// In appsettings.Production.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Database=conduit;Username=conduit;Password=xxx;Maximum Pool Size=100;Connection Idle Lifetime=300"
  },
  "Redis": {
    "Configuration": "redis:6379,password=xxx,connectTimeout=5000,syncTimeout=5000,abortConnect=false,connectRetry=3"
  }
}
```

### Caching Configuration

```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = Configuration.GetConnectionString("Redis");
    options.InstanceName = "conduit";
});

services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024 * 1024 * 100; // 100MB
});
```

## Observability

### Distributed Tracing

```yaml
apiVersion: v1
kind: Service
metadata:
  name: jaeger
  namespace: conduit
spec:
  ports:
  - name: collector
    port: 14268
    targetPort: 14268
  - name: query
    port: 16686
    targetPort: 16686
  selector:
    app: jaeger
```

### Log Aggregation

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: fluent-bit-config
  namespace: conduit
data:
  fluent-bit.conf: |
    [SERVICE]
        Flush         1
        Log_Level     info
        Daemon        off

    [INPUT]
        Name              tail
        Path              /var/log/containers/*conduit*.log
        Parser            docker
        Tag               conduit.*
        Refresh_Interval  5

    [OUTPUT]
        Name              es
        Match             conduit.*
        Host              elasticsearch
        Port              9200
        Index             conduit
        Type              _doc
```

## Deployment Checklist

- [ ] SSL/TLS certificates configured
- [ ] Database connection pooling optimized
- [ ] Redis memory limits set
- [ ] Horizontal scaling configured
- [ ] Health checks enabled
- [ ] Monitoring endpoints exposed
- [ ] Backup strategy implemented
- [ ] Network policies applied
- [ ] Resource limits defined
- [ ] Logging configured
- [ ] Secrets management in place
- [ ] Disaster recovery tested

## Next Steps

- [Health Checks](health-checks.md) - Configure health monitoring
- [Metrics Monitoring](metrics-monitoring.md) - Set up Prometheus
- [Runbooks](runbooks.md) - Operational procedures