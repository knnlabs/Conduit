# Operational Runbooks

This guide provides step-by-step procedures for common operational tasks and incident response scenarios in ConduitLLM production environments.

## Overview

These runbooks cover:
- Incident response procedures
- Common troubleshooting scenarios
- Maintenance operations
- Performance optimization
- Disaster recovery

## Incident Response

### High Error Rate

**Symptoms:**
- Error rate above 5% for more than 5 minutes
- Multiple user complaints
- Alerts from monitoring system

**Investigation Steps:**

1. **Check System Health**
   ```bash
   # Check overall health
   curl -s http://api.conduit.example.com/health/ready | jq
   
   # Check provider health
   curl -s http://admin.conduit.example.com/api/admin/providers/health \
     -H "X-Master-Key: $MASTER_KEY" | jq
   ```

2. **Review Recent Logs**
   ```bash
   # Check API logs for errors
   kubectl logs -n conduit -l app=conduit-api --tail=100 | grep ERROR
   
   # Check specific provider errors
   kubectl logs -n conduit -l app=conduit-api --tail=1000 | \
     grep -E "(openai|anthropic|googlecloud)" | grep -i error
   ```

3. **Check Metrics**
   ```bash
   # Query Prometheus for error rates
   curl -s "http://prometheus:9090/api/v1/query?query=rate(conduit_llm_requests_total{status='error'}[5m])" | jq
   
   # Check provider-specific errors
   curl -s "http://prometheus:9090/api/v1/query?query=conduit_provider_health" | jq
   ```

**Resolution Steps:**

1. **Provider Issues**
   ```bash
   # Disable unhealthy provider
   curl -X PATCH http://admin.conduit.example.com/api/admin/providers/openai/disable \
     -H "X-Master-Key: $MASTER_KEY"
   
   # Force health check
   curl -X POST http://admin.conduit.example.com/api/admin/providers/health/check \
     -H "X-Master-Key: $MASTER_KEY"
   ```

2. **Circuit Breaker Reset**
   ```bash
   # Reset circuit breaker for provider
   curl -X POST http://admin.conduit.example.com/api/admin/providers/openai/reset-circuit \
     -H "X-Master-Key: $MASTER_KEY"
   ```

3. **Scale Up if Needed**
   ```bash
   # Increase replicas
   kubectl scale deployment conduit-api -n conduit --replicas=10
   
   # Or update HPA
   kubectl patch hpa conduit-api-hpa -n conduit \
     -p '{"spec":{"minReplicas":5,"maxReplicas":30}}'
   ```

### Database Connection Issues

**Symptoms:**
- "Database connection failed" in health checks
- Timeout errors in logs
- Slow response times

**Investigation Steps:**

1. **Check Database Status**
   ```bash
   # Check PostgreSQL pod status
   kubectl get pods -n conduit -l app=postgres
   
   # Check connection pool stats
   kubectl exec -n conduit postgres-0 -- psql -U conduit -c \
     "SELECT count(*) as total, state FROM pg_stat_activity GROUP BY state;"
   ```

2. **Review Connection Limits**
   ```bash
   # Check max connections
   kubectl exec -n conduit postgres-0 -- psql -U conduit -c \
     "SHOW max_connections;"
   
   # Check current connections
   kubectl exec -n conduit postgres-0 -- psql -U conduit -c \
     "SELECT count(*) FROM pg_stat_activity;"
   ```

**Resolution Steps:**

1. **Kill Idle Connections**
   ```sql
   -- Terminate idle connections older than 5 minutes
   SELECT pg_terminate_backend(pid) 
   FROM pg_stat_activity 
   WHERE state = 'idle' 
   AND state_change < NOW() - INTERVAL '5 minutes'
   AND datname = 'conduit';
   ```

2. **Restart Connection Pool**
   ```bash
   # Rolling restart of API pods
   kubectl rollout restart deployment conduit-api -n conduit
   
   # Monitor rollout
   kubectl rollout status deployment conduit-api -n conduit
   ```

3. **Increase Connection Limits**
   ```bash
   # Update PostgreSQL config
   kubectl exec -n conduit postgres-0 -- psql -U postgres -c \
     "ALTER SYSTEM SET max_connections = 500;"
   
   # Restart PostgreSQL
   kubectl delete pod -n conduit postgres-0
   ```

### Memory Issues

**Symptoms:**
- OutOfMemoryError in logs
- Pods being killed (OOMKilled)
- Degraded performance

**Investigation Steps:**

1. **Check Memory Usage**
   ```bash
   # Current memory usage
   kubectl top pods -n conduit
   
   # Historical memory usage
   curl -s "http://prometheus:9090/api/v1/query_range?query=container_memory_usage_bytes{namespace='conduit'}" | jq
   ```

2. **Identify Memory Leaks**
   ```bash
   # Check for increasing memory over time
   kubectl exec -n conduit conduit-api-xxxx -- \
     curl -s http://localhost:8080/metrics | grep dotnet_gc
   ```

**Resolution Steps:**

