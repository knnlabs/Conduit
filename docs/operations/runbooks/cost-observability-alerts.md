# Cost Observability Alert Runbooks

## Overview

This document provides step-by-step runbooks for responding to cost-related alerts in the Conduit platform. Each runbook includes severity assessment, immediate actions, investigation steps, and resolution procedures.

---

## Alert: High Cost Burn Rate

**Alert Name**: `HighCostBurnRate`  
**Severity**: Critical  
**Threshold**: Cost rate > $100/minute for 5 minutes  

### Impact Assessment

- **Business Impact**: Direct financial loss, potential budget overrun
- **Customer Impact**: May trigger rate limiting or service degradation
- **Urgency**: Immediate action required

### Immediate Actions (First 5 Minutes)

1. **Acknowledge Alert**
   ```bash
   curl -X POST http://alertmanager:9093/api/v1/alerts/acknowledge \
     -d '{"alertname": "HighCostBurnRate", "acknowledged_by": "$USER"}'
   ```

2. **Check Current Burn Rate**
   ```promql
   sum(conduit_cost_rate_dollars_per_minute)
   ```

3. **Identify Top Cost Contributors**
   ```promql
   topk(5, sum by (provider, model) (rate(conduit_cost_total_dollars[5m])))
   ```

4. **Page On-Call if Rate > $200/min**
   ```bash
   if [ $(curl -s http://prometheus:9090/api/v1/query?query=sum\(conduit_cost_rate_dollars_per_minute\) | jq '.data.result[0].value[1]' | cut -d'"' -f2) -gt 200 ]; then
     pagerduty-cli trigger --service conduit-platform --severity critical
   fi
   ```

### Investigation Steps

1. **Check for Anomalous Virtual Keys**
   ```promql
   # Keys with sudden spike in usage
   topk(10, rate(conduit_virtualkey_requests_total[5m]) / rate(conduit_virtualkey_requests_total[1h] offset 1h))
   
   # Keys exceeding normal patterns
   conduit_virtualkey_spend_total - conduit_virtualkey_spend_total offset 1h > 1000
   ```

2. **Analyze Model Usage Patterns**
   ```bash
   # Check for expensive model usage
   curl -s http://prometheus:9090/api/v1/query \
     -d 'query=topk(5, sum by (model) (rate(conduit_cost_total_dollars[5m])))' \
     | jq '.data.result[] | {model: .metric.model, cost_rate: .value[1]}'
   ```

3. **Check for Provider Issues**
   ```promql
   # Provider error rates
   sum by (provider) (rate(conduit_provider_errors_total[5m]))
   ```

4. **Review Recent Deployments**
   ```bash
   # Check deployment events
   kubectl get events --sort-by='.lastTimestamp' | grep -E "Deployment|ConfigMap" | head -10
   
   # Check recent config changes
   git log --oneline --since="1 hour ago" -- config/
   ```

### Mitigation Actions

1. **Implement Rate Limiting (if needed)**
   ```bash
   # Temporary rate limit on high-cost keys
   redis-cli SET "ratelimit:emergency:enabled" "true" EX 3600
   
   # Update rate limits
   kubectl patch configmap rate-limits --patch '{"data":{"high_cost_limit":"10"}}'
   ```

2. **Disable Expensive Models**
   ```sql
   -- Temporarily disable expensive models
   UPDATE model_provider_mapping 
   SET is_active = false 
   WHERE model_alias IN ('gpt-4-vision', 'claude-3-opus')
   AND provider_id IN (SELECT id FROM provider WHERE provider_type = 'openai');
   ```

3. **Switch to Cheaper Providers**
   ```bash
   # Update routing rules to prefer cheaper providers
   kubectl apply -f emergency-routing-rules.yaml
   ```

4. **Block Suspicious Keys**
   ```bash
   # Block specific virtual key
   redis-cli SADD "blocked_keys" "$VIRTUAL_KEY_ID"
   redis-cli EXPIRE "blocked_keys" 3600
   ```

### Resolution Verification

