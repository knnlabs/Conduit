# Metrics and Monitoring

ConduitLLM provides comprehensive monitoring capabilities through Prometheus metrics, health checks, and distributed tracing to ensure reliable operation in production environments.

## Overview

The monitoring system provides:

- **Real-time Metrics**: Prometheus-compatible metrics for all operations
- **Health Checks**: Continuous monitoring of system and provider health
- **Distributed Tracing**: Request tracking across services with correlation IDs
- **Performance Tracking**: Detailed latency and throughput measurements
- **Cost Attribution**: Per-request cost tracking and reporting
- **Custom Dashboards**: Pre-built Grafana dashboards for visualization

## Prometheus Metrics

### Enabling Metrics

Metrics are exposed on the `/metrics` endpoint when enabled:

```json
{
  "Monitoring": {
    "Prometheus": {
      "Enabled": true,
      "Endpoint": "/metrics",
      "IncludeDefaultMetrics": true
    }
  }
}
```

Or via environment variables:
```bash
CONDUIT_MONITORING_PROMETHEUS_ENABLED=true
CONDUIT_MONITORING_PROMETHEUS_ENDPOINT=/metrics
```

### Available Metrics

#### Request Metrics

```prometheus
# Total requests by provider and model
conduit_llm_requests_total{provider="openai",model="gpt-4",status="success"} 1234

# Request duration histogram
conduit_llm_request_duration_seconds_bucket{provider="openai",model="gpt-4",le="0.5"} 890
conduit_llm_request_duration_seconds_bucket{provider="openai",model="gpt-4",le="1.0"} 1100
conduit_llm_request_duration_seconds_bucket{provider="openai",model="gpt-4",le="2.0"} 1200

# Active requests gauge
conduit_llm_active_requests{provider="openai"} 5

# Token usage
conduit_llm_tokens_total{provider="openai",model="gpt-4",type="input"} 450000
conduit_llm_tokens_total{provider="openai",model="gpt-4",type="output"} 125000
```

#### Audio Metrics

```prometheus
# Audio operations
conduit_audio_operations_total{provider="openai",operation="transcription",status="success"} 850
conduit_audio_operations_total{provider="googlecloud",operation="tts",status="success"} 1200

# Audio duration processed
conduit_audio_duration_seconds_total{provider="openai",operation="transcription"} 51000

# Real-time session metrics
conduit_realtime_sessions_active{provider="openai"} 3
conduit_realtime_session_duration_seconds_bucket{provider="openai",le="60"} 45
conduit_realtime_session_duration_seconds_bucket{provider="openai",le="300"} 120
```

#### System Metrics

```prometheus
# Health check status
conduit_health_check_status{check="database",status="healthy"} 1
conduit_health_check_status{check="redis",status="healthy"} 1
conduit_health_check_status{check="providers",status="degraded"} 1

# Cache metrics
conduit_cache_hits_total{cache="response"} 3400
conduit_cache_misses_total{cache="response"} 1200
conduit_cache_size_bytes{cache="response"} 52428800

# Provider health
conduit_provider_health{provider="openai",status="healthy"} 1
conduit_provider_health{provider="anthropic",status="healthy"} 1
conduit_provider_response_time_seconds{provider="openai",quantile="0.99"} 0.845
```

### Prometheus Configuration

Example `prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'conduit'
    static_configs:
      - targets: ['conduit-api:8080']
    metrics_path: '/metrics'
    scheme: 'http'
```

## Grafana Dashboards

Pre-built dashboards are available in the `docs/grafana-dashboards/` directory:

### 1. System Overview Dashboard

Provides high-level system metrics:
- Request rate and latency
- Error rates by provider
- Token usage and costs
- Cache hit rates
- Active connections

### 2. Audio Services Dashboard

Specialized dashboard for audio operations:
- Transcription and TTS volumes
- Audio processing duration
- Real-time session metrics
- Provider-specific audio costs
- Quality metrics (WER, latency)

### 3. Provider Health Dashboard

Monitors provider availability:
- Health check status
- Response times by provider
- Error rates and types
- Circuit breaker status
- Failover events

### Importing Dashboards

```bash
# Import via Grafana API
curl -X POST http://grafana:3000/api/dashboards/db \
  -H "Authorization: Bearer $GRAFANA_API_KEY" \
  -H "Content-Type: application/json" \
  -d @audio-metrics-dashboard.json
```

## Health Checks

### System Health Endpoint

```http
GET /health/ready
```

