# Technical Debt and Future Migrations

This document tracks known technical debt and future migrations that don't require immediate action but should be considered for long-term project health.

## RabbitMQ Management Metrics Deprecation

**Status**: Monitoring Only  
**Urgency**: Low (2-3 years runway)  
**Effort**: High  
**Issue**: [#XXX](https://github.com/knnlabs/Conduit/issues/XXX)

### Background
RabbitMQ has deprecated the `management_metrics_collection` feature in favor of Prometheus-based metrics. Currently, we use this feature for:
- Health check monitoring (queue depths, memory usage)
- Error queue visibility in Admin UI
- Prometheus metrics collection for error queues

### Current Mitigation
Add to `rabbitmq.conf`:
```
deprecated_features.permit.management_metrics_collection = true
```

### Future Migration Path
When RabbitMQ announces removal (or if we need the performance benefits):
1. Add Prometheus + Grafana to infrastructure
2. Rewrite RabbitMQHealthCheck to use Prometheus queries
3. Implement alternative error queue browsing (database storage recommended)
4. Create Grafana dashboards to replace management UI graphs

### Decision Criteria for Migration
- RabbitMQ announces removal date
- Memory usage becomes a problem (>50% of container limit)
- We adopt Prometheus for other services
- We need historical metrics (>24 hours)

### References
- [RabbitMQ Prometheus Docs](https://www.rabbitmq.com/prometheus.html)
- [Migration Analysis](docs/BREAKING-CHANGES-AUDIO-PROVIDER-TYPE.md#rabbitmq-metrics)
