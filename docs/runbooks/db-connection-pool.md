# Runbook: Database Connection Pool Exhausted

## Alert Details
- **Alert Name**: DatabaseConnectionPoolExhausted
- **Severity**: Critical
- **Condition**: `conduit_database_connections_available < 10` for more than 2 minutes
- **Impact**: New requests may fail with connection timeouts, degraded performance

## Diagnosis Steps

### 1. Check Current Connection Status
```bash
# View all active connections
psql -U conduit -c "SELECT pid, usename, application_name, client_addr, state, state_change, query FROM pg_stat_activity WHERE datname = 'conduit' ORDER BY state_change;"

# Count connections by state
psql -U conduit -c "SELECT state, count(*) FROM pg_stat_activity WHERE datname = 'conduit' GROUP BY state ORDER BY count DESC;"

# Check connection pool metrics
curl -s http://localhost/metrics | grep conduit_database_connections
```

### 2. Identify Connection Leaks
```bash
# Find long-running idle connections
psql -U conduit -c "SELECT pid, usename, application_name, state_change, current_timestamp - state_change as idle_time FROM pg_stat_activity WHERE datname = 'conduit' AND state = 'idle' AND current_timestamp - state_change > interval '5 minutes';"

# Find connections holding locks
psql -U conduit -c "SELECT pid, usename, query, state FROM pg_stat_activity WHERE pid IN (SELECT pid FROM pg_locks WHERE granted = false);"
```

### 3. Check Application Logs
```bash
# Look for connection errors
docker logs conduit-api --tail 1000 | grep -E "(connection|pool|timeout|Npgsql)"

# Check for specific errors
docker logs conduit-api --tail 1000 | grep -E "(exhausted|too many connections|timeout expired)"
```

### 4. Monitor Query Performance
```bash
# Find slow queries
psql -U conduit -c "SELECT pid, now() - pg_stat_activity.query_start AS duration, query FROM pg_stat_activity WHERE (now() - pg_stat_activity.query_start) > interval '5 minutes';"

# Check for blocking queries
psql -U conduit -c "SELECT blocked_locks.pid AS blocked_pid, blocked_activity.usename AS blocked_user, blocking_locks.pid AS blocking_pid, blocking_activity.usename AS blocking_user, blocked_activity.query AS blocked_statement, blocking_activity.query AS current_statement_in_blocking_process FROM pg_catalog.pg_locks blocked_locks JOIN pg_catalog.pg_stat_activity blocked_activity ON blocked_activity.pid = blocked_locks.pid JOIN pg_catalog.pg_locks blocking_locks ON blocking_locks.locktype = blocked_locks.locktype AND blocking_locks.database IS NOT DISTINCT FROM blocked_locks.database AND blocking_locks.relation IS NOT DISTINCT FROM blocked_locks.relation AND blocking_locks.page IS NOT DISTINCT FROM blocked_locks.page AND blocking_locks.tuple IS NOT DISTINCT FROM blocked_locks.tuple AND blocking_locks.virtualxid IS NOT DISTINCT FROM blocked_locks.virtualxid AND blocking_locks.transactionid IS NOT DISTINCT FROM blocked_locks.transactionid AND blocking_locks.classid IS NOT DISTINCT FROM blocked_locks.classid AND blocking_locks.objid IS NOT DISTINCT FROM blocked_locks.objid AND blocking_locks.objsubid IS NOT DISTINCT FROM blocked_locks.objsubid AND blocking_locks.pid != blocked_locks.pid JOIN pg_catalog.pg_stat_activity blocking_activity ON blocking_activity.pid = blocking_locks.pid WHERE NOT blocked_locks.granted;"
```

## Resolution Steps

### Immediate Actions

1. **Kill Idle Connections**
   ```bash
   # Kill connections idle for more than 5 minutes
   psql -U conduit -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'conduit' AND state = 'idle' AND state_change < current_timestamp - interval '5 minutes';"
   
   # Kill all idle connections (more aggressive)
   psql -U conduit -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'conduit' AND state = 'idle';"
   ```

2. **Kill Long-Running Queries**
   ```bash
   # Kill queries running longer than 10 minutes
   psql -U conduit -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'conduit' AND state != 'idle' AND query_start < current_timestamp - interval '10 minutes';"
   ```

3. **Restart Affected Services**
   ```bash
   # Rolling restart to reset connection pools
   docker restart conduit-api
   # Wait 30 seconds between instances if running multiple
   ```

### Configuration Adjustments

1. **Increase Connection Pool Size (Temporary)**
   ```bash
   # Update environment variable
   export CONDUITLLM__DATABASE__MAXPOOLSIZE=150
   docker restart conduit-api
   ```

2. **Adjust PostgreSQL Settings**
   ```bash
   # Edit postgresql.conf
   sudo nano /etc/postgresql/*/main/postgresql.conf
   
   # Increase max_connections (requires restart)
   max_connections = 200
   
   # Restart PostgreSQL
   sudo systemctl restart postgresql
   ```

3. **Configure Connection Timeouts**
   ```bash
   # Add to connection string
   export ConnectionStrings__DefaultConnection="Host=localhost;Database=conduit;Username=conduit;Password=xxx;Maximum Pool Size=100;Connection Idle Lifetime=300;Connection Pruning Interval=10"
   ```

### Long-term Solutions

1. **Enable Connection Pool Monitoring**
   ```csharp
   // Add to appsettings.json
   {
     "ConduitLLM": {
       "Database": {
         "EnableDetailedMetrics": true,
         "LogConnectionPoolEvents": true
       }
     }
   }
   ```

2. **Implement Query Timeouts**
   ```bash
   # Set statement timeout in PostgreSQL
   psql -U conduit -c "ALTER DATABASE conduit SET statement_timeout = '30s';"
   ```

3. **Add Connection Health Checks**
   ```bash
   # Configure health check to monitor pool
   export CONDUITLLM__HEALTHCHECKS__DATABASE__WARNTHRESHOLD=80
   export CONDUITLLM__HEALTHCHECKS__DATABASE__CRITICALTHRESHOLD=90
   ```

## Prevention

1. **Regular Monitoring**
   - Set up alerts at 50%, 70%, and 90% pool usage
   - Monitor connection lifetime metrics
   - Track query performance trends

2. **Code Review Guidelines**
   - Ensure all database contexts are properly disposed
   - Use `async`/`await` for all database operations
   - Implement circuit breakers for database calls

3. **Load Testing**
   - Test with expected peak load + 50%
   - Monitor connection pool behavior under stress
   - Identify connection leak patterns

## Metrics to Track

```promql
# Connection pool utilization
(conduit_database_connections_active / conduit_database_connections_active + conduit_database_connections_available) * 100

# Connection wait time
histogram_quantile(0.95, conduit_database_connection_wait_duration_seconds_bucket)

# Failed connection attempts
rate(conduit_database_connection_failures_total[5m])
```

## Related Runbooks
- [API Down](./api-down.md)
- [High Response Time](./high-response-time.md)
- [High Error Rate](./high-error-rate.md)