Response:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0123456"
    },
    "redis": {
      "status": "Healthy",
      "duration": "00:00:00.0034567"
    },
    "providers": {
      "status": "Degraded",
      "duration": "00:00:00.0076543",
      "description": "1 provider unhealthy: anthropic"
    }
  }
}
```

### Liveness Check

```http
GET /health/live
```

Simple check that the service is running.

### Provider Health

```http
GET /api/admin/providers/health
X-Master-Key: your-master-key
```

Detailed provider health information:
```json
{
  "providers": [
    {
      "name": "openai",
      "status": "healthy",
      "lastChecked": "2024-01-15T10:30:00Z",
      "responseTime": 234,
      "successRate": 0.998,
      "services": {
        "chat": "healthy",
        "transcription": "healthy",
        "tts": "healthy",
        "realtime": "healthy"
      }
    },
    {
      "name": "anthropic",
      "status": "unhealthy",
      "lastChecked": "2024-01-15T10:30:00Z",
      "error": "Connection timeout",
      "consecutiveFailures": 3
    }
  ]
}
```

## Distributed Tracing

### Correlation IDs

Every request is assigned a correlation ID for tracing:

```http
GET /v1/chat/completions
X-Virtual-Key: your-key
X-Correlation-Id: 550e8400-e29b-41d4-a716-446655440000
```

The correlation ID is propagated through:
- All internal service calls
- Provider API requests
- Log entries
- Error responses

### Accessing Trace Information

Response headers include trace information:
```http
HTTP/1.1 200 OK
X-Correlation-Id: 550e8400-e29b-41d4-a716-446655440000
X-Request-Duration-Ms: 845
X-Provider-Used: openai
X-Model-Used: gpt-4
X-Cache-Hit: false
```

### Structured Logging

All logs include correlation ID and contextual information:

```json
{
  "timestamp": "2024-01-15T10:30:45.123Z",
  "level": "info",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "LLM request completed",
  "provider": "openai",
  "model": "gpt-4",
  "duration": 845,
  "inputTokens": 150,
  "outputTokens": 200,
  "cost": 0.0105
}
```

## Performance Metrics

### Enabling Performance Tracking

```json
{
  "PerformanceTracking": {
    "Enabled": true,
    "SampleRate": 1.0,
    "DetailedMetrics": true,
    "SlowRequestThreshold": 5000
  }
}
```

### Performance Data Collection

Performance metrics are collected for:
- Overall request duration
- Provider API latency
- Token generation rate
- Time to first token (streaming)
- Queue wait time
- Processing overhead

### Accessing Performance Data

```http
GET /api/admin/metrics/performance?startDate=2024-01-01&endDate=2024-01-15
X-Master-Key: your-master-key
```

Response:
```json
{
  "summary": {
    "totalRequests": 45000,
    "averageLatency": 1234,
    "p50Latency": 800,
    "p95Latency": 2500,
    "p99Latency": 4500
  },
  "byProvider": [
    {
      "provider": "openai",
      "requests": 30000,
      "averageLatency": 1100,
      "errorRate": 0.002
    }
  ],
  "byModel": [
    {
      "model": "gpt-4",
      "requests": 15000,
      "averageLatency": 1800,
      "tokensPerSecond": 45.2
    }
  ]
}
```

## Alerting

### Prometheus Alerting Rules

Example `alerts.yml`:

```yaml
groups:
  - name: conduit
    rules:
      - alert: HighErrorRate
        expr: rate(conduit_llm_requests_total{status="error"}[5m]) > 0.05
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High error rate detected"
          description: "Error rate is {{ $value }} for {{ $labels.provider }}"
      
      - alert: ProviderDown
        expr: conduit_provider_health{status="unhealthy"} == 1
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Provider {{ $labels.provider }} is down"
      
      - alert: HighLatency
        expr: histogram_quantile(0.95, conduit_llm_request_duration_seconds_bucket) > 5
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "High latency detected"
```

### Webhook Notifications

Configure webhook alerts:

```http
POST /api/admin/alerts/webhooks
X-Master-Key: your-master-key

{
  "url": "https://your-app.com/alerts",
  "events": [
    "provider.down",
    "high.error.rate",
    "quota.exceeded"
  ],
  "secret": "webhook-secret"
}
```

## Custom Metrics

### Adding Custom Metrics

```csharp
// In your code
services.AddSingleton<IMetricsCollector, PrometheusMetricsCollector>();

// Collect custom metric
_metrics.IncrementCounter("custom_operation_total", 
    new[] { ("operation", "special"), ("status", "success") });

_metrics.ObserveHistogram("custom_duration_seconds", 
    elapsedSeconds, 
    new[] { ("operation", "special") });
```

### Exposing Business Metrics

```csharp
// Track business-specific metrics
_metrics.SetGauge("active_premium_users", activePremiumUsers);
_metrics.IncrementCounter("revenue_total", revenueAmount, 
    new[] { ("currency", "usd"), ("plan", "premium") });
```

## Best Practices

1. **Metric Naming**: Follow Prometheus naming conventions
2. **Label Cardinality**: Keep label combinations under control
3. **Sampling**: Use sampling for high-volume metrics
4. **Retention**: Configure appropriate retention policies
5. **Aggregation**: Pre-aggregate where possible
6. **Alerting**: Set up alerts for critical metrics

## Troubleshooting

### Missing Metrics

1. Verify Prometheus is enabled in configuration
2. Check firewall rules for metrics endpoint
3. Ensure metrics are being collected (check logs)

### High Cardinality

1. Review label usage
2. Implement sampling for detailed metrics
3. Use recording rules for aggregation

### Performance Impact

1. Adjust sampling rates
2. Disable detailed metrics if needed
3. Use separate metrics instance

## Next Steps

- [Health Checks](health-checks.md) - Detailed health check configuration
- [Production Deployment](production-deployment.md) - Deploy monitoring stack
- [Troubleshooting Guide](../troubleshooting/common-issues.md) - Common monitoring issues