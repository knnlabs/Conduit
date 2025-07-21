# Conduit Monitoring and Observability Guide

## Overview

Conduit implements comprehensive monitoring and observability for production deployments supporting 10,000+ concurrent customers. The monitoring stack includes:

- **OpenTelemetry**: Distributed tracing and metrics collection
- **Prometheus**: Time-series metrics storage and alerting
- **Grafana**: Visualization and dashboards
- **Custom Metrics**: Business and operational metrics

## Architecture

```
┌─────────────────┐     ┌─────────────────┐
│   Core API      │     │   Admin API     │
│                 │     │                 │
│ - HTTP Metrics  │     │ - HTTP Metrics  │
│ - Business      │     │ - Operations    │
│ - Infrastructure│     │   Metrics       │
│ - SignalR       │     │                 │
│ - Task Processing     │                 │
└────────┬────────┘     └────────┬────────┘
         │                       │
         └───────────┬───────────┘
                     │
              ┌──────▼──────┐
              │ Prometheus  │
              │  Exporter   │
              └──────┬──────┘
                     │
         ┌───────────┴───────────┐
         │                       │
    ┌────▼────┐           ┌─────▼─────┐
    │Prometheus│           │  Grafana  │
    │ Server  │◄──────────┤           │
    └─────────┘           └───────────┘
```

## Metrics Categories

### 1. HTTP Metrics
- **Request rate**: `conduit_http_requests_total`
- **Response time**: `conduit_http_request_duration_seconds`
- **Request/Response size**: `conduit_http_request/response_size_bytes`
- **Active requests**: `conduit_http_requests_active`
- **Errors**: `conduit_http_errors_total`

### 2. Business Metrics
- **Virtual key usage**: `conduit_virtualkey_requests_total`
- **Spend tracking**: `conduit_virtualkey_spend_total`
- **Budget utilization**: `conduit_virtualkey_budget_utilization_percent`
- **Model usage**: `conduit_model_requests_total`
- **Token consumption**: `conduit_model_tokens_total`
- **Cost tracking**: `conduit_cost_total_dollars`

### 3. Infrastructure Metrics
- **Database connections**: `conduit_database_connections_*`
- **Redis operations**: `conduit_redis_*`
- **RabbitMQ queues**: `conduit_rabbitmq_*`
- **System resources**: `conduit_process_*`

### 3a. Cache Statistics Metrics
- **Hit/miss rates**: `conduit_cache_hits_total`, `conduit_cache_misses_total`
- **Hit rate percentage**: `conduit_cache_hit_rate`
- **Response times**: `conduit_cache_response_time_seconds`
- **Active instances**: `conduit_cache_statistics_active_instances`
- **Aggregation latency**: `conduit_cache_statistics_aggregation_latency_ms`
- **Statistics drift**: `conduit_cache_statistics_max_drift_percentage`

### 4. SignalR Metrics
- **Active connections**: `conduit_signalr_connections_active`
- **Messages sent/received**: `conduit_signalr_messages_total`
- **Connection limits**: Per virtual key (100) and global (10K)

### 5. Task Processing Metrics
- **Queue depth**: `conduit_tasks_queue_depth`
- **Processing time**: `conduit_tasks_duration_seconds`
- **Success/failure rates**: `conduit_tasks_total`

## Setup Instructions

### 1. Docker Compose Configuration

```yaml
version: '3.8'

services:
  api:
    image: conduit/api:latest
    environment:
      - CONDUITLLM__MONITORING__ENABLED=true
      - CONDUITLLM__MONITORING__PROMETHEUS__ENABLED=true
    ports:
      - "8080:80"
    
  admin:
    image: conduit/admin:latest
    environment:
      - CONDUITLLM__MONITORING__ENABLED=true
      - CONDUITLLM__MONITORING__PROMETHEUS__ENABLED=true
    ports:
      - "8081:80"
    
  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./monitoring/prometheus:/etc/prometheus
      - prometheus_data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/usr/share/prometheus/console_libraries'
      - '--web.console.templates=/usr/share/prometheus/consoles'
    ports:
      - "9090:9090"
    
  grafana:
    image: grafana/grafana:latest
    volumes:
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./monitoring/grafana/datasources:/etc/grafana/provisioning/datasources
      - grafana_data:/var/lib/grafana
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    ports:
      - "3000:3000"

volumes:
  prometheus_data:
  grafana_data:
```

### 2. Prometheus Configuration

Create `monitoring/prometheus/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']

rule_files:
  - "alerts.yml"

scrape_configs:
  - job_name: 'conduit-api'
    static_configs:
      - targets: ['api:80']
    metrics_path: '/metrics'
    scrape_interval: 15s
    
  - job_name: 'conduit-admin'
    static_configs:
      - targets: ['admin:80']
    metrics_path: '/metrics'
    scrape_interval: 15s
    bearer_token: 'your-admin-api-key'
```

### 3. Grafana Dashboard Import

1. Access Grafana at http://localhost:3000
2. Login with admin/admin
3. Import dashboard from `/monitoring/grafana/dashboards/system-overview.json`
4. Configure Prometheus data source

### 4. Environment Variables

```bash
# Core monitoring settings
export CONDUITLLM__MONITORING__ENABLED=true
export CONDUITLLM__MONITORING__PROMETHEUS__ENABLED=true
export CONDUITLLM__MONITORING__PROMETHEUS__PORT=9090

# Metric collection intervals
export CONDUITLLM__MONITORING__INFRASTRUCTURE__INTERVAL=15
export CONDUITLLM__MONITORING__BUSINESS__INTERVAL=60
export CONDUITLLM__MONITORING__TASK__INTERVAL=30

# Resource limits for monitoring
export CONDUITLLM__MONITORING__MAXMEMORYMB=100
export CONDUITLLM__MONITORING__MAXCPUPERCENT=5
```