1. **Immediate Relief**
   ```bash
   # Restart affected pods
   kubectl delete pod -n conduit conduit-api-xxxx
   
   # Increase memory limits temporarily
   kubectl set resources deployment conduit-api -n conduit \
     --limits=memory=4Gi --requests=memory=2Gi
   ```

2. **Cache Cleanup**
   ```bash
   # Clear Redis cache
   kubectl exec -n conduit redis-0 -- redis-cli FLUSHDB
   
   # Clear specific cache patterns
   kubectl exec -n conduit redis-0 -- redis-cli --scan --pattern "cache:*" | \
     xargs kubectl exec -n conduit redis-0 -- redis-cli DEL
   ```

## Maintenance Operations

### Rolling Updates

**Pre-deployment Checklist:**
- [ ] Backup database
- [ ] Check current system health
- [ ] Notify users of maintenance window
- [ ] Prepare rollback plan

**Deployment Steps:**

1. **Create Backup**
   ```bash
   # Trigger manual backup
   kubectl create job -n conduit manual-backup-$(date +%Y%m%d-%H%M%S) \
     --from=cronjob/postgres-backup
   
   # Verify backup completed
   kubectl logs -n conduit -l job-name=manual-backup-* --tail=100
   ```

2. **Deploy New Version**
   ```bash
   # Update image
   kubectl set image deployment/conduit-api -n conduit \
     api=your-registry/conduit:v1.2.3
   
   # Monitor rollout
   kubectl rollout status deployment conduit-api -n conduit
   
   # Check pod status
   kubectl get pods -n conduit -l app=conduit-api -w
   ```

3. **Verify Deployment**
   ```bash
   # Check health
   curl -s http://api.conduit.example.com/health/ready | jq
   
   # Run smoke tests
   ./scripts/smoke-tests.sh
   
   # Check metrics
   curl -s http://api.conduit.example.com/metrics | grep conduit_
   ```

**Rollback if Needed:**
```bash
# Rollback to previous version
kubectl rollout undo deployment conduit-api -n conduit

# Or rollback to specific revision
kubectl rollout undo deployment conduit-api -n conduit --to-revision=2
```

### Database Maintenance

**Regular Maintenance Tasks:**

1. **Vacuum and Analyze**
   ```sql
   -- Run vacuum analyze
   VACUUM ANALYZE;
   
   -- Check table sizes
   SELECT 
     schemaname,
     tablename,
     pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
   FROM pg_tables 
   WHERE schemaname = 'public' 
   ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
   ```

2. **Index Maintenance**
   ```sql
   -- Find unused indexes
   SELECT 
     schemaname, 
     tablename, 
     indexname, 
     idx_scan
   FROM pg_stat_user_indexes 
   WHERE idx_scan = 0 
   AND schemaname = 'public';
   
   -- Rebuild bloated indexes
   REINDEX TABLE request_logs;
   ```

3. **Archive Old Data**
   ```sql
   -- Archive logs older than 90 days
   INSERT INTO request_logs_archive 
   SELECT * FROM request_logs 
   WHERE timestamp < NOW() - INTERVAL '90 days';
   
   -- Delete archived records
   DELETE FROM request_logs 
   WHERE timestamp < NOW() - INTERVAL '90 days';
   ```

### Certificate Renewal

**Check Certificate Expiration:**
```bash
# Check ingress certificates
kubectl get certificate -n conduit

# Check certificate details
kubectl describe certificate conduit-tls -n conduit

# Manual check
echo | openssl s_client -servername api.conduit.example.com \
  -connect api.conduit.example.com:443 2>/dev/null | \
  openssl x509 -noout -dates
```

**Renew Certificates:**
```bash
# Force renewal with cert-manager
kubectl delete certificate conduit-tls -n conduit

# Monitor renewal
kubectl logs -n cert-manager deployment/cert-manager -f

# Verify new certificate
kubectl get certificate conduit-tls -n conduit
```

## Performance Optimization

### Slow Response Times

**Investigation:**

1. **Identify Slow Queries**
   ```sql
   -- Find slow queries
   SELECT 
     query,
     mean_exec_time,
     calls,
     total_exec_time
   FROM pg_stat_statements 
   ORDER BY mean_exec_time DESC 
   LIMIT 20;
   ```

2. **Check Cache Performance**
   ```bash
   # Redis stats
   kubectl exec -n conduit redis-0 -- redis-cli INFO stats
   
   # Cache hit rate
   curl -s "http://prometheus:9090/api/v1/query?query=rate(conduit_cache_hits_total[5m])" | jq
   ```

**Optimization Steps:**

1. **Database Tuning**
   ```sql
   -- Add missing indexes
   CREATE INDEX CONCURRENTLY idx_request_logs_timestamp 
   ON request_logs(timestamp);
   
   -- Update statistics
   ANALYZE request_logs;
   ```

2. **Cache Warming**
   ```bash
   # Warm popular models cache
   curl -X POST http://admin.conduit.example.com/api/admin/cache/warm \
     -H "X-Master-Key: $MASTER_KEY" \
     -H "Content-Type: application/json" \
     -d '{"models": ["gpt-4", "claude-3-opus"]}'
   ```

