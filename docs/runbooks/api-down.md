# Runbook: API Down

## Alert Details
- **Alert Name**: APIDown
- **Severity**: Critical
- **Condition**: `up{job="conduit-api"} == 0` for more than 1 minute
- **Impact**: Complete service outage - no API requests can be processed

## Diagnosis Steps

### 1. Verify Alert Accuracy
```bash
# Check if API is responding
curl -I https://api.conduit.im/health
curl -I https://api.conduit.im/health/ready
curl -I https://api.conduit.im/health/live
```

### 2. Check Container/Pod Status
```bash
# Docker
docker ps -a | grep conduit-api
docker logs conduit-api --tail 100

# Kubernetes
kubectl get pods -l app=conduit-api
kubectl describe pod <pod-name>
kubectl logs <pod-name> --tail 100
```

### 3. Check System Resources
```bash
# Memory
free -h
docker stats conduit-api

# Disk space
df -h
du -sh /var/lib/docker/

# CPU
top -p $(pgrep -f ConduitLLM.Http)
```

### 4. Check Database Connectivity
```bash
# Test database connection
docker exec conduit-api dotnet exec ConduitLLM.Http.dll --test-db

# Check PostgreSQL status
systemctl status postgresql
pg_isready -h localhost -p 5432
```

### 5. Check Redis Connectivity
```bash
# Test Redis connection
redis-cli ping
redis-cli info server
```

## Resolution Steps

### Quick Fixes

1. **Restart API Service**
   ```bash
   # Docker
   docker restart conduit-api
   
   # Kubernetes
   kubectl rollout restart deployment conduit-api
   ```

2. **Clear Disk Space**
   ```bash
   # Clean up Docker resources
   docker system prune -af
   
   # Clean up logs
   find /var/log -type f -name "*.log" -mtime +7 -delete
   ```

3. **Scale Horizontally (if under load)**
   ```bash
   # Docker Compose
   docker-compose up -d --scale api=3
   
   # Kubernetes
   kubectl scale deployment conduit-api --replicas=3
   ```

### Database Issues

1. **Connection Pool Exhaustion**
   ```bash
   # Check active connections
   psql -U conduit -c "SELECT count(*) FROM pg_stat_activity WHERE datname = 'conduit';"
   
   # Kill idle connections
   psql -U conduit -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'conduit' AND state = 'idle' AND state_change < current_timestamp - interval '5 minutes';"
   ```

2. **Database Restart**
   ```bash
   systemctl restart postgresql
   ```

### Redis Issues

1. **Memory Issues**
   ```bash
   # Clear Redis cache (CAUTION: This will clear all cached data)
   redis-cli FLUSHDB
   
   # Check memory usage
   redis-cli INFO memory
   ```

2. **Redis Restart**
   ```bash
   systemctl restart redis
   ```

### Application Issues

1. **Check Configuration**
   ```bash
   # Verify environment variables
   docker exec conduit-api env | grep CONDUIT
   
   # Check appsettings
   docker exec conduit-api cat appsettings.json
   ```

2. **Enable Debug Logging**
   ```bash
   # Set logging level
   export Logging__LogLevel__Default=Debug
   docker restart conduit-api
   ```

## Escalation

If the issue persists after 15 minutes:

1. **Page the on-call engineer**
2. **Notify the team lead**
3. **Create incident channel in Slack**
4. **Start incident response process**

## Prevention

1. **Set up automatic restarts**
   ```yaml
   # Docker Compose
   restart: unless-stopped
   
   # Kubernetes
   livenessProbe:
     httpGet:
       path: /health/live
       port: 80
     periodSeconds: 30
   ```

2. **Monitor resource usage trends**
   - Set up alerts for 80% CPU/Memory usage
   - Monitor database connection pool usage
   - Track Redis memory consumption

3. **Regular health checks**
   - Implement comprehensive health check endpoints
   - Monitor all external dependencies
   - Set up synthetic monitoring

## Related Runbooks
- [Database Connection Pool Exhausted](./db-connection-pool.md)
- [High Response Time](./high-response-time.md)
- [Redis Memory Usage](./redis-memory.md)