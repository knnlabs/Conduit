# Grafana Dashboards for Conduit Audio Monitoring

This directory contains pre-configured Grafana dashboards for monitoring the Conduit audio API and tracing system.

## Available Dashboards

### 1. Audio Metrics Dashboard (`audio-metrics-dashboard.json`)
Provides comprehensive metrics for audio operations:
- **Audio Operations Rate**: Real-time request rate by operation type
- **Audio Success Rate**: Overall success percentage with gauge visualization
- **P95 Latency**: 95th percentile latency across all operations
- **Latency Percentiles**: P50, P95, and P99 latency trends by operation
- **Request Size Metrics**: P95 request sizes by operation type
- **Active Connections**: Current WebSocket connections by provider
- **Error Rate**: Error trends by operation and error type
- **Latency Heatmap**: Visual distribution of request latencies
- **Top Providers**: Table showing providers with highest request rates

### 2. Audio Traces Dashboard (`audio-traces-dashboard.json`)
Focuses on distributed tracing metrics:
- **Trace Statistics**: Total traces, active traces, P95 duration, errors/min
- **Trace Creation Rate**: Traces per second by operation and status
- **Trace Duration Percentiles**: P95 and P99 trace durations by operation
- **Span Metrics**: Total spans and average spans per trace
- **Error Analysis**: Top error types table
- **Performance Analysis**: Slowest operations by P95 duration

## Prerequisites

1. **Prometheus**: Ensure Prometheus is configured to scrape metrics from:
   - Conduit HTTP API (`/metrics` endpoint)
   - Audio service metrics endpoint

2. **Grafana**: Version 8.0 or higher recommended

## Installation

### Import Dashboards

1. Open Grafana web interface
2. Navigate to **Dashboards** → **Import**
3. Choose one of these methods:
   - Upload JSON file directly
   - Copy and paste the JSON content
   - Import from grafana.com (if published)
4. Select your Prometheus data source
5. Click **Import**

### Configure Data Source

Ensure your Prometheus data source is properly configured:
1. Go to **Configuration** → **Data Sources**
2. Add or verify Prometheus data source
3. Set the URL to your Prometheus server (e.g., `http://prometheus:9090`)
4. Test the connection

## Customization

### Adjusting Time Ranges
- Default time range: Last 1 hour
- Refresh interval: 10 seconds
- Both can be adjusted using Grafana's time picker

### Adding Alerts

To add alerts to these dashboards:
1. Edit the panel you want to alert on
2. Go to the **Alert** tab
3. Configure alert conditions and notifications

Example alert conditions:
- Audio success rate < 95%
- P95 latency > 500ms
- Active traces > 100
- Error rate > 10 per minute

### Modifying Queries

Common query patterns used:

```promql
# Rate calculation
rate(conduit_audio_operations_total[5m])

# Success rate
sum(rate(conduit_audio_operations_total{status="ok"}[5m])) / 
sum(rate(conduit_audio_operations_total[5m]))

# Histogram percentiles
histogram_quantile(0.95, sum(rate(conduit_audio_request_duration_seconds_bucket[5m])) by (le))
```

## Metric Reference

### Audio Operation Metrics
- `conduit_audio_operations_total`: Counter of audio operations
  - Labels: `operation`, `provider`, `status`, `error_type`
- `conduit_audio_request_duration_seconds`: Histogram of request durations
  - Labels: `operation`, `provider`
- `conduit_audio_request_size_bytes`: Histogram of request sizes
  - Labels: `operation`, `provider`
- `conduit_audio_active_connections`: Gauge of active WebSocket connections
  - Labels: `provider`

### Trace Metrics
- `conduit_audio_traces_total`: Counter of traces created
  - Labels: `operation`, `status`, `error_type`
- `conduit_audio_traces_active`: Gauge of currently active traces
- `conduit_audio_trace_duration_seconds`: Histogram of trace durations
  - Labels: `operation`
- `conduit_audio_trace_spans_total`: Counter of spans created
  - Labels: `operation`
- `conduit_audio_trace_spans_per_trace`: Gauge of average spans per trace
  - Labels: `operation`

## Best Practices

1. **Resource Usage**: These dashboards can be resource-intensive with high-cardinality data. Consider:
   - Adjusting refresh rates for large deployments
   - Using recording rules for frequently-queried metrics
   - Setting appropriate data retention policies

2. **Dashboard Organization**: 
   - Keep operational metrics (audio-metrics) separate from debugging tools (traces)
   - Create role-specific dashboards for different teams
   - Use dashboard folders to organize by service or team

3. **Performance Optimization**:
   - Limit time ranges for expensive queries
   - Use dashboard variables to filter by specific providers or operations
   - Consider using Grafana's query caching features

## Troubleshooting

### No Data Points
- Verify Prometheus is scraping the `/metrics` endpoint
- Check that the application is generating metrics
- Ensure label names match between queries and metrics

### High Memory Usage
- Reduce the number of panels with auto-refresh
- Increase the refresh interval
- Use more specific label selectors in queries

### Slow Dashboard Loading
- Optimize queries using recording rules
- Reduce the default time range
- Limit the number of series returned by top-k queries

## Support

For issues or questions:
1. Check Prometheus targets page for scraping errors
2. Verify metrics are being exposed using: `curl http://your-app:port/metrics`
3. Review Grafana query inspector for detailed error messages