1. **Confirm Burn Rate Reduction**
   ```promql
   # Should show decreasing trend
   sum(conduit_cost_rate_dollars_per_minute)
   ```

2. **Monitor for 15 minutes**
   ```bash
   watch -n 30 'curl -s http://prometheus:9090/api/v1/query?query=sum\(conduit_cost_rate_dollars_per_minute\) | jq .data.result[0].value[1]'
   ```

3. **Generate Incident Report**
   ```bash
   ./scripts/generate-cost-incident-report.sh --start-time "$ALERT_TIME" --duration 30m
   ```

### Post-Incident Actions

1. Update rate limiting rules permanently if needed
2. Review and adjust budget alerts
3. Conduct root cause analysis
4. Update monitoring thresholds if false positive

---

## Alert: Virtual Key Budget Exceeded

**Alert Name**: `VirtualKeyBudgetExceeded`  
**Severity**: High  
**Threshold**: Budget utilization >= 100%  

### Impact Assessment

- **Business Impact**: Customer over budget, potential billing disputes
- **Customer Impact**: Service interruption for affected key
- **Urgency**: High (customer-facing)

### Immediate Actions

1. **Identify Affected Key**
   ```promql
   conduit_virtualkey_budget_utilization_percent >= 100
   ```

2. **Check Key Status**
   ```bash
   # Get key details
   curl -X GET "http://admin-api:5002/api/virtualkeys/$VIRTUAL_KEY_ID" \
     -H "Authorization: Bearer $ADMIN_TOKEN"
   ```

3. **Review Recent Usage**
   ```promql
   # Last hour usage
   increase(conduit_virtualkey_spend_total{virtual_key_id="$KEY"}[1h])
   
   # Request pattern
   rate(conduit_virtualkey_requests_total{virtual_key_id="$KEY"}[5m])
   ```

### Investigation Steps

1. **Check for Usage Anomalies**
   ```sql
   SELECT 
     date_trunc('hour', created_at) as hour,
     COUNT(*) as requests,
     SUM(total_cost) as cost
   FROM llm_usage_log
   WHERE virtual_key_id = '$KEY_ID'
     AND created_at > NOW() - INTERVAL '24 hours'
   GROUP BY hour
   ORDER BY hour DESC;
   ```

2. **Verify Budget Configuration**
   ```sql
   SELECT 
     vk.id,
     vk.name,
     vk.budget_amount,
     vk.budget_period,
     vk.total_spend,
     vk.current_period_spend
   FROM virtual_keys vk
   WHERE vk.id = '$KEY_ID';
   ```

3. **Check for Billing Cycle Issues**
   ```bash
   # Check if budget should have reset
   ./scripts/check-budget-reset.sh --key "$VIRTUAL_KEY_ID"
   ```

### Mitigation Actions

1. **Temporary Budget Extension (if authorized)**
   ```sql
   -- Grant 10% emergency extension
   UPDATE virtual_keys 
   SET budget_amount = budget_amount * 1.1,
       notes = CONCAT(notes, '\nEmergency extension granted: ', NOW())
   WHERE id = '$KEY_ID';
   ```

2. **Rate Limit the Key**
   ```bash
   # Reduce rate limit to minimum
   redis-cli HSET "key:ratelimit:$KEY_ID" "requests_per_minute" "1"
   redis-cli EXPIRE "key:ratelimit:$KEY_ID" 3600
   ```

3. **Notify Customer**
   ```bash
   # Send automated notification
   curl -X POST "http://notification-service/api/notify" \
     -H "Content-Type: application/json" \
     -d '{
       "type": "budget_exceeded",
       "virtual_key_id": "'$KEY_ID'",
       "current_spend": "'$CURRENT_SPEND'",
       "budget": "'$BUDGET'"
     }'
   ```

### Resolution Verification

1. **Confirm Key Status**
   ```promql
   conduit_virtualkey_budget_utilization_percent{virtual_key_id="$KEY"}
   ```

2. **Check Service Restoration**
   ```bash
   # Test key functionality
   curl -X POST "http://api:5000/v1/chat/completions" \
     -H "Authorization: Bearer $KEY" \
     -d '{"model": "gpt-3.5-turbo", "messages": [{"role": "user", "content": "test"}]}'
   ```

