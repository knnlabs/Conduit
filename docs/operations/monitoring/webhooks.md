# Webhook Delivery System Operations Guide

## Overview

This guide provides operational procedures for monitoring, troubleshooting, and managing the distributed webhook delivery system in production environments.

## Monitoring Webhook Health

### Key Performance Indicators (KPIs)

#### System-Wide Metrics
- **Overall Success Rate**: Target >95%
- **Average Response Time**: Target <2 seconds  
- **Pending Deliveries**: Target <100
- **Circuit Breaker Opens**: Target <5 per hour
- **Redis Connectivity**: Target 100% uptime

#### Per-URL Metrics  
- **Individual Success Rate**: Target >90%
- **P95 Response Time**: Target <5 seconds
- **P99 Response Time**: Target <10 seconds
- **Failure Count**: Alert if >10 failures/hour
- **Circuit State**: Monitor for persistent "Open" states

### Redis Monitoring

#### Essential Redis Keys to Monitor
```bash
# Check circuit breaker states
KEYS webhook:circuit:*:state

# Monitor metrics keys
KEYS webhook:metrics:urls:*

# Check recent events
ZCARD webhook:events:recent

# Monitor memory usage
INFO memory
```

#### Redis Health Checks
```bash
# Test Redis connectivity
redis-cli ping

# Check key expiration
TTL webhook:circuit:ABC123DEF456:state

# Monitor memory usage by key type  
redis-cli --bigkeys

# Check slow queries
redis-cli SLOWLOG GET 10
```

### Application Logs

#### Critical Log Messages to Monitor

**Circuit Breaker Events**
```
[Warning] Circuit breaker opened for webhook: {WebhookUrl} after {FailureCount} failures
[Information] Circuit breaker closed for webhook: {WebhookUrl} after successful delivery
[Information] Circuit breaker transitioned to half-open for webhook: {WebhookUrl}
```

**Delivery Failures**
```
[Warning] Webhook delivery failed to {WebhookUrl} with status {StatusCode}. Error: {ErrorMessage}
[Error] Max retries exceeded for webhook delivery to {WebhookUrl}
[Warning] Circuit breaker is open for webhook URL {WebhookUrl}. Skipping delivery
```

**System Health**
```
[Error] Error checking circuit state for webhook: {WebhookUrl}
[Error] Error recording success for webhook: {WebhookUrl}  
[Information] Falling back to in-memory webhook circuit breaker - Redis unavailable
```

## Troubleshooting Common Issues

### Webhooks Not Being Delivered

#### Diagnostic Steps
1. **Check Circuit Breaker State**
   ```bash
   # Get circuit state for specific URL hash
   redis-cli GET "webhook:circuit:{urlHash}:state"
   
   # Check failure count
   redis-cli GET "webhook:circuit:{urlHash}:failures"
   ```

2. **Verify MassTransit Queue**
   ```bash
   # Check application logs for consumer activity
   grep "WebhookDeliveryConsumer" logs/app.log
   
   # Look for queue processing delays
   grep "Processing webhook delivery request" logs/app.log
   ```

3. **Test Webhook Endpoint**
   ```bash
   curl -X POST https://customer.webhook.url \
        -H "Content-Type: application/json" \
        -d '{"test": "connectivity"}'
   ```

#### Resolution Actions
- **If Circuit Open**: Wait for auto-recovery (5 minutes) or manually reset
- **If Queue Blocked**: Check MassTransit configuration and RabbitMQ health
- **If Endpoint Down**: Contact customer or mark as temporarily disabled

### High Failure Rates

#### Investigation Process
1. **Identify Failing URLs**
   ```bash
   # Get metrics for specific webhook URL hash
   redis-cli HGETALL "webhook:metrics:urls:{urlHash}"
   ```

2. **Analyze Response Codes**
   ```bash
   # Search logs for HTTP status codes
   grep -E "status [45][0-9][0-9]" logs/webhook.log
   ```

3. **Check Response Times**
   ```bash
   # Get P95/P99 response times
   redis-cli ZRANGE "webhook:metrics:response:{urlHash}" -10 -1 WITHSCORES
   ```

#### Resolution Strategies
- **4xx Errors**: Customer configuration issue - notify customer
- **5xx Errors**: Customer service issue - circuit breaker will handle
- **Timeouts**: Adjust timeout or ask customer to optimize endpoint
- **Network Issues**: Check connectivity and DNS resolution

### Circuit Breaker Stuck Open

