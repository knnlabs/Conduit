# Conduit Cache Statistics Monitoring

This directory contains monitoring configuration for the distributed cache statistics system.

## Overview

The cache statistics monitoring system provides:

1. **Health Checks** - Real-time health status of the distributed statistics system
2. **Accuracy Monitoring** - Validation of statistics consistency across instances
3. **Performance Monitoring** - Tracking of latencies and throughput
4. **Alerting** - Automated alerts for anomalies and issues

## Components

### Health Check Endpoint

The statistics health is exposed through the ASP.NET Core health check endpoint:

```
GET /health
```

Specific cache statistics health:

```
GET /health/cache_statistics
```

### Metrics Exposed

The following Prometheus metrics are exposed:

- `cache_hits_total{region}` - Total cache hits by region
- `cache_misses_total{region}` - Total cache misses by region
- `cache_hit_rate{region}` - Hit rate percentage by region
- `cache_response_time_milliseconds{region,quantile}` - Response time percentiles
- `conduit_cache_statistics_active_instances` - Number of active instances
- `conduit_cache_statistics_aggregation_latency_ms` - Aggregation latency
- `conduit_cache_statistics_redis_memory_mb` - Redis memory usage

### Alert Thresholds

Default alerting thresholds:

- **Instance Missing**: > 1 minute without heartbeat
- **High Aggregation Latency**: > 500ms
- **Statistics Drift**: > 5% between instances
- **High Redis Memory**: > 1GB
- **High Recording Latency P99**: > 10ms
- **Low Active Instances**: < 1 instance

## Grafana Dashboard

Import the dashboard from `grafana/dashboards/cache-statistics-monitoring.json`.

Features:
- Cache hit rate trends by region
- Active instances gauge
- Aggregation latency gauge
- Response time percentiles
- Redis memory usage
- Active alerts table

## Configuration

### Enable Monitoring

In `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "CacheStatistics": {
    "AggregationInterval": "00:01:00",
    "PersistenceInterval": "00:05:00",
    "MaxResponseTimeSamples": 1000
  },
  "StatisticsAlertThresholds": {
    "MaxInstanceMissingTime": "00:01:00",
    "MaxAggregationLatency": "00:00:00.500",
    "MaxDriftPercentage": 5.0,
    "MaxRedisMemoryBytes": 1073741824,
    "MaxRecordingLatencyP99Ms": 10.0,
    "MinActiveInstances": 1
  }
}
```

### Prometheus Configuration

Add to `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'conduit'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
```

## Alerts

### Prometheus Alert Rules

Create `alerts/cache-statistics.yml`:

```yaml
groups:
  - name: cache_statistics
    rules:
      - alert: CacheStatisticsInstanceMissing
        expr: conduit_cache_statistics_active_instances < 1
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "No active cache statistics instances"
          description: "No cache statistics collector instances are reporting"

      - alert: CacheStatisticsHighAggregationLatency
        expr: conduit_cache_statistics_aggregation_latency_ms > 500
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High cache statistics aggregation latency"
          description: "Aggregation latency is {{ $value }}ms"

      - alert: CacheStatisticsHighRedisMemory
        expr: conduit_cache_statistics_redis_memory_mb > 1024
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High Redis memory usage for cache statistics"
          description: "Redis memory usage is {{ $value }}MB"

      - alert: CacheStatisticsLowHitRate
        expr: cache_hit_rate < 50
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Low cache hit rate in region {{ $labels.region }}"
          description: "Hit rate is {{ $value }}%"
```

## Troubleshooting

### Common Issues

1. **Instances Not Reporting**
   - Check Redis connectivity
   - Verify instance registration on startup
   - Check for network partitions

2. **High Aggregation Latency**
   - Check Redis performance
   - Reduce number of regions being aggregated
   - Enable aggregation caching

3. **Statistics Drift**
   - Check for clock skew between instances
   - Verify all instances using same Redis
   - Check for duplicate instance IDs

### Debug Logging

Enable debug logging for detailed information:

```json
{
  "Logging": {
    "LogLevel": {
      "ConduitLLM.Core.Services.CacheStatisticsHealthCheck": "Debug",
      "ConduitLLM.Core.Services.RedisCacheStatisticsCollector": "Debug"
    }
  }
}
```

## Performance Tuning

1. **Reduce Aggregation Frequency**
   - Increase health check interval
   - Cache aggregation results

2. **Optimize Redis**
   - Use Redis cluster for large deployments
   - Enable Redis persistence
   - Monitor Redis slow log

3. **Instance Management**
   - Limit number of active instances
   - Use instance groups for large deployments

## Integration with Existing Monitoring

The cache statistics monitoring integrates with:

- **Health Checks** - Standard ASP.NET Core health check infrastructure
- **Prometheus** - Metrics exposed in Prometheus format
- **Grafana** - Pre-built dashboard included
- **Alerts** - Works with standard Prometheus alerting

## Development

To test monitoring locally:

1. Start Redis: `docker run -d -p 6379:6379 redis`
2. Enable monitoring in configuration
3. Start multiple instances on different ports
4. Access health endpoint: `http://localhost:5000/health`
5. View metrics: `http://localhost:5000/metrics`