---
sidebar_position: 5
title: Monitoring & Health
description: Comprehensive monitoring, alerting, and health checks for Conduit production deployments
---

# Monitoring & Health

Comprehensive monitoring is essential for production Conduit deployments. This guide covers health checks, metrics collection, alerting, and observability.

## Health Check Endpoints

### Core API Health Checks

```bash
# Basic health check
curl http://localhost:5000/health
# Response: Healthy

# Detailed health information
curl http://localhost:5000/health/ready

# Component-specific health
curl http://localhost:5000/health/database
curl http://localhost:5000/health/redis
curl http://localhost:5000/health/rabbitmq
```

### Admin API Health Checks

```bash
# Admin API health
curl http://localhost:5002/health

# Admin-specific components
curl http://localhost:5002/health/database
curl http://localhost:5002/health/providers
```

### Health Check Response Format

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "description": "Database connection successful"
    },
    "redis": {
      "status": "Healthy", 
      "duration": "00:00:00.0098765",
      "description": "Redis connection successful"
    },
    "rabbitmq": {
      "status": "Healthy",
      "duration": "00:00:00.0156789",
      "description": "RabbitMQ connection successful"
    },
    "providers": {
      "status": "Degraded",
      "duration": "00:00:00.0245678",
      "description": "2 of 5 providers available",
      "data": {
        "openai": "Healthy",
        "anthropic": "Healthy", 
        "google": "Unhealthy",
        "azure": "Unhealthy",
        "cohere": "Healthy"
      }
    }
  }
}
```

## Prometheus Metrics

### Metrics Configuration

```yaml
# appsettings.Production.json
{
  "Metrics": {
    "Enabled": true,
    "Endpoint": "/metrics",
    "IncludeDebugMetrics": false
  }
}
```

### Core Metrics Categories

**HTTP Request Metrics:**
```prometheus
# Request duration
http_request_duration_seconds{method="POST",endpoint="/v1/chat/completions",status="200"}

# Request count
http_requests_total{method="POST",endpoint="/v1/chat/completions",status="200"}

# Active requests
http_requests_active{method="POST",endpoint="/v1/chat/completions"}
```

**Provider Metrics:**
```prometheus
# Provider request duration
provider_request_duration_seconds{provider="openai",model="gpt-4"}

# Provider request count
provider_requests_total{provider="openai",model="gpt-4",status="success"}

# Provider errors
provider_errors_total{provider="openai",error_type="rate_limit"}

# Provider health
provider_health_status{provider="openai",status="healthy"}
```

**Virtual Key Metrics:**
```prometheus
# Virtual key usage
virtual_key_requests_total{key_hash="abcd1234",model="gpt-4"}

# Virtual key spending  
virtual_key_cost_total{key_hash="abcd1234",provider="openai"}

# Virtual key rate limiting
virtual_key_rate_limited_total{key_hash="abcd1234"}
```

**Database Metrics:**
```prometheus
# Connection pool
database_connections_active
database_connections_available
database_connections_total

# Query performance
database_query_duration_seconds{operation="select"}
database_queries_total{operation="select",status="success"}
```

**RabbitMQ Metrics:**
```prometheus
# Queue depth
rabbitmq_queue_messages{queue="conduit.webhook-delivery"}

# Message processing
rabbitmq_messages_processed_total{queue="conduit.webhook-delivery"}

# Consumer performance
rabbitmq_consumer_duration_seconds{queue="conduit.webhook-delivery"}
```

**Cache Metrics:**
```prometheus
# Redis performance
redis_operations_total{operation="get",status="hit"}
redis_operation_duration_seconds{operation="get"}