### Post-Incident Actions

1. Review budget alert thresholds (consider 80% warning)
2. Implement progressive rate limiting before hard stop
3. Create customer communication template
4. Consider implementing grace period policy

---

## Alert: Cost Anomaly Detected

**Alert Name**: `CostAnomalyDetected`  
**Severity**: Warning  
**Threshold**: Cost increase > 500% compared to same time yesterday  

### Impact Assessment

- **Business Impact**: Potential unexpected costs
- **Customer Impact**: None immediate
- **Urgency**: Medium

### Immediate Actions

1. **Compare Cost Patterns**
   ```promql
   # Current vs yesterday
   sum(rate(conduit_cost_total_dollars[1h])) / 
   sum(rate(conduit_cost_total_dollars[1h] offset 24h))
   ```

2. **Identify Changed Patterns**
   ```promql
   # Model usage changes
   topk(5, 
     sum by (model) (rate(conduit_model_requests_total[1h])) - 
     sum by (model) (rate(conduit_model_requests_total[1h] offset 24h))
   )
   ```

### Investigation Steps

1. **Check for New Virtual Keys**
   ```sql
   SELECT id, name, created_at, budget_amount
   FROM virtual_keys
   WHERE created_at > NOW() - INTERVAL '24 hours'
   ORDER BY created_at DESC;
   ```

2. **Review Model Changes**
   ```sql
   SELECT 
     mpm.model_alias,
     p.name as provider,
     mpm.updated_at
   FROM model_provider_mapping mpm
   JOIN provider p ON mpm.provider_id = p.id
   WHERE mpm.updated_at > NOW() - INTERVAL '24 hours';
   ```

3. **Check for Configuration Changes**
   ```bash
   # Audit log review
   grep -E "UPDATE|INSERT|DELETE" /var/log/conduit/audit.log | grep -v SELECT | tail -50
   ```

### Mitigation Actions

1. **Set Temporary Cost Cap**
   ```bash
   # Implement hourly cost cap
   redis-cli SET "cost:cap:hourly" "5000" EX 7200
   ```

2. **Enable Detailed Logging**
   ```bash
   # Increase logging verbosity
   kubectl set env deployment/conduit-api LOG_LEVEL=DEBUG
   ```

3. **Create Cost Snapshot**
   ```bash
   # Capture current state for analysis
   ./scripts/create-cost-snapshot.sh --output /tmp/cost_anomaly_$(date +%s).json
   ```

### Resolution Verification

1. **Monitor Cost Stabilization**
   ```promql
   # 30-minute moving average
   avg_over_time(sum(rate(conduit_cost_total_dollars[5m]))[30m:])
   ```

2. **Generate Anomaly Report**
   ```bash
   ./scripts/analyze-cost-anomaly.sh --start "$ANOMALY_START" --end "now"
   ```

---

## Alert: Provider Cost Spike

**Alert Name**: `ProviderCostSpike`  
**Severity**: Warning  
**Threshold**: Provider cost increase > 200% in 15 minutes  

### Impact Assessment

- **Business Impact**: Increased operational costs
- **Customer Impact**: Potential service degradation if provider limited
- **Urgency**: Medium

### Immediate Actions

1. **Identify Affected Provider**
   ```promql
   topk(1, 
     sum by (provider) (rate(conduit_cost_total_dollars[15m])) /
     sum by (provider) (rate(conduit_cost_total_dollars[15m] offset 15m))
   )
   ```

2. **Check Provider Errors**
   ```promql
   conduit_provider_errors_total{provider="$PROVIDER"}
   ```

### Investigation Steps

1. **Review Provider Configuration**
   ```sql
   SELECT 
     p.id,
     p.name,
     p.provider_type,
     p.is_active,
     COUNT(pkc.id) as active_keys
   FROM provider p
   LEFT JOIN provider_key_credential pkc ON p.id = pkc.provider_id
   WHERE p.name = '$PROVIDER'
   GROUP BY p.id;
   ```

