# Runbook: High Response Time

## Alert Details
- **Alert Name**: HighResponseTime
- **Severity**: Warning
- **Condition**: 95th percentile response time > 1 second for more than 10 minutes
- **Calculation**: `histogram_quantile(0.95, sum(rate(conduit_http_request_duration_seconds_bucket[5m])) by (le)) > 1`
- **Impact**: Poor user experience, potential timeouts, SLA violations

## Diagnosis Steps

### 1. Identify Slow Endpoints
```bash
# Check response times by endpoint
curl -s http://localhost/metrics | grep conduit_http_request_duration_seconds | grep -v "#" | sort -k2 -nr

# View slow request logs
docker logs conduit-api --tail 1000 | grep "Slow request detected"

# Check specific endpoint performance
curl -w "@curl-format.txt" -o /dev/null -s http://localhost/v1/chat/completions
```

Create `curl-format.txt`:
```
time_namelookup:  %{time_namelookup}s\n
time_connect:  %{time_connect}s\n
time_appconnect:  %{time_appconnect}s\n
time_pretransfer:  %{time_pretransfer}s\n
time_redirect:  %{time_redirect}s\n
time_starttransfer:  %{time_starttransfer}s\n
time_total:  %{time_total}s\n
```

### 2. Database Performance
```bash
# Check slow queries
psql -U conduit -c "SELECT query, calls, mean_exec_time, max_exec_time FROM pg_stat_statements WHERE mean_exec_time > 100 ORDER BY mean_exec_time DESC LIMIT 20;"

# Active queries
psql -U conduit -c "SELECT pid, now() - pg_stat_activity.query_start AS duration, query FROM pg_stat_activity WHERE (now() - pg_stat_activity.query_start) > interval '5 seconds';"

# Index usage
psql -U conduit -c "SELECT schemaname, tablename, indexname, idx_scan, idx_tup_read, idx_tup_fetch FROM pg_stat_user_indexes ORDER BY idx_scan;"

# Table bloat
psql -U conduit -c "SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size, n_live_tup, n_dead_tup, n_dead_tup::float / NULLIF(n_live_tup + n_dead_tup, 0) AS dead_ratio FROM pg_stat_user_tables WHERE n_dead_tup > 1000 ORDER BY dead_ratio DESC;"
```

### 3. Cache Performance
```bash
# Redis latency
redis-cli --latency
redis-cli --latency-history

# Cache hit rate
curl -s http://localhost/metrics | grep -E "conduit_redis_cache_(hits|misses)"

# Redis slow log
redis-cli SLOWLOG GET 10

# Memory fragmentation
redis-cli INFO memory | grep fragmentation
```

### 4. Provider Latency
```bash
# Check provider response times
curl -s http://localhost/metrics | grep conduit_provider_latency_seconds

# Provider-specific metrics
curl -s http://localhost/metrics | grep conduit_model_response_time_seconds

# Test provider directly
time curl -X POST https://api.openai.com/v1/chat/completions \
  -H "Authorization: Bearer $OPENAI_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"model": "gpt-3.5-turbo", "messages": [{"role": "user", "content": "test"}]}'
```

### 5. System Resources
```bash
# CPU throttling
docker stats conduit-api --no-stream

# Thread pool starvation
docker exec conduit-api dotnet-counters monitor -n ConduitLLM.Http --counters System.Runtime

# Network latency
ping -c 10 database-host
traceroute api.openai.com
```

## Resolution Steps

### Quick Optimizations

1. **Increase Cache TTL**
   ```bash
   # Extend cache duration for stable data
   export CONDUITLLM__CACHE__DEFAULTTTL=3600
   docker restart conduit-api
   ```

2. **Enable Response Compression**
   ```bash
   # Enable gzip compression
   export CONDUITLLM__COMPRESSION__ENABLED=true
   export CONDUITLLM__COMPRESSION__LEVEL=Optimal
   docker restart conduit-api
   ```

3. **Optimize Database Queries**
   ```sql
   -- Update statistics
   ANALYZE;
   
   -- Reindex frequently used tables
   REINDEX TABLE virtual_keys;
   REINDEX TABLE request_logs;
   
   -- Vacuum to remove dead tuples
   VACUUM ANALYZE;
   ```

