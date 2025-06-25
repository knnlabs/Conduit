# PostgreSQL Connection Pool Scaling Configuration

This document describes the PostgreSQL configuration changes needed to support 10,000 concurrent customers across multiple Conduit Core API instances.

## Overview

Conduit has been updated to support service-specific connection pool settings that optimize database connection usage based on traffic patterns:

- **Core API**: High traffic (150 max connections per instance)
- **Admin API**: Medium traffic (75 max connections per instance)  
- **WebUI**: No direct database access (uses Admin API)

## Production Scale Calculations

### Connection Requirements for 10,000 Concurrent Customers

**Deployment Assumptions:**
- 8 Core API instances (for redundancy and load distribution)
- 3 Admin API instances
- 20% buffer for burst traffic
- Maintenance connections for monitoring, backups, migrations

**Connection Math:**
```
Core APIs:      8 instances × 150 connections = 1,200
Admin APIs:     3 instances × 75 connections  = 225
Subtotal:                                      1,425
Buffer (20%):                                  285
Maintenance:                                   50
──────────────────────────────────────────────────
Total Required:                                1,760
```

**PostgreSQL Setting:** We set `max_connections = 2000` to handle this load with headroom for growth and preventing connection exhaustion under peak conditions.

### Memory Requirements

With 2000 connections, PostgreSQL requires significant memory:
- **Base requirement**: ~10MB per connection = 20GB
- **Work memory**: 8MB × concurrent queries (worst case 16GB if all connections active)
- **Shared buffers**: 16GB
- **OS and cache**: 16GB
- **Recommended server**: 64GB RAM minimum, 128GB for production

## PostgreSQL Server Configuration

### Required postgresql.conf Changes

Add or update these settings in your PostgreSQL server's `postgresql.conf` file:

```ini
# Connection settings
max_connections = 2000                 # Support production scale with headroom
superuser_reserved_connections = 20    # Reserve connections for admin/monitoring tasks

# Memory settings for 2000 connections (requires 64GB+ RAM server)
shared_buffers = 16GB                  # 25% of 64GB RAM
effective_cache_size = 48GB            # 75% of 64GB RAM
work_mem = 8MB                         # Conservative to prevent memory exhaustion (2000 * 8MB = 16GB worst case)
maintenance_work_mem = 512MB           # For VACUUM, index creation, etc.

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

### How Service Type Propagation Works

The service type is propagated through the connection string itself:

1. **Service Registration**: Each service passes its type to `ConnectionStringManager`:
   ```csharp
   // Core API
   var (dbProvider, dbConnectionString) = connectionStringManager.GetProviderAndConnectionString("CoreAPI", ...);
   
   // Admin API
   var (dbProvider, dbConnectionString) = connectionStringManager.GetProviderAndConnectionString("AdminAPI", ...);
   ```

2. **Connection String Enhancement**: The `ConnectionStringManager` embeds pool settings in the connection string:
   ```
   Host=postgres;Port=5432;Database=conduitdb;Username=conduit;Password=***;
   Pooling=true;MinPoolSize=10;MaxPoolSize=150;ConnectionLifetime=300;ConnectionIdleLifetime=60
   ```

3. **Automatic Application**: When `DbContextFactory` uses this connection string with `UseNpgsql()`, Npgsql automatically applies these pool settings.

4. **Verification**: On startup, services log their pool configuration:
   ```
   [Conduit] Core API database connection pool configured:
   [Conduit]   Min Pool Size: 10
   [Conduit]   Max Pool Size: 150
   ```

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
      "description": "Connection pool healthy: 12ms acquisition time",
      "data": {
        "maxPoolSize": 150,
        "minPoolSize": 10,
        "connectionAcquisitionTimeMs": 12,
        "database": "conduitdb",
        "dataSource": "postgres:5432"
      }
    }
  ]
}
```

### Health Status Thresholds

Based on connection acquisition time:
- **<50ms**: Healthy (pool has available connections)
- **50-200ms**: Degraded (pool under pressure)
- **>200ms**: Unhealthy (pool likely exhausted)
- **Timeout/Exception**: Unhealthy (pool exhausted or database down)

Note: The health check no longer queries `pg_stat_activity` to avoid performance impact at scale. Connection acquisition time is a reliable indicator of pool health without adding database load.

### Metrics Endpoint

Monitor connection pool configuration and performance via the metrics endpoint:

**Pool Metrics**: `GET /metrics/database/pool`
```json
{
  "timestamp": "2025-12-20T10:30:00Z",
  "provider": "postgresql",
  "connectionString": {
    "host": "postgres",
    "port": 5432,
    "database": "conduitdb",
    "applicationName": "Conduit Core API"
  },
  "poolConfiguration": {
    "minPoolSize": 10,
    "maxPoolSize": 150,
    "connectionLifetime": 300,
    "connectionIdleLifetime": 60,
    "pooling": true
  },
  "currentMetrics": {
    "connectionAcquisitionTimeMs": 12,
    "healthStatus": "healthy",
    "note": "For detailed pool statistics, query pg_stat_activity directly or use monitoring tools"
  }
}
```

