# Runbook: High Error Rate

## Alert Details
- **Alert Name**: HighErrorRate
- **Severity**: Critical
- **Condition**: Error rate > 5% for more than 5 minutes
- **Calculation**: `sum(rate(conduit_http_requests_total{status_code=~"5.."}[5m])) / sum(rate(conduit_http_requests_total[5m])) > 0.05`
- **Impact**: Users experiencing failures, potential data loss, degraded service

## Diagnosis Steps

### 1. Identify Error Patterns
```bash
# Check error distribution by endpoint
curl -s http://localhost/metrics | grep conduit_http_requests_total | grep -E 'status_code="5'

# View recent error logs
docker logs conduit-api --tail 1000 | grep -E "(ERROR|CRITICAL|Exception)"

# Group errors by type
docker logs conduit-api --tail 1000 | grep ERROR | awk '{print $4}' | sort | uniq -c | sort -nr
```

### 2. Check Specific Error Types
```bash
# 500 - Internal Server Errors
docker logs conduit-api --tail 1000 | grep "StatusCode=500"

# 502 - Bad Gateway (provider issues)
docker logs conduit-api --tail 1000 | grep "StatusCode=502"

# 503 - Service Unavailable
docker logs conduit-api --tail 1000 | grep "StatusCode=503"

# 504 - Gateway Timeout
docker logs conduit-api --tail 1000 | grep "StatusCode=504"
```

### 3. Check External Dependencies
```bash
# Database connectivity
docker exec conduit-api dotnet exec ConduitLLM.Http.dll --test-db

# Redis connectivity
redis-cli ping
redis-cli info stats | grep rejected_connections

# Provider health
curl -s http://localhost/metrics | grep conduit_provider_health
```

### 4. Resource Utilization
```bash
# Memory pressure
docker stats conduit-api --no-stream
free -h

# CPU usage
top -p $(pgrep -f ConduitLLM.Http)

# Disk I/O
iostat -x 1 5
```

### 5. Recent Changes
```bash
# Check deployment history
docker ps -a | grep conduit-api
docker inspect conduit-api | grep -E "(Created|Image)"

# Check configuration changes
git log --oneline -n 20 -- '*.json' '*.yml'
```

## Resolution Steps

### By Error Type

#### 1. Database Connection Errors (500)
```bash
# Check connection pool
psql -U conduit -c "SELECT count(*) FROM pg_stat_activity WHERE datname = 'conduit';"

# Restart if pool exhausted
docker restart conduit-api

# See: [Database Connection Pool Runbook](./db-connection-pool.md)
```

#### 2. Provider Errors (502/503)
```bash
# Check provider status
curl -s http://localhost/api/providerhealth

# Disable failing provider temporarily
curl -X POST http://localhost/api/providers/{id}/disable \
  -H "X-API-Key: $ADMIN_KEY"

# Switch to fallback providers
curl -X PUT http://localhost/api/modelmappings \
  -H "X-API-Key: $ADMIN_KEY" \
  -d '{"model": "gpt-4", "provider": "anthropic"}'
```

#### 3. Rate Limit Errors (429)
```bash
# Check rate limit metrics
curl -s http://localhost/metrics | grep conduit_rate_limit

# Increase rate limits temporarily
export CONDUITLLM__RATELIMITS__REQUESTSPERSECOND=100
docker restart conduit-api
```

#### 4. Memory/Resource Errors (500)
```bash
# Increase memory limits
docker update --memory="4g" --memory-swap="4g" conduit-api

# Force garbage collection
docker exec conduit-api kill -USR1 1

# Restart with increased limits
docker-compose down
docker-compose up -d
```

### Emergency Responses

1. **Circuit Breaker Activation**
   ```bash
   # Enable circuit breaker for failing endpoints
   export CONDUITLLM__CIRCUITBREAKER__ENABLED=true
   export CONDUITLLM__CIRCUITBREAKER__FAILURETHRESHOLD=10
   docker restart conduit-api
   ```

2. **Traffic Shedding**
   ```bash
   # Enable request throttling
   export CONDUITLLM__THROTTLING__ENABLED=true
   export CONDUITLLM__THROTTLING__MAXCONCURRENT=100
   docker restart conduit-api
   ```

3. **Fallback Mode**
   ```bash
   # Enable degraded mode (cache-only responses)
   export CONDUITLLM__FEATURES__DEGRADEDMODE=true
   docker restart conduit-api
   ```

## Root Cause Analysis

### Data Collection
```bash
# Capture detailed logs
docker logs conduit-api --since 30m > /tmp/error-investigation-$(date +%Y%m%d-%H%M%S).log

# Export metrics snapshot
curl -s http://localhost/metrics > /tmp/metrics-$(date +%Y%m%d-%H%M%S).txt

# Database query log
psql -U conduit -c "SELECT query, calls, mean_exec_time FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 20;" > /tmp/slow-queries.txt
```

### Common Root Causes

1. **Deployment Issues**
   - New code with bugs
   - Configuration changes
   - Missing migrations

2. **Resource Exhaustion**
   - Memory leaks
   - Connection pool exhaustion
   - Thread starvation

3. **External Dependencies**
   - Provider outages
   - Network issues
   - DNS problems

4. **Traffic Patterns**
   - Sudden spike in traffic
   - Abusive clients
   - DDoS attacks

## Prevention

1. **Automated Testing**
   ```yaml
   # Add to CI/CD pipeline
   - name: Load Test
     run: |
       k6 run --vus 100 --duration 5m loadtest.js
       if [ $(curl -s http://localhost/metrics | grep -oP 'error_rate \K[0-9.]+') > 0.01 ]; then
         exit 1
       fi
   ```

2. **Gradual Rollouts**
   ```bash
   # Use blue-green deployments
   docker-compose -f docker-compose.blue.yml up -d
   # Monitor for 10 minutes
   docker-compose -f docker-compose.green.yml down
   ```

3. **Error Budget Monitoring**
   ```promql
   # Track error budget consumption
   1 - (sum(rate(conduit_http_requests_total{status_code!~"5.."}[7d])) / sum(rate(conduit_http_requests_total[7d])))
   ```

## Escalation

If error rate remains above 5% after 30 minutes:

1. **Initiate incident response**
2. **Roll back recent changes**
3. **Enable maintenance mode**
4. **Notify customers via status page**

## Related Runbooks
- [API Down](./api-down.md)
- [High Response Time](./high-response-time.md)
- [Provider Health](./provider-health.md)