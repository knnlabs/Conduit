# Prometheus Metrics Setup Guide

This guide explains how to configure and use Prometheus metrics for monitoring audio operations in Conduit.

## Overview

Conduit now includes built-in Prometheus metrics export for comprehensive monitoring of audio operations including:
- Transcription performance and success rates
- Text-to-speech generation metrics
- Real-time session statistics
- Provider health and availability
- Cost tracking and cache efficiency
- System resource utilization

## Configuration

### 1. Enable Prometheus Metrics in Startup

Add the following to your `Program.cs` or `Startup.cs`:

```csharp
// Add audio metrics collector (if not already added)
builder.Services.AddAudioMetricsCollector(builder.Configuration);

// Add Prometheus metrics exporter
builder.Services.AddPrometheusAudioMetrics(builder.Configuration);

// In Configure method, add the metrics endpoint
app.UsePrometheusMetrics("/metrics");
```

### 2. Configure Options in appsettings.json

```json
{
  "PrometheusMetrics": {
    "ExportInterval": "00:00:15",      // Export metrics every 15 seconds
    "MetricsWindow": "00:05:00",        // Aggregate metrics over 5 minutes
    "CacheExpiration": "00:00:10"       // Cache metrics for 10 seconds
  }
}
```

### 3. Custom Configuration

You can also configure programmatically:

```csharp
builder.Services.AddPrometheusAudioMetrics(options =>
{
    options.ExportInterval = TimeSpan.FromSeconds(30);
    options.MetricsWindow = TimeSpan.FromMinutes(10);
    options.CacheExpiration = TimeSpan.FromSeconds(5);
});
```

## Available Metrics

### Request Metrics
- `conduit_audio_requests_total` - Total number of audio requests (counter)
  - Labels: `operation`, `provider`, `status`
  
### Performance Metrics
- `conduit_audio_request_duration_seconds` - Request duration histogram
  - Labels: `operation`, `provider`
  - Buckets: 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0, 60.0, +Inf

### Size Metrics  
- `conduit_audio_request_size_bytes` - Request/response size histogram
  - Labels: `operation`, `provider`
  - Buckets: 1KB, 10KB, 100KB, 1MB, 10MB, +Inf

### Cache Metrics
- `conduit_audio_cache_hit_ratio` - Cache hit ratio (gauge)
  - Labels: `operation`

### Provider Metrics
- `conduit_audio_provider_error_rate` - Provider error rate (gauge)
  - Labels: `provider`
- `conduit_audio_provider_uptime_ratio` - Provider uptime ratio (gauge)
  - Labels: `provider`

### Active Operations
- `conduit_audio_active_operations` - Currently active operations (gauge)
  - Labels: `operation` or `resource`

### Real-time Metrics
- `conduit_audio_realtime_sessions_total` - Total realtime sessions (counter)
  - Labels: `provider`, `disconnect_reason`
- `conduit_audio_realtime_duration_seconds` - Session duration histogram
  - Labels: `provider`
- `conduit_audio_realtime_latency_seconds` - Latency histogram
  - Labels: `provider`

### Cost Metrics
- `conduit_audio_cost_dollars` - Operation costs in dollars (counter)
  - Labels: `operation`

## Prometheus Configuration

### prometheus.yml Example

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'conduit-audio'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
```

## Grafana Dashboard

### Import Dashboard

A pre-built Grafana dashboard is available at `docs/grafana-dashboards/audio-metrics.json`.

To import:
1. Open Grafana
2. Go to Dashboards → Import
3. Upload the JSON file or paste its contents
4. Select your Prometheus data source
5. Click Import

### Key Panels

1. **Request Rate** - Requests per second by operation type
2. **Success Rate** - Success percentage by provider
3. **Latency Percentiles** - P50, P95, P99 latencies
4. **Provider Health** - Uptime and error rates
5. **Cost Analysis** - Running costs and cache savings
6. **Active Operations** - Real-time operation counts
7. **System Resources** - CPU, memory, connections

## Alerting Rules

### Example Prometheus Alert Rules

```yaml
groups:
  - name: audio_alerts
    rules:
      - alert: HighErrorRate
        expr: conduit_audio_provider_error_rate > 0.05
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High error rate for provider {{ $labels.provider }}"
          description: "Error rate is {{ $value }} for provider {{ $labels.provider }}"

      - alert: HighLatency
        expr: histogram_quantile(0.95, rate(conduit_audio_request_duration_seconds_bucket[5m])) > 5
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High P95 latency for {{ $labels.operation }}"
          description: "P95 latency is {{ $value }}s"

      - alert: ProviderDown
        expr: conduit_audio_provider_uptime_ratio < 0.95
        for: 10m
        labels:
          severity: critical
        annotations:
          summary: "Provider {{ $labels.provider }} has low uptime"
          description: "Uptime is {{ $value }} for provider {{ $labels.provider }}"
```

## Security Considerations

1. **Authentication**: The metrics endpoint is not authenticated by default. Consider:
   - Using a reverse proxy with authentication
   - Implementing custom authentication middleware
   - Restricting access by IP address

2. **Sensitive Data**: Metrics do not contain sensitive user data, but may reveal:
   - Usage patterns
   - Cost information
   - Provider relationships

3. **Network Security**: 
   - Use HTTPS in production
   - Consider VPN or private network access
   - Implement rate limiting

## Performance Impact

The Prometheus exporter has minimal performance impact:
- Metrics are aggregated asynchronously
- Results are cached between scrapes
- No blocking operations in request path
- Memory usage scales with metric cardinality

## Troubleshooting

### No Metrics Appearing

1. Check the service is registered:
   ```csharp
   services.AddPrometheusAudioMetrics(configuration);
   ```

2. Verify middleware is added:
   ```csharp
   app.UsePrometheusMetrics();
   ```

3. Check logs for errors:
   ```
   grep "PrometheusAudioMetricsExporter" logs.txt
   ```

### High Memory Usage

Reduce metric cardinality by:
- Increasing aggregation interval
- Reducing retention period
- Limiting provider labels

### Incomplete Metrics

Ensure AudioMetricsCollector is properly configured and receiving events from audio services.

## Next Steps

1. Set up Prometheus server to scrape metrics
2. Import Grafana dashboards
3. Configure alerting rules
4. Implement custom business metrics
5. Set up long-term storage (e.g., Thanos)