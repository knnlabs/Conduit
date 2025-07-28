# GitHub Issue: RabbitMQ Management Metrics Deprecation

**Type**: Technical Debt / Future Migration  
**Priority**: P3 - Low  
**Labels**: `technical-debt`, `infrastructure`, `monitoring`, `breaking-change`

## Summary

RabbitMQ has deprecated the `management_metrics_collection` feature. While still functional, it will be removed in a future version. This issue tracks the eventual migration to Prometheus-based metrics.

## Current Impact

- 🟡 Warning logs in RabbitMQ container
- ✅ No functional impact
- ✅ All features working normally

## Affected Components

1. **RabbitMQHealthCheck** - Uses Management API for queue metrics
2. **ErrorQueueMetricsService** - Collects error queue statistics  
3. **ErrorQueueService** - Browses error queue messages
4. **Admin UI** - Error queue viewing functionality

## Temporary Mitigation

```yaml
# Add to rabbitmq.conf or docker/rabbitmq/rabbitmq.conf
deprecated_features.permit.management_metrics_collection = true
```

This silences warnings and maintains all functionality.

## Migration Checklist (When Needed)

- [ ] Add Prometheus to docker-compose.yml
- [ ] Configure Prometheus to scrape RabbitMQ (port 15692)
- [ ] Rewrite RabbitMQHealthCheck to query Prometheus
- [ ] Implement database storage for error message browsing
- [ ] Create Grafana dashboards for queue monitoring
- [ ] Update documentation for new monitoring stack
- [ ] Update docker-compose.yml with new environment variables

## When to Act

Consider migration when ANY of these occur:
- RabbitMQ announces removal date
- RabbitMQ memory usage exceeds 4GB
- We adopt Prometheus for other monitoring
- We need metrics history beyond 24 hours

## Estimated Effort

- **Development**: 3-5 days
- **Testing**: 2 days
- **Documentation**: 1 day
- **Risk**: Low (monitoring only, no business logic affected)

## References

- [Technical Debt Document](docs/TECHNICAL-DEBT.md)
- [RabbitMQ Prometheus Plugin](https://www.rabbitmq.com/prometheus.html)
- [Deprecation Announcement](https://github.com/rabbitmq/rabbitmq-server/releases)

---

**Note**: This is intentionally filed as a tracking issue, not an action item. No immediate work required.