2. **Check Rate Limiting**
   ```promql
   conduit_provider_rate_limit_remaining{provider="$PROVIDER"}
   ```

3. **Analyze Request Distribution**
   ```promql
   # Request distribution by model
   sum by (model) (rate(conduit_model_requests_total{provider="$PROVIDER"}[15m]))
   ```

### Mitigation Actions

1. **Redistribute Load**
   ```sql
   -- Reduce provider weight in load balancing
   UPDATE provider 
   SET routing_weight = routing_weight * 0.5
   WHERE name = '$PROVIDER';
   ```

2. **Enable Provider Fallback**
   ```bash
   # Configure fallback rules
   kubectl apply -f - <<EOF
   apiVersion: v1
   kind: ConfigMap
   metadata:
     name: provider-fallback
   data:
     enabled: "true"
     primary: "$PROVIDER"
     fallback: "$FALLBACK_PROVIDER"
   EOF
   ```

3. **Implement Provider-Specific Rate Limit**
   ```bash
   redis-cli SET "provider:ratelimit:$PROVIDER" "100" EX 3600
   ```

### Resolution Verification

1. **Monitor Cost Normalization**
   ```promql
   sum by (provider) (rate(conduit_cost_total_dollars[5m]))
   ```

2. **Check Service Quality**
   ```promql
   histogram_quantile(0.95, 
     rate(conduit_model_response_time_seconds_bucket{provider="$PROVIDER"}[5m])
   )
   ```

---

## Alert: Cache Performance Degradation

**Alert Name**: `CostCacheDegradation`  
**Severity**: Warning  
**Threshold**: Cache hit ratio < 80% for 10 minutes  

### Impact Assessment

- **Business Impact**: Increased database load, slower cost calculations
- **Customer Impact**: Potential latency increase
- **Urgency**: Low-Medium

### Immediate Actions

1. **Check Cache Metrics**
   ```promql
   # Hit ratio
   sum(rate(conduit_cost_cache_hits_total[5m])) /
   (sum(rate(conduit_cost_cache_hits_total[5m])) + sum(rate(conduit_cost_cache_misses_total[5m])))
   
   # Operation latency
   histogram_quantile(0.99, rate(conduit_cost_cache_operation_duration_seconds_bucket[5m]))
   ```

2. **Check Redis Health**
   ```bash
   redis-cli INFO stats | grep -E "instantaneous_ops_per_sec|used_memory_human|connected_clients"
   ```

### Investigation Steps

1. **Check for Cache Invalidation Storm**
   ```bash
   # Check invalidation rate
   redis-cli --scan --pattern "model:cost:*" | wc -l
   
   # Monitor cache operations
   redis-cli MONITOR | grep -E "DEL|FLUSH" | head -20
   ```

2. **Review Memory Usage**
   ```bash
   redis-cli INFO memory
   ```

3. **Check for Configuration Changes**
   ```bash
   # Recent cost configuration updates
   tail -100 /var/log/conduit/admin-api.log | grep -i "modelcost"
   ```

### Mitigation Actions

1. **Warm Cache**
   ```bash
   # Preload frequently used costs
   ./scripts/warm-cost-cache.sh --top-models 100
   ```

2. **Extend Cache TTL**
   ```bash
   # Temporarily increase TTL
   redis-cli CONFIG SET maxmemory-policy allkeys-lru
   export COST_CACHE_TTL=600  # 10 minutes instead of 5
   ```

3. **Scale Redis if Needed**
   ```bash
   # Add read replica
   kubectl scale statefulset redis-cache --replicas=3
   ```

### Resolution Verification

1. **Monitor Cache Recovery**
   ```bash
   watch -n 10 'redis-cli INFO stats | grep -E "keyspace_hits|keyspace_misses"'
   ```

2. **Verify Performance**
   ```promql
   histogram_quantile(0.95, rate(conduit_cost_calculation_duration_ms_bucket[5m]))
   ```

---

## Alert: Batch Processing Failure

**Alert Name**: `BatchSpendUpdateFailure`  
**Severity**: High  
**Threshold**: Failed batch updates > 5 in 5 minutes  