# Cache effectiveness
cache_hit_ratio{cache_type="virtual_key"}
cache_evictions_total{cache_type="virtual_key"}
```

## Grafana Dashboards

### Core API Dashboard

```json
{
  "dashboard": {
    "title": "Conduit Core API",
    "panels": [
      {
        "title": "Request Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(http_requests_total[5m])",
            "legendFormat": "{{method}} {{endpoint}}"
          }
        ]
      },
      {
        "title": "Response Time P95",
        "type": "graph", 
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))",
            "legendFormat": "95th percentile"
          }
        ]
      },
      {
        "title": "Error Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(http_requests_total{status=~\"5..\"}[5m]) / rate(http_requests_total[5m])",
            "legendFormat": "Error Rate"
          }
        ]
      },
      {
        "title": "Provider Health",
        "type": "stat",
        "targets": [
          {
            "expr": "provider_health_status",
            "legendFormat": "{{provider}}"
          }
        ]
      }
    ]
  }
}
```

### Infrastructure Dashboard

```json
{
  "dashboard": {
    "title": "Conduit Infrastructure",
    "panels": [
      {
        "title": "Database Connections",
        "type": "graph",
        "targets": [
          {
            "expr": "database_connections_active",
            "legendFormat": "Active"
          },
          {
            "expr": "database_connections_available", 
            "legendFormat": "Available"
          }
        ]
      },
      {
        "title": "RabbitMQ Queue Depth",
        "type": "graph",
        "targets": [
          {
            "expr": "rabbitmq_queue_messages",
            "legendFormat": "{{queue}}"
          }
        ]
      },
      {
        "title": "Redis Performance",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(redis_operations_total[5m])",
            "legendFormat": "{{operation}} - {{status}}"
          }
        ]
      }
    ]
  }
}
```

## Alerting Rules

### Critical Alerts

```yaml
# alerts/critical.yml
groups:
  - name: conduit-critical
    rules:
      - alert: ConduitDown
        expr: up{job=~"conduit.*"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Conduit service is down"
          description: "{{$labels.job}} has been down for more than 1 minute"
          
      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) > 0.05
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "High error rate detected"
          description: "Error rate is {{$value | humanizePercentage}} for the last 5 minutes"
          
      - alert: DatabaseConnectionPoolExhausted
        expr: database_connections_available / database_connections_total < 0.1
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Database connection pool nearly exhausted"
          description: "Less than 10% of database connections available"
```

### Warning Alerts

```yaml
# alerts/warning.yml
groups:
  - name: conduit-warning
    rules:
      - alert: HighLatency
        expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 2
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High latency detected"
          description: "95th percentile latency is {{$value}}s"
          
      - alert: ProviderDegraded
        expr: provider_health_status{status="healthy"} / ignoring(status) group_left() provider_health_status < 0.8
        for: 3m
        labels:
          severity: warning
        annotations:
          summary: "Provider availability degraded"
          description: "Less than 80% of providers are healthy"
          
      - alert: RabbitMQQueueBuildup
        expr: rabbitmq_queue_messages > 1000
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "RabbitMQ queue buildup detected"
          description: "Queue {{$labels.queue}} has {{$value}} messages"
          
      - alert: CacheHitRateLow
        expr: cache_hit_ratio < 0.8
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Cache hit rate is low"
          description: "Cache hit rate is {{$value | humanizePercentage}} for {{$labels.cache_type}}"
```

## Log Aggregation

### Structured Logging Configuration

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.Elasticsearch"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://elasticsearch:9200",
          "indexFormat": "conduit-logs-{0:yyyy.MM.dd}",
          "autoRegisterTemplate": true
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithEnvironmentUserName"],
    "Properties": {
      "Application": "Conduit",
      "Environment": "Production"
    }
  }
}
```

### Key Log Events

**Request Tracing:**
```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "level": "Information",
  "messageTemplate": "Processed {Method} {Endpoint} in {Duration}ms",
  "properties": {
    "Method": "POST",
    "Endpoint": "/v1/chat/completions", 
    "Duration": 1234,
    "StatusCode": 200,
    "VirtualKeyHash": "abcd1234",
    "Provider": "openai",
    "Model": "gpt-4",
    "TokensUsed": 150,
    "Cost": 0.003,
    "CorrelationId": "req-xyz789"
  }
}
```

**Error Tracking:**
```json
{
  "timestamp": "2024-01-15T10:35:00.456Z",
  "level": "Error",
  "messageTemplate": "Provider request failed: {Error}",
  "properties": {
    "Provider": "openai",
    "Model": "gpt-4",
    "Error": "Rate limit exceeded",
    "StatusCode": 429,
    "RetryAfter": 60,
    "CorrelationId": "req-abc123"
  },
  "exception": "ProviderRateLimitException: Rate limit exceeded..."
}
```

### ELK Stack Configuration

```yaml
# docker-compose.yml additions
services:
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
    ports:
      - "9200:9200"
    volumes:
      - elasticsearch_data:/usr/share/elasticsearch/data
      
  logstash:
    image: docker.elastic.co/logstash/logstash:8.11.0
    volumes:
      - "./logstash.conf:/usr/share/logstash/pipeline/logstash.conf"
    ports:
      - "5044:5044"
    depends_on:
      - elasticsearch
      
  kibana:
    image: docker.elastic.co/kibana/kibana:8.11.0
    ports:
      - "5601:5601"
    environment:
      - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
    depends_on:
      - elasticsearch

volumes:
  elasticsearch_data:
```

## Application Performance Monitoring (APM)

### OpenTelemetry Configuration

```csharp
// Program.cs
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddRedisInstrumentation()
            .AddSource("Conduit.*")
            .SetSampler(new TraceIdRatioBasedSampler(1.0))
            .AddJaegerExporter();
    })
    .WithMetrics(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });
```

### Distributed Tracing

