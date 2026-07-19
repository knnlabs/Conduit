# Provider Error Tracking Runbook

This runbook provides operational procedures for managing the provider error tracking system.

## Table of Contents

1. [Quick Reference](#quick-reference)
2. [Investigating Error Spikes](#investigating-error-spikes)
3. [Re-enabling Keys After Outage](#re-enabling-keys-after-outage)
4. [Redis Memory Management](#redis-memory-management)
5. [Monitoring and Alerting](#monitoring-and-alerting)
6. [Common Issues](#common-issues)
7. [Emergency Procedures](#emergency-procedures)

---

## Quick Reference

### Key API Endpoints

```bash
# Error statistics (last 24 hours)
curl http://localhost:5002/api/provider-errors/stats?hours=24

# Recent errors
curl http://localhost:5002/api/provider-errors/recent?limit=50

# Provider summaries
curl http://localhost:5002/api/provider-errors/summary

# Specific key details
curl http://localhost:5002/api/provider-errors/keys/{keyId}

# Clear errors and re-enable key
curl -X POST http://localhost:5002/api/provider-errors/keys/{keyId}/clear \
  -H "Content-Type: application/json" \
  -d '{"reenableKey": true, "confirmReenable": true, "reason": "Issue resolved"}'
```

### Redis Key Patterns

```bash
# Fatal errors for a key
redis-cli HGETALL "provider:errors:key:{keyId}:fatal"

# Warnings for a key
redis-cli ZRANGE "provider:errors:key:{keyId}:warnings" 0 -1

# Provider summary
redis-cli HGETALL "provider:errors:provider:{providerId}:summary"

# Recent errors feed
redis-cli ZRANGE "provider:errors:recent" -100 -1
```

---

## Investigating Error Spikes

### Step 1: Identify the Scope

```bash
# Get overall statistics
curl http://localhost:5002/api/provider-errors/stats?hours=1

# Sample response:
# {
#   "totalErrors": 150,
#   "fatalErrors": 45,
#   "warnings": 105,
#   "disabledKeys": 3,
#   "errorsByType": {
#     "RateLimitExceeded": 100,
#     "InvalidApiKey": 45,
#     "ServiceUnavailable": 5
#   },
#   "errorsByProvider": {
#     "OpenAI Production": 120,
#     "Anthropic": 30
#   }
# }
```

### Step 2: Drill Down by Provider

```bash
# Get errors for specific provider
curl "http://localhost:5002/api/provider-errors/recent?providerId=1&limit=50"
```

### Step 3: Check Provider Status

External status pages:
- **OpenAI**: https://status.openai.com
- **Anthropic**: https://status.anthropic.com
- **Azure OpenAI**: https://status.azure.com

### Step 4: Analyze Error Patterns

Questions to answer:
- Are errors concentrated on specific keys or spread across all?
- Is one provider affected or multiple?
- Are errors transient (503) or permanent (401, 402)?
- Did something change recently? (deployment, config change)

### Step 5: Take Action

| Pattern | Likely Cause | Action |
|---------|--------------|--------|
| All keys same error | Provider outage | Wait, monitor status page |
| Single key InvalidApiKey | Key revoked/expired | Replace API key |
| Multiple 402 errors | Account billing issue | Check billing, add credits |
| High 429 rate | Traffic spike | Add keys, implement rate limiting |

---

## Re-enabling Keys After Outage

### Single Key Re-enable

```bash
# 1. Verify the issue is resolved (test key directly)
curl https://api.openai.com/v1/models \
  -H "Authorization: Bearer $API_KEY"

# 2. Clear errors and re-enable
curl -X POST http://localhost:5002/api/provider-errors/keys/{keyId}/clear \
  -H "Content-Type: application/json" \
  -d '{
    "reenableKey": true,
    "confirmReenable": true,
    "reason": "Provider outage resolved"
  }'
```

### Bulk Re-enable After Provider Outage

When a provider outage has disabled multiple keys:

```bash
#!/bin/bash
# bulk-reenable.sh

ADMIN_API="http://localhost:5002"
PROVIDER_ID=$1
REASON="Provider outage resolved - $(date)"

# Get all disabled keys for provider
DISABLED_KEYS=$(curl -s "$ADMIN_API/api/provider-errors/summary" | \
  jq -r ".[] | select(.providerId == $PROVIDER_ID) | .disabledKeyIds[]")

# Re-enable each key
for KEY_ID in $DISABLED_KEYS; do
  echo "Re-enabling key $KEY_ID..."
  curl -X POST "$ADMIN_API/api/provider-errors/keys/$KEY_ID/clear" \
    -H "Content-Type: application/json" \
    -d "{\"reenableKey\": true, \"confirmReenable\": true, \"reason\": \"$REASON\"}"
  echo ""
done

echo "Done. Re-enabled $(echo $DISABLED_KEYS | wc -w) keys."
```

Usage:
```bash
chmod +x bulk-reenable.sh
./bulk-reenable.sh 1  # Provider ID 1
```

### Verify Re-enablement

```bash
# Check provider summary
curl http://localhost:5002/api/provider-errors/summary

# Verify key is enabled in database
# (via Admin API or direct database query)
```

---

## Redis Memory Management

### Check Memory Usage

```bash
# Overall Redis memory
redis-cli INFO memory | grep used_memory_human

# Count error tracking keys
redis-cli KEYS "provider:errors:*" | wc -l

# Size of recent errors feed
redis-cli ZCARD "provider:errors:recent"
```

### Automatic Cleanup

The system automatically:
- Trims warnings to last 100 per key
- Trims recent feed to last 1,000 entries
- Applies 30-day TTL to warning entries

### Manual Cleanup (if needed)

```bash
# Clear all warnings for a specific key
redis-cli DEL "provider:errors:key:{keyId}:warnings"

# Clear provider summary
redis-cli DEL "provider:errors:provider:{providerId}:summary"

# Clear recent feed (caution: loses history)
redis-cli DEL "provider:errors:recent"

# Clear ALL error tracking data (emergency only)
redis-cli KEYS "provider:errors:*" | xargs redis-cli DEL
```

### Memory Optimization

If memory is a concern:

1. Reduce warning retention (modify `RedisErrorStore`):
```csharp
// Change from 30 days
private static readonly TimeSpan WarningTtl = TimeSpan.FromDays(7);
```

2. Reduce recent feed size:
```csharp
// Change from 1000
private const int MaxRecentErrors = 500;
```

---

## Monitoring and Alerting

### Key Metrics to Monitor

| Metric | Source | Alert Threshold |
|--------|--------|-----------------|
| Fatal errors per hour | `/stats?hours=1` | > 10 |
| Disabled keys count | `/stats` `.disabledKeys` | > 0 |
| Rate limit warnings | `/stats` `.errorsByType.RateLimitExceeded` | > 50/hour |
| Provider error rate | `/stats` `.errorsByProvider` | Any > 20/hour |

### Prometheus Metrics (if configured)

```promql
# Error rate by type
rate(conduit_provider_errors_total[5m])

# Disabled keys gauge
conduit_disabled_provider_keys

# Error rate by provider
rate(conduit_provider_errors_total{provider="OpenAI"}[5m])
```

### Alert Rules Example

```yaml
groups:
- name: conduit-provider-errors
  rules:
  - alert: HighProviderErrorRate
    expr: rate(conduit_provider_errors_total[5m]) > 0.1
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "High provider error rate"

  - alert: ProviderKeysDisabled
    expr: conduit_disabled_provider_keys > 0
    for: 1m
    labels:
      severity: critical
    annotations:
      summary: "Provider keys have been disabled"
```

### Log Queries

Search logs for error tracking activity:

```bash
# Docker logs
docker logs conduit-gateway 2>&1 | grep -i "error tracking\|key disabled\|provider error"

# If using structured logging
docker logs conduit-gateway 2>&1 | jq 'select(.message | contains("ProviderError"))'
```

---

## Common Issues

### Issue: Keys Keep Getting Disabled

**Symptoms**: Keys repeatedly disabled shortly after re-enabling

**Diagnosis**:
```bash
# Check what error is causing disables
curl http://localhost:5002/api/provider-errors/keys/{keyId}
```

**Solutions**:
1. **InvalidApiKey**: Key is actually invalid - regenerate
2. **InsufficientBalance**: Account has no credits - add funds
3. **AccessForbidden**: API access revoked - check provider dashboard

### Issue: Errors Not Being Tracked

**Symptoms**: Requests fail but no errors appear in dashboard

**Diagnosis**:
```bash
# Check Redis connection
redis-cli PING

# Check error tracking service logs
docker logs conduit-gateway 2>&1 | grep -i "error tracking"

# Verify Redis keys exist
redis-cli KEYS "provider:errors:*"
```

**Solutions**:
1. Verify Redis is connected and healthy
2. Check `IProviderErrorTrackingService` is registered in DI
3. Ensure `ContextAwareLLMClient` decorator is applied
4. Check for exceptions in error tracking code

### Issue: High Redis Memory from Errors

**Symptoms**: Redis memory growing, error keys accumulating

**Diagnosis**:
```bash
# Count error keys
redis-cli KEYS "provider:errors:*" | wc -l

# Check largest keys
redis-cli --bigkeys
```

**Solutions**:
1. Clear old warnings: `redis-cli KEYS "provider:errors:key:*:warnings" | xargs redis-cli DEL`
2. Reduce TTLs in configuration
3. Scale Redis memory

### Issue: False Positive Disables

**Symptoms**: Valid keys disabled due to transient errors

**Diagnosis**:
- Check if errors clustered in short time
- Verify if provider had brief outage
- Review error messages for patterns

**Solutions**:
1. Increase threshold in `FatalErrorPolicies`
2. Extend time window for error counting
3. Re-enable affected keys

---

## Emergency Procedures

### All Providers Down

If all provider keys are disabled:

```bash
# 1. Get status
curl http://localhost:5002/api/provider-errors/summary

# 2. Identify disabled keys
# Look for "disabledKeyIds" in each provider

# 3. Mass re-enable (use with caution)
for PROVIDER_ID in 1 2 3; do
  curl -s "http://localhost:5002/api/provider-errors/summary" | \
    jq -r ".[] | select(.providerId == $PROVIDER_ID) | .disabledKeyIds[]" | \
    while read KEY_ID; do
      curl -X POST "http://localhost:5002/api/provider-errors/keys/$KEY_ID/clear" \
        -H "Content-Type: application/json" \
        -d '{"reenableKey":true,"confirmReenable":true,"reason":"Emergency re-enable"}'
    done
done
```

### Redis Failure

If Redis is unavailable:

1. Error tracking will fail gracefully (requests continue)
2. Key disabling won't work automatically
3. Fix Redis connection
4. Once restored, error tracking resumes

```bash
# Verify Redis connection
docker logs conduit-redis

# Restart Redis if needed
docker restart conduit-redis
```

### Disable Error Tracking Temporarily

If error tracking is causing issues:

```bash
# Option 1: Set Redis to null (config change, requires restart)
# REDIS_CONNECTION_STRING=""

# Option 2: Feature flag (if implemented)
# ERROR_TRACKING_ENABLED=false

# Option 3: Stop tracking specific provider
# Add to code: if (providerId == X) return;
```

### Data Recovery

Error data is ephemeral and designed to be disposable. If lost:

1. Manually check all provider keys in provider dashboards
2. Test each key with a simple API call
3. Re-enable valid keys
4. Replace invalid keys

---

## Maintenance Tasks

### Daily

- [ ] Check dashboard for disabled keys
- [ ] Review error statistics
- [ ] Verify no sustained high error rates

### Weekly

- [ ] Review warning trends
- [ ] Check Redis memory usage
- [ ] Verify all providers have at least one active key

### Monthly

- [ ] Audit disabled key history
- [ ] Review alert thresholds
- [ ] Test re-enable procedures
- [ ] Clean up old error data if needed

---

## Related Documentation

- [Administrator Guide](../api-guides/admin/provider-error-tracking.md)
- [Error Tracking Architecture](../architecture/provider-system/error-tracking.md)
- [Redis Resilience](./infrastructure/redis-resilience.md)
