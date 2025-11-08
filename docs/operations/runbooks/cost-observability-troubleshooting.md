# Cost Observability Troubleshooting Guide

## Quick Reference

| Symptom | Likely Cause | Quick Fix | Page |
|---------|-------------|-----------|------|
| No cost metrics appearing | Prometheus not scraping | Check scrape config | [Metrics Not Appearing](#metrics-not-appearing) |
| Costs not updating | Batch processor stuck | Restart batch processor | [Cost Updates Stuck](#cost-updates-stuck) |
| Wrong cost calculations | Outdated pricing config | Clear cache, verify config | [Incorrect Cost Calculations](#incorrect-cost-calculations) |
| Dashboard loading slowly | Query performance issues | Use recording rules | [Dashboard Performance](#dashboard-performance-issues) |
| Budget alerts not firing | AlertManager misconfigured | Verify alert rules | [Alert Issues](#alert-not-firing) |
| Cache hit ratio low | Cache invalidation storm | Increase TTL | [Cache Problems](#cache-performance-issues) |

---

## Common Issues and Solutions

### Metrics Not Appearing

#### Symptom
Prometheus metrics for cost observability are not showing up in Grafana or queries return empty results.

#### Diagnostic Steps

1. **Check if metrics are being exposed**
   ```bash
   # Test metrics endpoint directly
   curl -s http://localhost:8080/metrics | grep conduit_cost
   
   # Expected output:
   # conduit_cost_total_dollars{provider="openai",model="gpt-4"} 1234.56
   ```

2. **Verify Prometheus is scraping**
   ```bash
   # Check Prometheus targets
   curl -s http://prometheus:9090/api/v1/targets | jq '.data.activeTargets[] | select(.labels.job=="conduit") | {health, lastError, lastScrape}'
   
   # Check scrape configuration
   curl -s http://prometheus:9090/api/v1/targets | grep -A5 conduit
   ```

3. **Check service registration**
   ```bash
   # Verify service is registered correctly
   kubectl get service conduit-api -o yaml | grep -A5 "prometheus.io"
   ```

#### Solutions

**Solution 1: Fix Prometheus Scrape Configuration**
```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'conduit'
    scrape_interval: 15s
    static_configs:
      - targets: ['conduit-api:8080']
    metrics_path: '/metrics'
```

**Solution 2: Fix Service Annotations**
```yaml
# Kubernetes service annotation
metadata:
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "8080"
    prometheus.io/path: "/metrics"
```

**Solution 3: Restart Metrics Exporter**
```bash
# Restart the metrics service
kubectl rollout restart deployment/conduit-api

# Verify metrics are now exposed
sleep 30
curl -s http://localhost:8080/metrics | head -20
```

---

### Cost Updates Stuck

#### Symptom
Virtual key spend totals are not updating despite ongoing usage. Batch processing appears to be lagging or stopped.

#### Diagnostic Steps

1. **Check batch processor status**
   ```bash
   # Check if batch processor is running
   kubectl get pods -l app=batch-processor
   
   # Check logs for errors
   kubectl logs -l app=batch-processor --tail=100 | grep -E "ERROR|WARN"
   ```

2. **Check batch processing metrics**
   ```promql
   # Check batch lag
   conduit_batch_spend_update_lag_seconds
   
   # Check failure rate
   rate(conduit_batch_spend_update_failures_total[5m])
   ```

3. **Inspect database queue**
   ```sql
   -- Check pending updates
   SELECT COUNT(*), MIN(created_at), MAX(created_at)
   FROM spend_update_queue;
   
   -- Check for stuck transactions
   SELECT pid, state, query_start, query
   FROM pg_stat_activity
   WHERE state != 'idle' 
     AND query_start < NOW() - INTERVAL '5 minutes';
   ```

#### Solutions

**Solution 1: Clear Stuck Batches**
```sql
-- Move stuck items to dead letter queue
BEGIN;
INSERT INTO spend_update_dlq (virtual_key_id, amount, created_at, error)
SELECT virtual_key_id, amount, created_at, 'Manual intervention - stuck batch'
FROM spend_update_queue
WHERE created_at < NOW() - INTERVAL '30 minutes';

DELETE FROM spend_update_queue
WHERE created_at < NOW() - INTERVAL '30 minutes';
COMMIT;
```

**Solution 2: Force Batch Processing**
```bash
# Manually trigger batch processing
curl -X POST http://admin-api:5002/api/batch/process \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"force": true}'

# Monitor processing
watch -n 5 'curl -s http://prometheus:9090/api/v1/query?query=conduit_batch_spend_update_lag_seconds | jq .data.result[0].value[1]'
```

**Solution 3: Restart Batch Processor**
```bash
# Scale down
kubectl scale deployment batch-processor --replicas=0

# Clear Redis queue
redis-cli DEL "batch:queue"

# Scale back up
kubectl scale deployment batch-processor --replicas=1

# Verify processing resumed
kubectl logs -f deployment/batch-processor | grep "Processing batch"
```

---

### Incorrect Cost Calculations

#### Symptom
Costs being calculated don't match expected values based on provider pricing.

#### Diagnostic Steps

1. **Verify pricing configuration**
   ```sql
   -- Check active cost configurations
   SELECT 
     mc.id,
     mc.cost_name,
     mc.input_token_cost,
     mc.output_token_cost,
     mc.effective_date,
     mc.priority
   FROM model_cost mc
   WHERE mc.is_active = true
     AND mc.effective_date <= NOW()
     AND (mc.expiry_date IS NULL OR mc.expiry_date > NOW())
   ORDER BY mc.priority DESC;
   ```

2. **Test cost calculation**
   ```bash
   # Test calculation endpoint
   curl -X POST http://admin-api:5002/api/costs/calculate \
     -H "Content-Type: application/json" \
     -d '{
       "model": "gpt-4",
       "prompt_tokens": 1000,
       "completion_tokens": 500
     }'
   ```

3. **Check cache consistency**
   ```bash
   # Check cached values
   redis-cli GET "model:cost:gpt-4"
   
   # Compare with database
   psql -c "SELECT * FROM model_cost WHERE cost_name LIKE '%gpt-4%'"
   ```

#### Solutions

**Solution 1: Clear Cost Cache**
```bash
# Clear all cost cache entries
redis-cli --scan --pattern "model:cost:*" | xargs redis-cli DEL

# Verify cache cleared
redis-cli --scan --pattern "model:cost:*" | wc -l  # Should be 0
```

**Solution 2: Fix Priority Issues**
```sql
-- Fix overlapping cost configurations
UPDATE model_cost
SET priority = priority * 10
WHERE cost_name LIKE '%2024%';

-- Ensure only one active cost per model
WITH ranked_costs AS (
  SELECT 
    mc.id,
    ROW_NUMBER() OVER (
      PARTITION BY mcm.model_provider_mapping_id 
      ORDER BY mc.priority DESC, mc.effective_date DESC
    ) as rank
  FROM model_cost mc
  JOIN model_cost_mapping mcm ON mc.id = mcm.model_cost_id
  WHERE mc.is_active = true
)
UPDATE model_cost_mapping
SET is_active = false
WHERE id IN (
  SELECT mcm.id 
  FROM model_cost_mapping mcm
  JOIN ranked_costs rc ON mcm.model_cost_id = rc.id
  WHERE rc.rank > 1
);
```

**Solution 3: Recalculate Historical Costs**
```bash
# Recalculate costs for affected period
./scripts/recalculate-costs.sh \
  --start-date "2024-01-01" \
  --end-date "2024-01-31" \
  --models "gpt-4,gpt-3.5-turbo" \
  --dry-run false
```

---

### Dashboard Performance Issues

#### Symptom
Grafana dashboards loading slowly or timing out.

#### Diagnostic Steps

1. **Identify slow queries**
   ```bash
   # Enable Prometheus query logging
   echo "query_log_file: /var/log/prometheus/queries.log" >> /etc/prometheus/prometheus.yml
   kill -HUP $(pgrep prometheus)
   
   # Find slow queries
   tail -f /var/log/prometheus/queries.log | jq 'select(.duration_seconds > 1) | {query, duration_seconds}'
   ```

2. **Check cardinality**
   ```promql
   # Check metric cardinality
   count by (__name__)({__name__=~"conduit_.*"})
   
   # Find high cardinality labels
   count(count by (virtual_key_id) (conduit_virtualkey_spend_total))
   ```

3. **Review dashboard queries**
   ```bash
   # Export dashboard and analyze queries
   curl -s http://grafana:3000/api/dashboards/uid/cost-observability \
     -H "Authorization: Bearer $GRAFANA_API_KEY" | \
     jq '.dashboard.panels[].targets[].expr' | sort | uniq -c | sort -rn
   ```

#### Solutions

**Solution 1: Implement Recording Rules**
```yaml
# prometheus-rules.yml
groups:
  - name: cost_recording_rules
    interval: 30s
    rules:
      - record: conduit:hourly_cost
        expr: sum(increase(conduit_cost_total_dollars[1h]))
      
      - record: conduit:cost_by_provider
        expr: sum by (provider) (rate(conduit_cost_total_dollars[5m]))
      
      - record: conduit:top10_spending_keys
        expr: topk(10, conduit_virtualkey_spend_total)
```

**Solution 2: Optimize Queries**
```promql
# Before: Heavy aggregation
sum by (model) (
  rate(conduit_cost_total_dollars[1h])
) * 3600

# After: Use recording rule
conduit:hourly_cost_by_model
```

**Solution 3: Reduce Dashboard Refresh Rate**
```json
{
  "refresh": "1m",  // Changed from "10s"
  "panels": [{
    "datasource": {
      "type": "prometheus",
      "uid": "${datasource}"
    },
    "maxDataPoints": 300,  // Limit data points
    "interval": "1m"  // Minimum interval
  }]
}
```

---

### Alert Not Firing

#### Symptom
Cost alerts are not triggering despite thresholds being exceeded.

#### Diagnostic Steps

1. **Check alert rule evaluation**
   ```bash
   # Check if rules are loaded
   curl -s http://prometheus:9090/api/v1/rules | jq '.data.groups[].rules[] | select(.name | contains("Cost"))'
   
   # Check alert state
   curl -s http://prometheus:9090/api/v1/alerts | jq '.data.alerts[] | select(.labels.alertname | contains("Cost"))'
   ```

2. **Verify AlertManager configuration**
   ```bash
   # Check AlertManager status
   curl -s http://alertmanager:9093/api/v1/status
   
   # Check if alerts are being received
   curl -s http://alertmanager:9093/api/v1/alerts | jq '.[] | {labels, status}'
   ```

3. **Test alert routing**
   ```bash
   # Send test alert
   curl -X POST http://alertmanager:9093/api/v1/alerts \
     -H "Content-Type: application/json" \
     -d '[{
       "labels": {
         "alertname": "TestCostAlert",
         "severity": "critical"
       },
       "annotations": {
         "summary": "Test cost alert"
       }
     }]'
   ```

#### Solutions

**Solution 1: Fix Alert Rule Syntax**
```yaml
# Fixed alert rule
groups:
  - name: cost_alerts
    rules:
      - alert: HighCostBurnRate
        expr: sum(conduit_cost_rate_dollars_per_minute) > 100
        for: 5m  # Must exceed threshold for 5 minutes
        labels:
          severity: critical
        annotations:
          summary: "High cost burn rate: ${{ $value }}/min"
```

**Solution 2: Fix AlertManager Routing**
```yaml
# alertmanager.yml
route:
  group_by: ['alertname', 'severity']
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 12h
  receiver: 'cost-alerts'
  routes:
    - match:
        alertname: HighCostBurnRate
      receiver: 'critical-cost-alerts'
      continue: true

receivers:
  - name: 'critical-cost-alerts'
    email_configs:
      - to: 'oncall@example.com'
        require_tls: true
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_KEY'
```

**Solution 3: Reload Configuration**
```bash
# Reload Prometheus configuration
curl -X POST http://prometheus:9090/-/reload

# Reload AlertManager configuration
curl -X POST http://alertmanager:9093/-/reload

# Verify rules are active
curl -s http://prometheus:9090/api/v1/rules | grep -i cost
```

---

### Cache Performance Issues

#### Symptom
Redis cache hit ratio is low, causing increased database load and slower cost lookups.

#### Diagnostic Steps

1. **Check cache metrics**
   ```bash
   # Get Redis statistics
   redis-cli INFO stats | grep -E "keyspace_hits|keyspace_misses|evicted_keys"
   
   # Calculate hit ratio
   redis-cli INFO stats | awk -F: '/keyspace_hits/{hits=$2} /keyspace_misses/{misses=$2} END {print "Hit ratio:", hits/(hits+misses)*100 "%"}'
   ```

2. **Monitor cache operations**
   ```bash
   # Monitor cache operations in real-time
   redis-cli MONITOR | grep -E "GET|SET|DEL" | head -20
   
   # Check for invalidation patterns
   redis-cli MONITOR | grep DEL | cut -d'"' -f2 | sort | uniq -c | sort -rn
   ```

3. **Check memory usage**
   ```bash
   # Check memory stats
   redis-cli INFO memory
   
   # Check if hitting memory limits
   redis-cli CONFIG GET maxmemory
   ```

#### Solutions

**Solution 1: Increase Cache TTL**
```bash
# Update cache TTL configuration
export REDIS_COST_CACHE_TTL=600  # 10 minutes instead of 5

# Update application configuration
kubectl set env deployment/conduit-api REDIS_COST_CACHE_TTL=600

# Verify new TTL
redis-cli TTL "model:cost:gpt-4"
```

**Solution 2: Optimize Cache Keys**
```python
# Implement cache warming
def warm_cache():
    models = get_frequently_used_models(limit=100)
    for model in models:
        cost = fetch_model_cost(model)
        redis.setex(
            f"model:cost:{model}",
            ttl=600,
            value=json.dumps(cost)
        )

# Schedule cache warming
schedule.every(5).minutes.do(warm_cache)
```

**Solution 3: Fix Memory Issues**
```bash
# Increase Redis memory limit
redis-cli CONFIG SET maxmemory 10gb

# Set eviction policy
redis-cli CONFIG SET maxmemory-policy allkeys-lru

# Persist configuration
redis-cli CONFIG REWRITE
```

---

### Virtual Key Budget Not Enforcing

#### Symptom
Virtual keys continue to work despite exceeding budget limits.

#### Diagnostic Steps

1. **Check budget configuration**
   ```sql
   -- Verify budget settings
   SELECT 
     id,
     name,
     budget_amount,
     current_period_spend,
     hard_limit_enabled,
     budget_period
   FROM virtual_keys
   WHERE id = 'AFFECTED_KEY_ID';
   ```

2. **Check enforcement logic**
   ```bash
   # Test budget check endpoint
   curl -X GET "http://api:5000/api/budget/check?key_id=AFFECTED_KEY_ID" \
     -H "Authorization: Bearer $ADMIN_TOKEN"
   ```

3. **Review rate limiting**
   ```bash
   # Check if rate limiting is applied
   redis-cli GET "ratelimit:AFFECTED_KEY_ID"
   
   # Check blocked keys
   redis-cli SMEMBERS "blocked_keys"
   ```

#### Solutions

**Solution 1: Fix Budget Calculation**
```sql
-- Recalculate current period spend
UPDATE virtual_keys vk
SET current_period_spend = (
  SELECT COALESCE(SUM(total_cost), 0)
  FROM llm_usage_log ul
  WHERE ul.virtual_key_id = vk.id
    AND ul.created_at >= vk.period_start_date
)
WHERE vk.id = 'AFFECTED_KEY_ID';
```

**Solution 2: Enable Hard Limits**
```sql
-- Enable hard budget enforcement
UPDATE virtual_keys
SET 
  hard_limit_enabled = true,
  updated_at = NOW()
WHERE id = 'AFFECTED_KEY_ID';

-- Add to blocked list if over budget
INSERT INTO blocked_virtual_keys (virtual_key_id, reason, blocked_at)
SELECT id, 'Budget exceeded', NOW()
FROM virtual_keys
WHERE current_period_spend >= budget_amount
  AND hard_limit_enabled = true;
```

**Solution 3: Fix Middleware**
```python
# Fix budget check middleware
class BudgetEnforcementMiddleware:
    def process_request(self, request):
        key_id = extract_key_id(request)
        
        # Check budget
        budget_status = check_budget(key_id)
        
        if budget_status['exceeded'] and budget_status['hard_limit']:
            # Block request immediately
            return JsonResponse(
                {"error": "Budget exceeded"},
                status=429
            )
        elif budget_status['utilization'] > 0.9:
            # Apply rate limiting
            apply_rate_limit(key_id, requests_per_minute=10)
```

---

## Performance Tuning

### Database Query Optimization

#### Slow Cost Aggregation Queries

**Problem**: Aggregation queries taking too long

**Solution**:
```sql
-- Add appropriate indexes
CREATE INDEX CONCURRENTLY idx_usage_log_created_key 
ON llm_usage_log(created_at, virtual_key_id);

CREATE INDEX CONCURRENTLY idx_usage_log_model_cost 
ON llm_usage_log(model, total_cost);

-- Partition large tables
CREATE TABLE llm_usage_log_2024_01 PARTITION OF llm_usage_log
FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');

-- Use materialized views for common aggregations
CREATE MATERIALIZED VIEW daily_cost_summary AS
SELECT 
  DATE(created_at) as date,
  virtual_key_id,
  model,
  SUM(total_cost) as total_cost,
  COUNT(*) as request_count
FROM llm_usage_log
GROUP BY DATE(created_at), virtual_key_id, model;

CREATE UNIQUE INDEX ON daily_cost_summary(date, virtual_key_id, model);
```

### Redis Performance Tuning

#### High Memory Usage

**Problem**: Redis using too much memory

**Solution**:
```bash
# Analyze memory usage
redis-cli --bigkeys

# Set memory limits
redis-cli CONFIG SET maxmemory 8gb
redis-cli CONFIG SET maxmemory-policy volatile-lru

# Enable compression
redis-cli CONFIG SET hash-max-ziplist-entries 512
redis-cli CONFIG SET hash-max-ziplist-value 64

# Use Redis streams for batch updates
redis-cli XADD spend:stream * key_id "KEY123" amount "10.50"
```

### Prometheus Performance Tuning

#### High Cardinality Issues

**Problem**: Too many unique label combinations

**Solution**:
```yaml
# Limit cardinality with relabeling
scrape_configs:
  - job_name: 'conduit'
    metric_relabel_configs:
      # Drop high cardinality metrics
      - source_labels: [__name__]
        regex: 'conduit_debug_.*'
        action: drop
      
      # Aggregate virtual key IDs
      - source_labels: [virtual_key_id]
        target_label: key_bucket
        regex: '(.{8}).*'
        replacement: '${1}...'
```

---

## Diagnostic Commands

### Quick Health Check

```bash
#!/bin/bash
# cost-health-check.sh

echo "=== Cost Observability Health Check ==="
echo

# Check services
for service in prometheus grafana alertmanager redis postgresql; do
  if systemctl is-active --quiet $service; then
    echo "✓ $service: Running"
  else
    echo "✗ $service: Not running"
  fi
done

echo

# Check metrics
METRICS_COUNT=$(curl -s http://localhost:8080/metrics | grep -c "conduit_cost")
echo "Metrics exposed: $METRICS_COUNT"

# Check Prometheus scraping
LAST_SCRAPE=$(curl -s http://prometheus:9090/api/v1/targets | jq -r '.data.activeTargets[] | select(.labels.job=="conduit") | .lastScrape')
echo "Last Prometheus scrape: $LAST_SCRAPE"

# Check cache hit ratio
HIT_RATIO=$(redis-cli INFO stats | awk -F: '/keyspace_hits/{hits=$2} /keyspace_misses/{misses=$2} END {print hits/(hits+misses)*100}')
echo "Cache hit ratio: ${HIT_RATIO}%"

# Check batch lag
BATCH_LAG=$(curl -s http://prometheus:9090/api/v1/query?query=conduit_batch_spend_update_lag_seconds | jq -r '.data.result[0].value[1]')
echo "Batch processing lag: ${BATCH_LAG}s"

echo
echo "=== Check Complete ==="
```

### Debug Data Collection

```bash
#!/bin/bash
# collect-debug-info.sh

OUTPUT_DIR="/tmp/cost-debug-$(date +%Y%m%d-%H%M%S)"
mkdir -p $OUTPUT_DIR

echo "Collecting debug information to $OUTPUT_DIR..."

# System info
date > $OUTPUT_DIR/timestamp.txt
uname -a > $OUTPUT_DIR/system.txt
df -h > $OUTPUT_DIR/disk.txt
free -h > $OUTPUT_DIR/memory.txt

# Service logs
for service in conduit-api batch-processor; do
  kubectl logs -l app=$service --tail=1000 > $OUTPUT_DIR/${service}.log
done

# Database info
psql -c "SELECT version();" > $OUTPUT_DIR/postgres_version.txt
psql -c "SELECT * FROM pg_stat_activity WHERE state != 'idle';" > $OUTPUT_DIR/postgres_activity.txt
psql -c "SELECT COUNT(*) FROM llm_usage_log WHERE created_at > NOW() - INTERVAL '1 hour';" > $OUTPUT_DIR/recent_usage.txt

# Redis info
redis-cli INFO > $OUTPUT_DIR/redis_info.txt
redis-cli --scan --pattern "model:cost:*" | head -20 > $OUTPUT_DIR/redis_keys_sample.txt

# Prometheus metrics
curl -s http://prometheus:9090/api/v1/query?query=up > $OUTPUT_DIR/prometheus_up.json
curl -s http://prometheus:9090/api/v1/alerts > $OUTPUT_DIR/prometheus_alerts.json

# Grafana dashboards
curl -s http://grafana:3000/api/dashboards/uid/cost-observability \
  -H "Authorization: Bearer $GRAFANA_API_KEY" > $OUTPUT_DIR/dashboard.json

# Create archive
tar -czf $OUTPUT_DIR.tar.gz $OUTPUT_DIR/
echo "Debug information collected: $OUTPUT_DIR.tar.gz"
```

---

## Emergency Procedures

### Complete Cost System Failure

If the entire cost tracking system fails:

1. **Enable Emergency Mode**
   ```bash
   # Set emergency flag to bypass cost checks
   redis-cli SET "emergency:cost:bypass" "true" EX 3600
   
   # Log all requests for later processing
   redis-cli SET "emergency:cost:logging" "true" EX 3600
   ```

2. **Capture Usage for Later Processing**
   ```bash
   # Enable request logging to file
   echo "request_logging: true" >> /etc/conduit/emergency.yaml
   echo "log_path: /var/log/conduit/emergency_usage.jsonl" >> /etc/conduit/emergency.yaml
   
   # Restart with emergency config
   kubectl set env deployment/conduit-api EMERGENCY_MODE=true
   ```

3. **Manual Recovery Process**
   ```python
   # Process logged requests after recovery
   def process_emergency_logs():
       with open('/var/log/conduit/emergency_usage.jsonl') as f:
           for line in f:
               request = json.loads(line)
               calculate_and_store_cost(request)
   ```

### Data Corruption Recovery

If cost data becomes corrupted:

1. **Identify Corruption**
   ```sql
   -- Find inconsistencies
   SELECT 
     vk.id,
     vk.total_spend,
     SUM(ul.total_cost) as calculated_spend,
     ABS(vk.total_spend - SUM(ul.total_cost)) as discrepancy
   FROM virtual_keys vk
   LEFT JOIN llm_usage_log ul ON vk.id = ul.virtual_key_id
   GROUP BY vk.id
   HAVING ABS(vk.total_spend - SUM(ul.total_cost)) > 1;
   ```

2. **Restore from Backup**
   ```bash
   # Stop services
   kubectl scale deployment conduit-api --replicas=0
   
   # Restore database
   pg_restore -h $DB_HOST -d conduit_restore /backups/cost_backup.dump
   
   # Verify and switch
   psql -c "ALTER DATABASE conduit RENAME TO conduit_corrupted;"
   psql -c "ALTER DATABASE conduit_restore RENAME TO conduit;"
   
   # Restart services
   kubectl scale deployment conduit-api --replicas=3
   ```

---

## Monitoring Scripts

### Continuous Monitoring Script

```bash
#!/bin/bash
# monitor-cost-system.sh

while true; do
  clear
  echo "=== Cost System Monitor - $(date) ==="
  echo
  
  # Burn rate
  BURN_RATE=$(curl -s http://prometheus:9090/api/v1/query?query=sum\(conduit_cost_rate_dollars_per_minute\) | jq -r '.data.result[0].value[1]')
  echo "Current burn rate: \$${BURN_RATE}/min"
  
  # Top spending keys
  echo -e "\nTop 5 spending keys:"
  curl -s http://prometheus:9090/api/v1/query?query=topk\(5,conduit_virtualkey_spend_total\) | \
    jq -r '.data.result[] | "\(.metric.virtual_key_id): $\(.value[1])"'
  
  # Cache performance
  HIT_RATIO=$(redis-cli INFO stats | awk -F: '/keyspace_hits/{hits=$2} /keyspace_misses/{misses=$2} END {printf "%.1f", hits/(hits+misses)*100}')
  echo -e "\nCache hit ratio: ${HIT_RATIO}%"
  
  # Batch processing
  LAG=$(curl -s http://prometheus:9090/api/v1/query?query=conduit_batch_spend_update_lag_seconds | jq -r '.data.result[0].value[1]')
  echo "Batch lag: ${LAG}s"
  
  # Active alerts
  echo -e "\nActive alerts:"
  curl -s http://alertmanager:9093/api/v1/alerts | \
    jq -r '.[] | select(.status.state=="active") | .labels.alertname' | \
    sed 's/^/  - /'
  
  sleep 10
done
```

---

## Additional Resources

### Documentation Links

- [Cost Architecture](/docs/operations/cost-observability-architecture.md)
- [Metrics Catalog](/docs/operations/cost-metrics-catalog.md)
- [Alert Runbooks](/docs/runbooks/cost-observability-alerts.md)
- [Operational Procedures](/docs/operations/cost-observability-procedures.md)

### Support Contacts

- **Slack Channel**: #cost-observability
- **Email**: platform-support@conduit.ai
- **On-Call**: PagerDuty "conduit-platform"

### Useful Tools

- **Prometheus Query Tester**: http://prometheus:9090/graph
- **Grafana Explorer**: http://grafana:3000/explore
- **AlertManager UI**: http://alertmanager:9093
- **Redis Commander**: http://redis-commander:8081