#### Diagnostic Commands
```bash
# Check current circuit state
redis-cli GET "webhook:circuit:{urlHash}:state"

# Check when circuit was opened
redis-cli GET "webhook:circuit:{urlHash}:opened"

# View failure history
redis-cli GET "webhook:circuit:{urlHash}:failures"
```

#### Manual Circuit Reset
```bash
# Delete circuit state (forces closed state)
redis-cli DEL "webhook:circuit:{urlHash}:state"
redis-cli DEL "webhook:circuit:{urlHash}:failures"
redis-cli DEL "webhook:circuit:{urlHash}:opened"

# Verify reset
redis-cli GET "webhook:circuit:{urlHash}:state"  # Should return null
```

⚠️ **Warning**: Manual resets bypass safety mechanisms. Only use after confirming webhook endpoint is healthy.

### Redis Connection Issues

#### Detection
```bash
# Check Redis connectivity from application servers
redis-cli -h redis-host -p 6379 ping

# Monitor connection count
redis-cli INFO clients

# Check for connection errors in logs
grep -i "redis.*error\|redis.*timeout" logs/app.log
```

#### Fallback Verification
```bash
# Confirm fallback to in-memory mode
grep "Falling back to in-memory webhook circuit breaker" logs/app.log

# Check for reduced functionality warnings
grep "Redis.*unavailable.*webhook" logs/app.log
```

#### Recovery Actions
1. **Restore Redis Service**: Fix underlying Redis issues
2. **Verify Reconnection**: Check logs for successful Redis operations
3. **Monitor State Sync**: Circuit states will rebuild as webhooks are processed

### Memory Issues

#### Redis Memory Monitoring
```bash
# Check total memory usage
redis-cli INFO memory

# Find largest keys
redis-cli --bigkeys

# Check for memory leaks in webhook keys
redis-cli EVAL "return #redis.call('keys', 'webhook:*')" 0
```

#### Memory Cleanup
```bash
# Manual cleanup of expired webhook data (emergency only)
redis-cli EVAL "
local keys = redis.call('keys', 'webhook:metrics:*')
for i=1,#keys do
    local ttl = redis.call('ttl', keys[i])
    if ttl == -1 then
        redis.call('expire', keys[i], 86400)
    end
end
return #keys
" 0
```

## Performance Tuning

### Redis Optimization

#### Configuration Recommendations
```ini
# redis.conf optimizations for webhook workload
maxmemory 2gb
maxmemory-policy allkeys-lru
timeout 60
tcp-keepalive 300

# Persistence (adjust based on requirements)
save 300 10000
save 60 100000

# Performance
hash-max-ziplist-entries 512
hash-max-ziplist-value 64
```

#### Connection Pool Tuning
```csharp
// StackExchange.Redis configuration
var config = ConfigurationOptions.Parse(connectionString);
config.ConnectRetry = 3;
config.ConnectTimeout = 5000;
config.SyncTimeout = 5000;
config.AsyncTimeout = 5000;
config.AbortOnConnectFail = false;
```

### Application Performance

#### HTTP Client Configuration
```csharp
// Optimal settings for webhook delivery
services.AddHttpClient("webhook")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        MaxConnectionsPerServer = 50,
        PooledConnectionLifetime = TimeSpan.FromMinutes(15)
    });
```

#### MassTransit Tuning
```csharp
// Consumer concurrency for webhook processing
cfg.ReceiveEndpoint("webhook-delivery", e =>
{
    e.PrefetchCount = 25;
    e.ConcurrentMessageLimit = 50;
});
```

## Maintenance Procedures

### Daily Operations

#### Health Check Script
```bash
#!/bin/bash
# Daily webhook system health check

echo "=== Webhook System Health Report $(date) ==="

# Check Redis connectivity
redis-cli ping || echo "❌ Redis connection failed"

# Check circuit breaker states
OPEN_CIRCUITS=$(redis-cli KEYS "webhook:circuit:*:state" | wc -l)
echo "📊 Open circuits: $OPEN_CIRCUITS"

# Check recent failures
RECENT_FAILURES=$(redis-cli ZCOUNT "webhook:events:recent" $(date -d '1 hour ago' +%s)000 +inf)
echo "📊 Recent failures (1h): $RECENT_FAILURES"

# Application health
curl -s http://localhost:5000/health/ready || echo "❌ Application not ready"

echo "=== End Health Report ==="
```

#### Metrics Collection
```bash
#!/bin/bash
# Collect webhook metrics for daily report

# Total metrics
TOTAL_URLS=$(redis-cli KEYS "webhook:metrics:urls:*" | wc -l)
TOTAL_EVENTS=$(redis-cli ZCARD "webhook:events:recent")

# Success rates
echo "Active webhook URLs: $TOTAL_URLS"
echo "Recent events: $TOTAL_EVENTS"

# Top failing URLs
redis-cli KEYS "webhook:circuit:*:failures" | head -10
```