### Impact Assessment

- **Business Impact**: Inaccurate spend tracking, delayed budget enforcement
- **Customer Impact**: Budget limits may not be enforced correctly
- **Urgency**: High

### Immediate Actions

1. **Check Batch Processing Status**
   ```promql
   rate(conduit_batch_spend_update_failures_total[5m])
   ```

2. **Review Error Logs**
   ```bash
   tail -100 /var/log/conduit/batch-processor.log | grep ERROR
   ```

3. **Check Database Connectivity**
   ```bash
   psql -h $DB_HOST -U $DB_USER -d conduit -c "SELECT NOW();"
   ```

### Investigation Steps

1. **Check Batch Queue Size**
   ```promql
   conduit_batch_spend_update_size
   conduit_batch_spend_update_lag_seconds
   ```

2. **Review Database Locks**
   ```sql
   SELECT 
     pid, 
     usename, 
     application_name,
     state,
     query_start,
     state_change,
     query
   FROM pg_stat_activity
   WHERE state != 'idle'
     AND query_start < NOW() - INTERVAL '1 minute';
   ```

3. **Check for Data Integrity Issues**
   ```sql
   -- Check for orphaned records
   SELECT COUNT(*) 
   FROM spend_update_queue suq
   LEFT JOIN virtual_keys vk ON suq.virtual_key_id = vk.id
   WHERE vk.id IS NULL;
   ```

### Mitigation Actions

1. **Clear Stuck Batches**
   ```sql
   -- Move stuck items to dead letter queue
   INSERT INTO spend_update_dlq (virtual_key_id, amount, created_at, error)
   SELECT virtual_key_id, amount, created_at, 'Batch timeout'
   FROM spend_update_queue
   WHERE created_at < NOW() - INTERVAL '10 minutes';
   
   DELETE FROM spend_update_queue
   WHERE created_at < NOW() - INTERVAL '10 minutes';
   ```

2. **Process Manually**
   ```bash
   # Force immediate processing
   ./scripts/process-spend-batch.sh --force --timeout 60
   ```

3. **Restart Batch Processor**
   ```bash
   kubectl rollout restart deployment/batch-processor
   ```

### Resolution Verification

1. **Confirm Processing Resumed**
   ```promql
   rate(conduit_batch_spend_update_failures_total[1m]) == 0
   ```

2. **Verify Spend Accuracy**
   ```sql
   -- Compare calculated vs recorded spend
   SELECT 
     vk.id,
     vk.total_spend as recorded,
     SUM(ul.total_cost) as calculated,
     ABS(vk.total_spend - SUM(ul.total_cost)) as difference
   FROM virtual_keys vk
   JOIN llm_usage_log ul ON vk.id = ul.virtual_key_id
   WHERE ul.created_at > NOW() - INTERVAL '1 hour'
   GROUP BY vk.id
   HAVING ABS(vk.total_spend - SUM(ul.total_cost)) > 0.01;
   ```

---

## Escalation Matrix

| Alert | L1 Response Time | L2 Escalation | L3/Management |
|-------|-----------------|---------------|---------------|
| High Cost Burn Rate | 5 min | 15 min if > $200/min | 30 min if > $500/min |
| Budget Exceeded | 15 min | 1 hour if enterprise | 2 hours if unresolved |
| Cost Anomaly | 30 min | 2 hours | Next business day |
| Provider Spike | 30 min | 2 hours | 4 hours |
| Cache Degradation | 1 hour | 4 hours | Next business day |
| Batch Failure | 15 min | 30 min | 1 hour |

---

## Communication Templates

### Customer Communication - Budget Exceeded

```
Subject: Conduit API Budget Alert - Immediate Action Required

Dear [Customer Name],

Your Virtual Key [KEY_NAME] has reached 100% of its configured budget limit of $[BUDGET].

Current spend: $[SPEND]
Budget period: [PERIOD]

Your API requests are currently being rate-limited to prevent further charges. 

To restore full service:
1. Increase your budget limit at: [DASHBOARD_URL]
2. Contact support for temporary extension: [SUPPORT_EMAIL]

Best regards,
Conduit Platform Team
```

