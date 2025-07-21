# Conduit Operational Runbooks

This directory contains operational runbooks for handling various incidents and alerts in the Conduit system. These runbooks are designed to help on-call engineers quickly diagnose and resolve issues.

## Critical Alerts

### [API Down](./api-down.md)
- **Severity**: Critical
- **Trigger**: API health check failing for >1 minute
- **Impact**: Complete service outage
- **First Response**: Restart API service, check logs

### [Database Connection Pool Exhausted](./db-connection-pool.md)
- **Severity**: Critical  
- **Trigger**: Available connections <10 for >2 minutes
- **Impact**: Request failures, timeouts
- **First Response**: Kill idle connections, restart service

### [High Error Rate](./high-error-rate.md)
- **Severity**: Critical
- **Trigger**: Error rate >5% for >5 minutes
- **Impact**: User-facing failures
- **First Response**: Check logs, identify error pattern

## Warning Alerts

### [High Response Time](./high-response-time.md)
- **Severity**: Warning
- **Trigger**: p95 latency >1s for >10 minutes
- **Impact**: Poor user experience
- **First Response**: Check slow queries, cache performance

### Task Queue Backup
- **Severity**: Warning
- **Trigger**: >1000 pending tasks for >5 minutes
- **Impact**: Delayed async operations
- **First Response**: Scale workers, check for failures

### SignalR Connection Limit
- **Severity**: Warning
- **Trigger**: >8000 active connections
- **Impact**: New connections rejected
- **First Response**: Check for connection leaks

### Redis High Memory Usage
- **Severity**: Warning
- **Trigger**: >8GB memory usage
- **Impact**: Potential OOM, evictions
- **First Response**: Check for memory leaks, clear cache

### Virtual Key Budget Exceeded
- **Severity**: Warning
- **Trigger**: >90% budget utilization
- **Impact**: Key may be disabled
- **First Response**: Contact customer, increase budget

### Provider Unhealthy
- **Severity**: Warning
- **Trigger**: Provider failing health checks
- **Impact**: Requests failing for provider
- **First Response**: Check provider status, failover

## Info Alerts

### Low Cache Hit Rate
- **Severity**: Info
- **Trigger**: <80% hit rate for >15 minutes
- **Impact**: Increased latency, DB load
- **First Response**: Review cache strategy

### [Cache Statistics Issues](./cache-statistics.md)
- **Severity**: Warning
- **Trigger**: Statistics showing zeros, high aggregation latency, drift
- **Impact**: Inaccurate metrics, monitoring blind spots
- **First Response**: Check Redis connectivity, verify instance registration

### Webhook Delivery Slow
- **Severity**: Info
- **Trigger**: p95 >5s for >10 minutes
- **Impact**: Delayed notifications
- **First Response**: Check webhook endpoints

### High CPU Usage
- **Severity**: Info
- **Trigger**: >80% CPU for >15 minutes
- **Impact**: Potential performance issues
- **First Response**: Profile application, scale out

## SLA Alerts

### API Availability SLA
- **Severity**: Warning
- **Trigger**: <99.9% availability in 1 hour
- **Impact**: SLA violation
- **First Response**: Review error logs, incident report

### Response Time SLA
- **Severity**: Warning
- **Trigger**: Chat completions p99 >10s
- **Impact**: SLA violation
- **First Response**: Check provider latency

## Quick Reference

### Essential Commands
```bash
# Check API health
curl -I http://localhost/health/ready

# View metrics
curl -s http://localhost/metrics | grep conduit_

# Container logs
docker logs conduit-api --tail 1000 | grep ERROR

# Database connections
psql -U conduit -c "SELECT count(*) FROM pg_stat_activity;"

# Redis status
redis-cli INFO stats
```

### Emergency Contacts
- **On-Call Engineer**: Check PagerDuty
- **Team Lead**: Check team roster
- **Platform Team**: #platform-oncall
- **Database Team**: #database-oncall

### Useful Links
- [Grafana Dashboard](https://grafana.conduit.im/d/conduit-system-overview)
- [Prometheus Alerts](https://prometheus.conduit.im/alerts)
- [Status Page](https://status.conduit.im)
- [Architecture Docs](../architecture/)

## Contributing

When creating new runbooks:
1. Use the template in [runbook-template.md](./runbook-template.md)
2. Include specific commands and expected outputs
3. Test all resolution steps in staging
4. Link related runbooks
5. Update this index

## Maintenance

Runbooks should be reviewed and updated:
- After each incident (within 48 hours)
- Quarterly review by on-call team
- When system changes affect procedures