### Scaling Solutions

1. **Horizontal Scaling**
   ```bash
   # Add more API instances
   docker-compose up -d --scale api=3
   
   # Enable load balancing
   docker run -d -p 80:80 \
     --link conduit-api_1:api1 \
     --link conduit-api_2:api2 \
     --link conduit-api_3:api3 \
     nginx:alpine
   ```

2. **Database Read Replicas**
   ```bash
   # Configure read replica
   export CONDUITLLM__DATABASE__READREPLICAS="Host=replica1;Host=replica2"
   docker restart conduit-api
   ```

3. **Redis Cluster Mode**
   ```bash
   # Switch to Redis cluster
   export REDIS_URL="redis://cluster.redis.local:6379"
   docker restart conduit-api
   ```

### Performance Tuning

1. **Connection Pool Optimization**
   ```bash
   # Increase pool size for high load
   export CONDUITLLM__DATABASE__MINPOOLSIZE=20
   export CONDUITLLM__DATABASE__MAXPOOLSIZE=100
   docker restart conduit-api
   ```

2. **Request Timeout Adjustments**
   ```bash
   # Increase timeouts for slow providers
   export CONDUITLLM__PROVIDERS__OPENAI__TIMEOUT=60
   export CONDUITLLM__PROVIDERS__ANTHROPIC__TIMEOUT=60
   docker restart conduit-api
   ```

3. **Batch Processing**
   ```bash
   # Enable request batching
   export CONDUITLLM__FEATURES__BATCHREQUESTS=true
   export CONDUITLLM__FEATURES__BATCHSIZE=10
   docker restart conduit-api
   ```

## Long-term Improvements

### 1. Query Optimization
```sql
-- Add missing indexes
CREATE INDEX CONCURRENTLY idx_request_logs_timestamp ON request_logs(timestamp);
CREATE INDEX CONCURRENTLY idx_virtual_keys_key_hash ON virtual_keys(key_hash);

-- Partition large tables
CREATE TABLE request_logs_2024_01 PARTITION OF request_logs
FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');
```

### 2. Caching Strategy
```yaml
# Implement multi-tier caching
- L1: In-memory cache (50MB)
- L2: Redis cache (1GB)
- L3: Database query cache

# Cache warming
- Pre-load frequently used data
- Background refresh for expiring entries
```

### 3. Async Processing
```csharp
// Move heavy operations to background queues
- Image generation
- Video processing
- Webhook deliveries
- Report generation
```

## Monitoring

### Key Metrics
```promql
# Response time by percentile
histogram_quantile(0.50, sum(rate(conduit_http_request_duration_seconds_bucket[5m])) by (le))
histogram_quantile(0.95, sum(rate(conduit_http_request_duration_seconds_bucket[5m])) by (le))
histogram_quantile(0.99, sum(rate(conduit_http_request_duration_seconds_bucket[5m])) by (le))

# Slow request rate
sum(rate(conduit_http_request_duration_seconds_count{duration_seconds>1}[5m]))

# Provider latency contribution
avg(conduit_provider_latency_seconds) by (provider)
```

### SLA Tracking
```bash
# Calculate availability
echo "scale=4; (1 - $(curl -s http://localhost/metrics | grep -oP 'error_rate \K[0-9.]+')) * 100" | bc

# Response time SLA
curl -s http://localhost/metrics | grep conduit_http_request_duration_seconds_summary
```

## Prevention

1. **Load Testing**
   ```bash
   # Regular performance tests
   k6 run --vus 50 --duration 30m performance-test.js
   
   # Stress testing
   k6 run --vus 200 --duration 10m stress-test.js
   ```

2. **Performance Budgets**
   - p50 < 100ms
   - p95 < 500ms
   - p99 < 2000ms

3. **Continuous Profiling**
   ```bash
   # Enable profiling endpoint
   export CONDUITLLM__FEATURES__PROFILING=true
   
   # Collect profiles regularly
   dotnet-trace collect -p $(pgrep ConduitLLM.Http) --duration 00:00:30
   ```

## Related Runbooks
- [Database Connection Pool](./db-connection-pool.md)
- [Redis Memory Usage](./redis-memory.md)
- [Provider Health](./provider-health.md)
- [Task Queue Backup](./task-queue-backup.md)