**All Metrics**: `GET /metrics` - Returns comprehensive metrics including system resources and database pool status.

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

#### 1. PostgreSQL Server Requirements
For 10,000 concurrent customers:
- **Hardware**: 64GB RAM minimum, 128GB recommended
- **Storage**: NVMe SSD with high IOPS
- **CPU**: 16+ cores for parallel query execution
- **Network**: 10Gbps network for database server

#### 2. PostgreSQL Configuration
```ini
# Essential settings for production scale
max_connections = 2000           # Supports ~8 Core APIs + 3 Admin APIs with buffer
shared_buffers = 16GB           # 25% of RAM
effective_cache_size = 48GB     # 75% of RAM
work_mem = 8MB                  # Conservative to prevent memory exhaustion
max_parallel_workers = 16       # Utilize available cores

# Connection settings
tcp_keepalives_idle = 60
tcp_keepalives_interval = 10
tcp_keepalives_count = 6

# Logging for monitoring
log_min_duration_statement = 1000  # Log slow queries (>1s)
log_connections = on
log_disconnections = on
log_lock_waits = on
log_temp_files = 0
```

#### 3. Application Deployment Strategy

**Core API Instances**:
- **Count**: 8-10 instances for 10,000 concurrent customers
- **Resources**: 4GB RAM, 2 vCPUs per instance
- **Connection Pool**: 150 max connections per instance
- **Placement**: Distribute across availability zones

**Admin API Instances**:
- **Count**: 3-4 instances
- **Resources**: 2GB RAM, 1 vCPU per instance
- **Connection Pool**: 75 max connections per instance
- **Placement**: Separate from Core API for isolation

**Load Balancing**:
- Use connection-based (least connections) load balancing
- Configure health check endpoints properly
- Set appropriate connection draining periods

#### 4. Monitoring and Alerting

**Key Metrics to Monitor**:
1. **Connection Pool Health**:
   - Connection acquisition time (alert if >100ms)
   - Pool exhaustion events
   - Connection timeouts

2. **Database Metrics**:
   - Active connections by application
   - Long-running queries (>5s)
   - Lock wait times
   - Replication lag (if using replicas)

3. **Application Metrics**:
   - Request latency percentiles (p50, p95, p99)
   - Error rates by endpoint
   - Memory usage and GC pressure

**Monitoring Tools**:
- **Application**: Use `/metrics` endpoints
- **Database**: pg_stat_activity, pg_stat_statements
- **Infrastructure**: Prometheus + Grafana recommended

#### 5. Performance Optimization

**Connection Pool Tuning**:
```bash
# Monitor actual usage
curl http://api-instance:5000/metrics/database/pool

# Adjust pool sizes based on:
# - Peak concurrent requests
# - Average query duration
# - Connection acquisition times
```

**Database Optimization**:
1. **Indexing**: Ensure proper indexes on frequently queried columns
2. **Partitioning**: Consider partitioning large tables (request_logs, etc.)
3. **Connection Pooling**: Consider pgBouncer for extreme scale (>20 API instances)
4. **Read Replicas**: Offload read queries to replicas

#### 6. Capacity Planning

**Connection Budget**:
```
Core APIs:        8 × 150 = 1,200 connections
Admin APIs:       3 × 75  = 225 connections
Monitoring:                 50 connections
Maintenance:                50 connections
Buffer (20%):               305 connections
────────────────────────────────────────────
Total:                      1,830 connections
PostgreSQL max_connections: 2,000 (safe margin)
```

**Scaling Triggers**:
- Connection acquisition time consistently >50ms
- Database CPU utilization >70%
- Connection pool usage >80%
- Memory pressure on database server

#### 7. Disaster Recovery

**Backup Strategy**:
- Continuous WAL archiving
- Daily full backups
- Point-in-time recovery capability
- Test restore procedures regularly

**High Availability**:
- Primary-replica setup with automatic failover
- Connection strings should support multiple hosts
- Application-level retry logic for transient failures

#### 8. Security Considerations

**Connection Security**:
- Always use SSL/TLS for database connections
- Rotate database credentials regularly
- Use connection pooling with proper authentication
- Implement network segmentation

**Access Control**:
- Separate credentials for Core API and Admin API
- Read-only users for monitoring
- Audit logging for compliance

## Docker Health Check Configuration

### Development Environment
Infrastructure services (PostgreSQL, Redis, RabbitMQ) retain frequent health checks (5s) for quick failure detection.

### Production Environment
Application services should use less frequent health checks to reduce load:

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:8080/health/ready"]
  interval: 60s      # Check every 60 seconds (was 10s)
  timeout: 10s       # Allow 10 seconds for response (was 5s)
  retries: 3         # Retry 3 times before marking unhealthy
  start_period: 60s  # Grace period during startup (was 15s)
```

This reduces health check queries from 360/hour to 60/hour per instance, significantly reducing database load at scale.

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