### Weekly Maintenance

#### Data Cleanup
```bash
#!/bin/bash
# Weekly cleanup of webhook data

# Remove expired circuit states
redis-cli EVAL "
local keys = redis.call('keys', 'webhook:circuit:*')
local cleaned = 0
for i=1,#keys do
    local ttl = redis.call('ttl', keys[i])
    if ttl == -2 then
        redis.call('del', keys[i])
        cleaned = cleaned + 1
    end
end
return cleaned
" 0

# Trim old events
redis-cli ZREMRANGEBYRANK "webhook:events:recent" 0 -1001
```

#### Performance Analysis
```bash
#!/bin/bash
# Weekly performance report

echo "=== Weekly Webhook Performance Report ==="

# Average response times by URL
for key in $(redis-cli KEYS "webhook:metrics:urls:*" | head -20); do
    URL=$(redis-cli HGET "$key" "url")
    TOTAL_TIME=$(redis-cli HGET "$key" "total_response_time")
    COUNT=$(redis-cli HGET "$key" "response_count")
    
    if [ "$COUNT" -gt 0 ]; then
        AVG=$(echo "$TOTAL_TIME / $COUNT" | bc)
        echo "$URL: ${AVG}ms average"
    fi
done

echo "=== End Weekly Report ==="
```

## Alerting Rules

### Critical Alerts (Immediate Response)

```yaml
# Webhook system critical alerts
- name: WebhookSystemCritical
  rules:
  - alert: WebhookSuccessRateBelow90
    expr: webhook_success_rate < 0.90
    for: 5m
    labels:
      severity: critical
    annotations:
      summary: "Webhook success rate dropped below 90%"
      
  - alert: RedisConnectionLost  
    expr: redis_connected == 0
    for: 1m
    labels:
      severity: critical
    annotations:
      summary: "Lost connection to Redis - webhook system degraded"
      
  - alert: WebhookQueueBacklog
    expr: webhook_pending_deliveries > 1000
    for: 5m  
    labels:
      severity: critical
    annotations:
      summary: "Large backlog of pending webhook deliveries"
```

### Warning Alerts (Monitor Closely)

```yaml
- name: WebhookSystemWarning
  rules:
  - alert: HighCircuitBreakerActivity
    expr: webhook_circuit_opens_per_hour > 10
    for: 15m
    labels:
      severity: warning
    annotations:
      summary: "High number of circuit breaker activations"
      
  - alert: SlowWebhookResponse
    expr: webhook_p95_response_time > 5000
    for: 10m
    labels:
      severity: warning  
    annotations:
      summary: "Webhook response times are high"
```

## Emergency Procedures

### Complete Webhook System Failure

1. **Immediate Assessment**
   ```bash
   # Check all system components
   curl http://localhost:5000/health
   redis-cli ping
   ps aux | grep -E "(conduit|dotnet)"
   ```

2. **Failover to Basic Mode**
   ```bash
   # Disable Redis to force in-memory fallback
   # Edit configuration to remove Redis connection
   systemctl restart conduit-api
   ```

3. **Customer Communication**
   - Notify customers of delayed webhook deliveries
   - Provide estimated recovery time
   - Offer alternative monitoring methods

### Data Recovery

#### Webhook Delivery History
```bash
# Export recent webhook events for analysis
redis-cli ZRANGE "webhook:events:recent" 0 -1 WITHSCORES > webhook_events_backup.txt

# Export circuit breaker states
redis-cli KEYS "webhook:circuit:*:state" | xargs -I {} redis-cli GET {} > circuit_states_backup.txt
```

#### State Restoration
```bash
# Restore webhook metrics (if needed)
cat webhook_metrics_backup.txt | redis-cli --pipe

# Verify restoration
redis-cli KEYS "webhook:*" | wc -l
```

## Contact Information

### Escalation Path
1. **Level 1**: Operations team - webhook monitoring alerts
2. **Level 2**: Development team - circuit breaker issues  
3. **Level 3**: Infrastructure team - Redis/network issues
4. **Level 4**: Customer Success - customer communication

### Key Commands Reference
```bash
# Quick system status
redis-cli ping && curl -s http://localhost:5000/health/ready

# Circuit breaker summary  
redis-cli KEYS "webhook:circuit:*:state" | wc -l

# Recent activity
redis-cli ZCARD "webhook:events:recent"

# Emergency circuit reset
redis-cli DEL "webhook:circuit:{urlHash}:state"
```