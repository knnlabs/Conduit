# Cache Statistics Runbook

## Overview
This runbook covers troubleshooting and maintenance procedures for the distributed cache statistics system.

## Quick Actions

| Issue | Command | Expected Result |
|-------|---------|-----------------|
| Check health | `curl http://localhost:5000/health/cache_statistics` | Status: Healthy |
| View active instances | `redis-cli ZRANGE conduit:cache:instances 0 -1 WITHSCORES` | List of instance IDs |
| Force aggregation | `curl -X POST http://localhost:5000/api/cache/stats/aggregate` | 200 OK |

## Common Issues

### Statistics showing zeros

**Symptoms:**
- Hit rate shows 0%
- All counters at zero
- Health check reports degraded

**Resolution:**
1. Check if instances are registered:
   ```bash
   redis-cli ZRANGE conduit:cache:instances 0 -1
   ```

2. Verify Redis connectivity:
   ```bash
   redis-cli PING
   ```

3. Check configuration - statistics must be enabled
4. Ensure unique instance IDs
5. Restart affected instances if needed

### High aggregation latency

**Symptoms:**
- Aggregation takes > 500ms
- API responses slow
- Health check warnings

**Resolution:**
1. Count active instances:
   ```bash
   redis-cli ZCARD conduit:cache:instances
   ```

2. Enable aggregation caching in configuration
3. Reduce number of tracked regions
4. Consider Redis Cluster for large deployments

### Instance not appearing in statistics

**Symptoms:**
- New instance deployed but not in active list
- Statistics don't include all pods

**Resolution:**
1. Verify `INSTANCE_ID` environment variable is set
2. Check Redis connectivity from pod
3. Verify statistics are enabled in configuration
4. Review startup logs for errors

### Memory usage growing

**Symptoms:**
- Redis memory constantly increasing
- OOM errors

**Resolution:**
1. Check TTL on statistics keys
2. Enable key eviction policy in Redis
3. Reduce retention period
4. Clean up stale instances:
   ```bash
   # Remove instances inactive for > 5 minutes
   redis-cli EVAL "$(cat <<'EOF'
   local current = tonumber(ARGV[1])
   local timeout = 300
   local instances = redis.call('ZRANGE', 'conduit:cache:instances', 0, -1, 'WITHSCORES')
   local removed = 0
   for i = 1, #instances, 2 do
       if current - tonumber(instances[i + 1]) > timeout then
           redis.call('ZREM', 'conduit:cache:instances', instances[i])
           removed = removed + 1
       end
   end
   return removed
   EOF
   )" 0 $(date +%s)
   ```

## Maintenance Tasks

### Daily
- Verify all instances reporting
- Check aggregation latency
- Monitor Redis memory usage

### Weekly
- Clean up stale instances
- Archive old statistics
- Review performance baselines

### Monthly
- Analyze metric cardinality
- Optimize Redis configuration
- Review retention policies

## Emergency Procedures

### Disable Statistics
If statistics are causing performance issues:

1. Via environment variable:
   ```bash
   export CACHE_STATISTICS_ENABLED=false
   ```

2. Via configuration:
   ```json
   {
     "CacheStatistics": {
       "Enabled": false
     }
   }
   ```

### Reset Statistics
To completely reset statistics:

```bash
# Stop all instances
kubectl scale deployment conduit --replicas=0

# Backup current data
redis-cli --rdb backup-$(date +%s).rdb

# Clear statistics
redis-cli EVAL "return redis.call('DEL', unpack(redis.call('KEYS', 'conduit:cache:*')))" 0

# Restart instances
kubectl scale deployment conduit --replicas=3
```

## Monitoring Alerts

Configure these alerts in your monitoring system:

```yaml
# No active instances
- alert: CacheStatisticsNoActiveInstances
  expr: conduit_cache_statistics_active_instances == 0
  for: 2m
  severity: critical
  
# High aggregation latency
- alert: CacheStatisticsSlowAggregation
  expr: conduit_cache_statistics_aggregation_latency_ms > 500
  for: 5m
  severity: warning
  
# Statistics drift
- alert: CacheStatisticsDrift
  expr: conduit_cache_statistics_max_drift_percentage > 10
  for: 10m
  severity: warning
```