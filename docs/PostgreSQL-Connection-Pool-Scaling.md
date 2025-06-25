# PostgreSQL Connection Pool Scaling Configuration

This document describes the PostgreSQL configuration changes needed to support 10,000 concurrent customers across multiple Conduit Core API instances.

## Overview

Conduit has been updated to support service-specific connection pool settings that optimize database connection usage based on traffic patterns:

- **Core API**: High traffic (150 max connections per instance)
- **Admin API**: Medium traffic (75 max connections per instance)  
- **WebUI**: No direct database access (uses Admin API)

## PostgreSQL Server Configuration

### Required postgresql.conf Changes

Add or update these settings in your PostgreSQL server's `postgresql.conf` file:

```ini
# Connection settings
max_connections = 500                  # Support up to 500 concurrent connections
superuser_reserved_connections = 10    # Reserve connections for admin tasks

# Memory settings optimized for 500 connections (adjust based on available RAM)
shared_buffers = 4GB                   # 25% of RAM for dedicated database server
effective_cache_size = 12GB            # 75% of RAM for dedicated database server
work_mem = 16MB                        # Per-operation memory
maintenance_work_mem = 256MB           # For VACUUM, index creation, etc.

# Connection pooling optimization
max_prepared_transactions = 100        # For prepared statements
max_locks_per_transaction = 128        # Prevent lock exhaustion

# Performance settings
random_page_cost = 1.1                 # For SSD storage (use 4.0 for HDD)
effective_io_concurrency = 200         # For SSD storage
max_parallel_workers_per_gather = 4    # Parallel query execution
max_parallel_workers = 8               # Total parallel workers

# Logging for monitoring
log_connections = on                   # Log new connections
log_disconnections = on                # Log disconnections
log_lock_waits = on                   # Log lock waits
deadlock_timeout = 1s                  # Detect deadlocks quickly
```

### Applying Configuration Changes

1. Edit postgresql.conf:
   ```bash
   sudo nano /etc/postgresql/16/main/postgresql.conf
   ```

2. Restart PostgreSQL:
   ```bash
   sudo systemctl restart postgresql
   ```

3. Verify settings:
   ```sql
   SHOW max_connections;
   SHOW shared_buffers;
   ```

## Application Connection Pool Settings

The application automatically applies optimized connection pool settings based on the service type:

### Core API (High Traffic)
- Min Pool Size: 10
- Max Pool Size: 150
- Connection Lifetime: 300 seconds
- Connection Idle Lifetime: 60 seconds

### Admin API (Medium Traffic)
- Min Pool Size: 5
- Max Pool Size: 75
- Connection Lifetime: 300 seconds
- Connection Idle Lifetime: 60 seconds

### Environment Variable Configuration

Continue using the standard `DATABASE_URL` environment variable:

```bash
DATABASE_URL=postgresql://conduit:password@postgres:5432/conduitdb
```

The application will automatically append the appropriate pooling parameters based on the service type.

## Connection Pool Monitoring

### Health Check Endpoints

Monitor connection pool health via these endpoints:

- `/health` - Full health check including connection pool status
- `/health/ready` - Readiness check with pool metrics

Example response:
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "database_pool",
      "status": "Healthy",
      "description": "Connection pool healthy: 25/150 connections in use",
      "data": {
        "activeConnections": 25,
        "idleConnections": 5,
        "maxPoolSize": 150,
        "minPoolSize": 10,
        "usagePercent": 16.67,
        "connectionAcquisitionTimeMs": 12
      }
    }
  ]
}
```

### Warning Thresholds

- **80% Usage**: Health check returns "Degraded" status
- **90% Usage**: Health check returns "Unhealthy" status
- **>50ms Acquisition Time**: Warning logged (indicates pool exhaustion)

### PostgreSQL Monitoring Queries

Monitor active connections by application:
```sql
SELECT 
    application_name,
    state,
    COUNT(*) as connection_count,
    MAX(state_change) as last_activity
FROM pg_stat_activity
WHERE datname = 'conduitdb'
GROUP BY application_name, state
ORDER BY connection_count DESC;
```

Find long-running idle connections:
```sql
SELECT 
    pid,
    application_name,
    state,
    state_change,
    NOW() - state_change as idle_duration
FROM pg_stat_activity
WHERE state = 'idle'
  AND NOW() - state_change > interval '5 minutes'
ORDER BY idle_duration DESC;
```

## Connection Pool Warming

Both Core API and Admin API implement connection pool warming on startup:

- **Core API**: Warms 10 connections
- **Admin API**: Warms 5 connections

This reduces latency for initial requests by pre-establishing database connections.

## Deployment Recommendations

### Development Environment
- Use default settings (no changes needed)
- SQLite fallback works automatically

### Staging Environment
- Set PostgreSQL max_connections to 200
- Test with realistic load patterns
- Monitor connection pool metrics

### Production Environment
1. **PostgreSQL Server**:
   - Dedicated server with 16GB+ RAM
   - SSD storage for optimal performance
   - Regular VACUUM and index maintenance

2. **Application Deployment**:
   - 3-5 Core API instances per region
   - 2-3 Admin API instances per region
   - Monitor connection pool usage via health checks

3. **Scaling Strategy**:
   - Start with recommended settings
   - Monitor actual usage patterns
   - Adjust pool sizes based on metrics

## Troubleshooting

### Connection Pool Exhaustion

Symptoms:
- Health check shows >80% pool usage
- Slow connection acquisition times
- Timeout errors in logs

Solutions:
1. Increase max_connections in PostgreSQL
2. Add more application instances
3. Investigate connection leaks

### Performance Degradation

Check for:
- Long-running queries holding connections
- Missing database indexes
- Insufficient work_mem settings

### Monitoring Commands

View current connection settings:
```bash
# From application logs
grep "Using provider: postgres" /var/log/conduit/api.log

# From PostgreSQL
psql -c "SELECT name, setting FROM pg_settings WHERE name LIKE '%connection%';"
```

## Security Considerations

1. **Connection Limits**: Set per-user connection limits to prevent DoS
2. **SSL/TLS**: Always use encrypted connections in production
3. **Connection Monitoring**: Alert on unusual connection patterns
4. **Resource Limits**: Set statement timeouts to prevent runaway queries

## References

- [PostgreSQL Connection Pooling](https://www.postgresql.org/docs/current/runtime-config-connection.html)
- [Npgsql Connection String Parameters](https://www.npgsql.org/doc/connection-string-parameters.html)
- [PgBouncer](https://www.pgbouncer.org/) - External connection pooler for extreme scale