```csharp
// Custom tracing for provider requests
using var activity = ActivitySource.StartActivity("provider.request");
activity?.SetTag("provider", providerName);
activity?.SetTag("model", modelName);
activity?.SetTag("virtual_key", virtualKeyHash);

try
{
    var response = await providerClient.SendAsync(request);
    activity?.SetTag("status", "success");
    activity?.SetTag("tokens", response.TokensUsed);
    return response;
}
catch (Exception ex)
{
    activity?.SetTag("status", "error");
    activity?.SetTag("error", ex.Message);
    throw;
}
```

## Operational Runbooks

### High Error Rate Response

1. **Immediate Actions (< 5 minutes)**
   - Check provider health status
   - Verify database connectivity
   - Check recent deployments
   - Scale up if resource constrained

2. **Investigation (5-15 minutes)**
   - Analyze error logs for patterns
   - Check external provider status pages
   - Verify configuration changes
   - Review recent virtual key changes

3. **Resolution (15-30 minutes)**
   - Disable problematic providers if needed
   - Rollback recent deployments
   - Adjust rate limits or scaling
   - Communicate with stakeholders

### High Latency Response

1. **Immediate Checks**
   - Database query performance
   - Provider response times
   - RabbitMQ queue buildup
   - Resource utilization

2. **Scaling Actions**
   - Increase Core API replicas
   - Scale database connections
   - Add RabbitMQ consumers
   - Check cache hit rates

### Provider Outage Response

1. **Detection and Validation**
   - Confirm provider status
   - Check multiple models/endpoints
   - Verify with provider status page

2. **Immediate Mitigation**
   - Disable affected provider temporarily
   - Reroute traffic to healthy providers
   - Adjust model mappings

3. **Communication**
   - Update stakeholders
   - Document incident timeline
   - Plan provider re-enablement

## Performance Baselines

### Expected Performance Metrics

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| **Response Time P95** | < 2s | > 5s |
| **Error Rate** | < 1% | > 5% |
| **Provider Availability** | > 95% | < 80% |
| **Database Connections** | < 80% | > 90% |
| **Queue Depth** | < 100 | > 1000 |
| **Cache Hit Rate** | > 90% | < 80% |
| **CPU Utilization** | < 70% | > 90% |
| **Memory Usage** | < 80% | > 95% |

### Capacity Planning

**Traffic Growth Indicators:**
- Request rate trending upward
- Database connection pool utilization increasing
- RabbitMQ queue depths growing
- Cache miss rate increasing

**Scaling Triggers:**
- Sustained CPU > 70% for 15 minutes
- Memory usage > 80% for 10 minutes
- Database connections > 80% for 5 minutes
- Queue depth > 500 for 5 minutes

## Troubleshooting Tools

### Health Check Scripts

```bash
#!/bin/bash
# health-check.sh - Comprehensive health validation

echo "=== Conduit Health Check ==="

# Core API Health
echo "Checking Core API..."
curl -f http://localhost:5000/health || echo "Core API FAILED"

# Admin API Health  
echo "Checking Admin API..."
curl -f http://localhost:5002/health || echo "Admin API FAILED"

# Database Health
echo "Checking Database..."
curl -f http://localhost:5000/health/database || echo "Database FAILED"

# RabbitMQ Health
echo "Checking RabbitMQ..."
curl -f http://localhost:5000/health/rabbitmq || echo "RabbitMQ FAILED"

# Provider Health
echo "Checking Providers..."
curl -s http://localhost:5002/api/admin/providers/health | jq '.[] | select(.isHealthy == false)'

echo "=== Health Check Complete ==="
```

### Performance Analysis

```bash
#!/bin/bash
# performance-check.sh

echo "=== Performance Analysis ==="

# Request metrics
echo "Request rate (last 5 minutes):"
curl -s 'http://prometheus:9090/api/v1/query?query=rate(http_requests_total[5m])' | jq -r '.data.result[] | "\(.metric.endpoint): \(.value[1])"'

# Latency percentiles
echo "Response time P95:"
curl -s 'http://prometheus:9090/api/v1/query?query=histogram_quantile(0.95,rate(http_request_duration_seconds_bucket[5m]))' | jq -r '.data.result[0].value[1]'

# Error rate
echo "Error rate:"
curl -s 'http://prometheus:9090/api/v1/query?query=rate(http_requests_total{status=~"5.."}[5m])/rate(http_requests_total[5m])' | jq -r '.data.result[0].value[1]'

echo "=== Performance Analysis Complete ==="
```

## Next Steps

- **Scaling Configuration**: Learn about [horizontal scaling strategies](scaling-configuration)
- **Deployment**: Complete your [production deployment](production-deployment)
- **Troubleshooting**: Review [common issues and solutions](../troubleshooting/common-issues)
- **Architecture**: Understand [event-driven design](../architecture/event-driven-design)