### High Latency to Providers

**Investigation:**
```bash
# Check provider latencies
curl -s "http://prometheus:9090/api/v1/query?query=conduit_provider_response_time_seconds" | jq

# Test direct connectivity
curl -w "@curl-format.txt" -o /dev/null -s https://api.openai.com/v1/models
```

**Resolution:**
1. **Enable Regional Routing**
   ```bash
   # Configure regional endpoints
   curl -X POST http://admin.conduit.example.com/api/admin/providers/config \
     -H "X-Master-Key: $MASTER_KEY" \
     -H "Content-Type: application/json" \
     -d '{
       "provider": "openai",
       "regionalEndpoints": {
         "us-east": "https://api.openai.com",
         "eu-west": "https://eu.api.openai.com"
       }
     }'
   ```

## Disaster Recovery

### Database Recovery

**From Backup:**
```bash
# List available backups
aws s3 ls s3://conduit-backups/ | grep sql.gz

# Download backup
aws s3 cp s3://conduit-backups/conduit-20240115-020000.sql.gz .

# Restore database
gunzip -c conduit-20240115-020000.sql.gz | \
  kubectl exec -i -n conduit postgres-0 -- psql -U conduit
```

**Point-in-Time Recovery:**
```bash
# Stop application
kubectl scale deployment conduit-api -n conduit --replicas=0

# Restore to specific time
kubectl exec -n conduit postgres-0 -- pg_restore \
  --dbname=conduit \
  --target-time="2024-01-15 10:30:00" \
  /backup/base.tar

# Restart application
kubectl scale deployment conduit-api -n conduit --replicas=3
```

### Split-Brain Recovery

**Identify Split-Brain:**
```bash
# Check Redis cluster status
kubectl exec -n conduit redis-0 -- redis-cli CLUSTER NODES

# Check for multiple masters
kubectl exec -n conduit redis-0 -- redis-cli CLUSTER INFO
```

**Resolution:**
```bash
# Force failover to single master
kubectl exec -n conduit redis-1 -- redis-cli CLUSTER FAILOVER FORCE

# Reset cluster if needed
kubectl exec -n conduit redis-0 -- redis-cli CLUSTER RESET HARD
```

## Monitoring and Alerting

### Alert Response Matrix

| Alert | Severity | Response Time | Actions |
|-------|----------|---------------|---------|
| Provider Down | Critical | 5 min | Disable provider, notify on-call |
| High Error Rate | High | 15 min | Check logs, scale up if needed |
| Database Connection Pool Exhausted | High | 10 min | Kill idle connections, restart pods |
| Memory > 90% | Medium | 30 min | Clear cache, increase limits |
| Disk > 80% | Medium | 1 hour | Archive old data, expand volume |
| Certificate Expiring | Low | 1 day | Renew certificate |

### On-Call Procedures

**Initial Response:**
1. Acknowledge alert within 5 minutes
2. Join incident channel
3. Run initial diagnostics
4. Escalate if needed

**Communication:**
- Update status page
- Notify stakeholders
- Document actions in incident channel
- Create post-mortem ticket

## Tools and Scripts

### Health Check Script
```bash
#!/bin/bash
# check-health.sh

echo "=== System Health Check ==="
echo "Timestamp: $(date)"

# API Health
echo -e "\n--- API Health ---"
curl -s http://api.conduit.example.com/health/ready | jq

# Provider Health  
echo -e "\n--- Provider Health ---"
curl -s http://admin.conduit.example.com/api/admin/providers/health \
  -H "X-Master-Key: $MASTER_KEY" | jq

# Database Status
echo -e "\n--- Database Status ---"
kubectl exec -n conduit postgres-0 -- psql -U conduit -c \
  "SELECT count(*) as connections, state FROM pg_stat_activity GROUP BY state;"

# Redis Status
echo -e "\n--- Redis Status ---"
kubectl exec -n conduit redis-0 -- redis-cli INFO server | grep uptime

# Pod Status
echo -e "\n--- Pod Status ---"
kubectl get pods -n conduit
```

### Quick Diagnostics
```bash
#!/bin/bash
# diagnose.sh

# Recent errors
echo "=== Recent Errors ==="
kubectl logs -n conduit -l app=conduit-api --tail=1000 | \
  grep ERROR | tail -20

# Current metrics
echo -e "\n=== Current Metrics ==="
curl -s http://api.conduit.example.com/metrics | \
  grep -E "(conduit_llm_requests_total|conduit_llm_active_requests)"

# Resource usage
echo -e "\n=== Resource Usage ==="
kubectl top pods -n conduit
```

## Next Steps

- [Health Checks](health-checks.md) - Configure health monitoring
- [Metrics Monitoring](metrics-monitoring.md) - Set up alerting
- [Production Deployment](production-deployment.md) - Deployment procedures
- [Troubleshooting Guide](../troubleshooting/common-issues.md) - Common issues