### Internal Escalation - High Burn Rate

```
Subject: CRITICAL: Cost Burn Rate Alert - $[RATE]/min

Team,

Automated cost controls have detected an abnormal burn rate:
- Current rate: $[RATE]/minute
- Normal rate: $[NORMAL]/minute
- Top contributor: [PROVIDER]/[MODEL]
- Affected keys: [COUNT]

Immediate investigation required. Runbook: [RUNBOOK_URL]

Alert details: [GRAFANA_URL]
```

---

## Automation Scripts

### Quick Diagnostics Script

```bash
#!/bin/bash
# cost-diagnostics.sh

echo "=== Cost Observability Quick Diagnostics ==="
echo "Time: $(date)"
echo

echo "Current Burn Rate:"
curl -s http://prometheus:9090/api/v1/query?query=sum\(conduit_cost_rate_dollars_per_minute\) | jq '.data.result[0].value[1]'

echo -e "\nTop 5 Spending Keys:"
curl -s http://prometheus:9090/api/v1/query?query=topk\(5,conduit_virtualkey_spend_total\) | jq '.data.result[].metric.virtual_key_id'

echo -e "\nProvider Errors:"
curl -s http://prometheus:9090/api/v1/query?query=conduit_provider_errors_total | jq '.data.result[] | {provider: .metric.provider, errors: .value[1]}'

echo -e "\nCache Performance:"
curl -s http://prometheus:9090/api/v1/query?query=sum\(rate\(conduit_cost_cache_hits_total[5m]\)\)/\(sum\(rate\(conduit_cost_cache_hits_total[5m]\)\)%2Bsum\(rate\(conduit_cost_cache_misses_total[5m]\)\)\) | jq '.data.result[0].value[1]'

echo -e "\nActive Alerts:"
curl -s http://alertmanager:9093/api/v1/alerts | jq '.data[] | select(.labels.alertname | contains("Cost")) | {name: .labels.alertname, state: .status.state}'
```

---

## Recovery Time Objectives

| Scenario | Detection | Response | Mitigation | Resolution |
|----------|-----------|----------|------------|------------|
| Cost Spike | < 1 min | < 5 min | < 15 min | < 30 min |
| Budget Exceeded | Immediate | < 5 min | < 10 min | < 20 min |
| Provider Issue | < 2 min | < 10 min | < 20 min | < 1 hour |
| Cache Failure | < 5 min | < 15 min | < 30 min | < 1 hour |
| Batch Processing | < 2 min | < 10 min | < 20 min | < 30 min |

---

## Lessons Learned Database

Document all incidents using this template:

```yaml
incident:
  id: INC-2024-001
  date: 2024-01-15
  alert: HighCostBurnRate
  duration: 25 minutes
  cost_impact: $2,847
  
root_cause: |
  Misconfigured routing rule directed all traffic to most expensive model
  
contributing_factors:
  - No validation on routing rule changes
  - Alert threshold too high ($100/min)
  - No automatic fallback mechanism
  
actions_taken:
  - Reverted routing configuration
  - Implemented rate limiting
  - Reduced alert threshold to $50/min
  
improvements:
  - Add routing rule validation
  - Implement automatic cost-based circuit breaker
  - Create staging environment for config testing
```

---

## Contact Information

**On-Call Rotation**: See PagerDuty schedule "conduit-platform"

**Escalation Contacts**:
- L1: platform-oncall@conduit.ai
- L2: platform-leads@conduit.ai  
- L3: platform-management@conduit.ai
- Executive: cto@conduit.ai (only for > $10K/hour impact)

**External Contacts**:
- OpenAI Support: [Account Manager Contact]
- Anthropic Support: [Account Manager Contact]
- AWS Support: [Premium Support Case]

**Documentation**:
- Full Runbooks: https://docs.conduit.ai/runbooks
- Architecture: https://docs.conduit.ai/architecture
- Metrics Reference: https://docs.conduit.ai/metrics