## Best Practices

### 1. Metric Naming Conventions
- Use lowercase with underscores
- Include unit in metric name (e.g., `_seconds`, `_bytes`)
- Group related metrics with common prefix
- Follow Prometheus naming guidelines

### 2. Label Cardinality
- Keep label cardinality low (<100 unique values)
- Use normalization for dynamic values (IDs → {id})
- Avoid high-cardinality labels like user IDs
- Pre-aggregate where possible

### 3. Resource Management
```yaml
# Monitoring overhead targets
- CPU: <5% of application CPU
- Memory: <100MB per service
- Network: <1MB/s metrics traffic
- Storage: 2GB/day retention
```

### 4. Alert Design
- **Critical**: Immediate action required (pager)
- **Warning**: Investigation needed (email)
- **Info**: Awareness only (dashboard)

Alert qualities:
- Actionable: Clear resolution steps
- Symptom-based: User impact focused
- Tested: Verified in staging
- Documented: Runbook linked

#### Cache Statistics Alerts
```yaml
# Critical: No active cache statistics instances
- alert: CacheStatisticsNoActiveInstances
  expr: conduit_cache_statistics_active_instances == 0
  for: 2m
  severity: critical
  
# Warning: High statistics aggregation latency
- alert: CacheStatisticsSlowAggregation
  expr: conduit_cache_statistics_aggregation_latency_ms > 500
  for: 5m
  severity: warning
  
# Warning: Statistics drift between instances
- alert: CacheStatisticsDrift
  expr: conduit_cache_statistics_max_drift_percentage > 10
  for: 10m
  severity: warning
```

### 5. Dashboard Guidelines
- One dashboard per service/domain
- Key metrics above the fold
- Drill-down capability
- Mobile-responsive layout
- 30-second refresh rate

## Production Deployment

### 1. High Availability Setup
```yaml
# Prometheus HA with Thanos
prometheus-1:
  external_labels:
    replica: A
    
prometheus-2:
  external_labels:
    replica: B
    
thanos-query:
  --store=prometheus-1:10901
  --store=prometheus-2:10901
```

### 2. Long-term Storage
```yaml
# S3 storage for metrics
thanos-store:
  --objstore.config=
    type: S3
    config:
      bucket: conduit-metrics
      endpoint: s3.amazonaws.com
      region: us-east-1
```

### 3. Security
```yaml
# TLS for metrics endpoints
prometheus:
  scheme: https
  tls_config:
    cert_file: /etc/prometheus/cert.pem
    key_file: /etc/prometheus/key.pem
    
# Authentication
basic_auth:
  username: prometheus
  password_file: /etc/prometheus/password
```

### 4. Scaling Considerations
- **Sharding**: Split metrics by service/region
- **Federation**: Hierarchical Prometheus setup
- **Sampling**: Reduce data for high-volume metrics
- **Retention**: 15 days hot, 1 year cold storage

## Troubleshooting

### High Memory Usage
```bash
# Check metric cardinality
curl -s http://localhost:9090/api/v1/label/__name__/values | jq '. | length'

# Find high cardinality metrics
curl -s http://localhost:9090/api/v1/query?query=prometheus_tsdb_symbol_table_size_bytes | jq
```

### Missing Metrics
```bash
# Verify endpoint accessibility
curl -s http://localhost/metrics | grep conduit_

# Check Prometheus targets
curl -s http://localhost:9090/api/v1/targets | jq '.data.activeTargets'

# Review service logs
docker logs conduit-api | grep -i metric
```

### Performance Impact
```bash
# Monitor scrape duration
histogram_quantile(0.99, prometheus_target_interval_length_seconds)

# Check metric processing time
rate(prometheus_rule_evaluation_duration_seconds_sum[5m])
```

## Integration Examples

### Custom Business Metrics
```csharp
// Track custom business events
BusinessMetricsService.RecordVirtualKeyOperation(
    operation: "create",
    status: "success",
    durationSeconds: stopwatch.Elapsed.TotalSeconds
);
```

### Alert Integration
```yaml
# PagerDuty integration
alertmanager:
  route:
    group_by: ['alertname', 'severity']
    receiver: 'pagerduty-critical'
    routes:
    - match:
        severity: critical
      receiver: pagerduty-critical
```

### Custom Dashboards
```json
{
  "dashboard": {
    "title": "Virtual Key Analytics",
    "panels": [
      {
        "title": "Keys by Status",
        "targets": [
          {
            "expr": "conduit_virtualkeys_total"
          }
        ]
      }
    ]
  }
}
```

## Maintenance

### Regular Tasks
1. **Weekly**: Review alert noise, tune thresholds
2. **Monthly**: Analyze metric cardinality, optimize queries
3. **Quarterly**: Review retention policies, capacity planning
4. **Yearly**: Architecture review, tool evaluation

### Monitoring the Monitors
```promql
# Prometheus health
up{job="prometheus"}

# Alertmanager health
alertmanager_cluster_members

# Grafana health
grafana_instance_info
```

## Additional Resources
- [Prometheus Best Practices](https://prometheus.io/docs/practices/)
- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [Grafana Dashboard Guide](https://grafana.com/docs/grafana/latest/dashboards/)
- [Runbooks](./runbooks/)