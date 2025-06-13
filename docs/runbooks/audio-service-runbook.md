# Audio Service Operational Runbook

## Table of Contents
1. [Service Overview](#service-overview)
2. [Architecture](#architecture)
3. [Common Operations](#common-operations)
4. [Troubleshooting](#troubleshooting)
5. [Emergency Procedures](#emergency-procedures)
6. [Monitoring and Alerts](#monitoring-and-alerts)
7. [Performance Tuning](#performance-tuning)
8. [Disaster Recovery](#disaster-recovery)

## Service Overview

The Conduit Audio Service provides:
- Audio transcription (speech-to-text)
- Text-to-speech generation
- Real-time audio streaming
- Hybrid audio conversations

### Key Components
- **Audio Router**: Distributes requests across providers
- **Connection Pool**: Manages provider connections
- **Cache Layer**: Stores frequently accessed audio data
- **Metrics Collector**: Tracks performance and usage

### Dependencies
- Redis (caching)
- PostgreSQL (metadata storage)
- Provider APIs (OpenAI, Google Cloud, AWS)

## Architecture

### Request Flow
```
Client Request
    ↓
API Gateway
    ↓
Audio Service
    ↓
Audio Router → Provider Selection
    ↓
Connection Pool → Provider API
    ↓
Response Processing
    ↓
Cache Storage
    ↓
Client Response
```

### Provider Distribution
- OpenAI: 70% (primary)
- Google Cloud: 20% (secondary)
- AWS: 10% (tertiary)

## Common Operations

### 1. Scaling Operations

#### Scale Up
```bash
# Increase replicas
kubectl scale deployment conduit-audio-service -n production --replicas=5

# Verify scaling
kubectl get pods -n production -l app=conduit-audio
```

#### Scale Down
```bash
# Decrease replicas (minimum 3 for HA)
kubectl scale deployment conduit-audio-service -n production --replicas=3
```

### 2. Provider Management

#### Disable a Provider
```bash
# Edit ConfigMap
kubectl edit configmap conduit-audio-config -n production

# Set provider enabled to false
# AudioService.Providers.OpenAI.Enabled: false

# Restart pods to apply
kubectl rollout restart deployment conduit-audio-service -n production
```

#### Adjust Provider Weights
```bash
# Edit ConfigMap to adjust traffic distribution
kubectl edit configmap conduit-audio-config -n production

# Example: Shift more traffic to Google
# OpenAI.Weight: 50
# Google.Weight: 40
# AWS.Weight: 10
```

### 3. Cache Management

#### Clear Cache
```bash
# Connect to Redis
kubectl exec -it redis-master-0 -n production -- redis-cli

# Clear audio cache keys
redis-cli --scan --pattern "audio:*" | xargs redis-cli DEL
```

#### Monitor Cache Performance
```bash
# Check cache hit rate
curl http://audio-service/metrics | grep audio_cache_hit_rate

# View cache size
redis-cli INFO memory | grep used_memory_human
```

### 4. Connection Pool Management

#### Reset Connection Pool
```bash
# Force connection pool reset
curl -X POST http://audio-service/admin/connection-pool/reset \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

#### View Pool Statistics
```bash
# Get current pool stats
curl http://audio-service/admin/connection-pool/stats \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

## Troubleshooting

### Issue: High Error Rate

#### Symptoms
- Error rate > 5%
- Increased latency
- Failed requests in logs

#### Investigation Steps
1. Check provider status
   ```bash
   # Check health endpoints
   curl http://audio-service/health/providers
   ```

2. Review error logs
   ```bash
   # Get recent errors
   kubectl logs -n production -l app=conduit-audio --tail=100 | grep ERROR
   ```

3. Check provider quotas
   ```bash
   # View current usage
   curl http://audio-service/metrics | grep provider_quota
   ```

#### Resolution
1. If provider issue:
   - Shift traffic to healthy providers
   - Contact provider support if needed

2. If quota exceeded:
   - Enable rate limiting
   - Scale horizontally
   - Request quota increase

### Issue: High Latency

#### Symptoms
- P95 latency > 5 seconds
- Slow response times
- Queue buildup

#### Investigation Steps
1. Check connection pool
   ```bash
   curl http://audio-service/admin/connection-pool/stats
   ```

2. Monitor provider latency
   ```bash
   curl http://audio-service/metrics | grep audio_provider_latency
   ```

3. Check resource usage
   ```bash
   kubectl top pods -n production -l app=conduit-audio
   ```

#### Resolution
1. If pool exhausted:
   - Increase pool size
   - Add more replicas

2. If provider slow:
   - Route to faster providers
   - Enable caching

3. If resource constrained:
   - Increase pod resources
   - Scale horizontally

### Issue: Memory Leak

#### Symptoms
- Increasing memory usage
- OOM kills
- Performance degradation

#### Investigation Steps
1. Monitor memory trends
   ```bash
   # View memory over time
   kubectl top pods -n production -l app=conduit-audio --containers
   ```

2. Analyze heap dumps
   ```bash
   # Trigger heap dump
   kubectl exec -it $POD_NAME -n production -- dotnet-dump collect
   ```

3. Check for connection leaks
   ```bash
   curl http://audio-service/admin/diagnostics/connections
   ```

#### Resolution
1. Restart affected pods
   ```bash
   kubectl delete pod $POD_NAME -n production
   ```

2. Apply memory limits
   ```yaml
   resources:
     limits:
       memory: "4Gi"
   ```

3. Deploy fix and monitor

## Emergency Procedures

### Complete Service Failure

1. **Immediate Actions**
   ```bash
   # Check pod status
   kubectl get pods -n production -l app=conduit-audio
   
   # Check recent events
   kubectl get events -n production --sort-by='.lastTimestamp'
   ```

2. **Failover to Backup Region**
   ```bash
   # Update DNS to point to backup region
   ./scripts/failover-audio-service.sh us-west-2
   ```

3. **Emergency Scaling**
   ```bash
   # Scale to maximum capacity
   kubectl scale deployment conduit-audio-service -n production --replicas=10
   ```

### Provider Outage

1. **Disable Affected Provider**
   ```bash
   # Quick disable via API
   curl -X POST http://audio-service/admin/providers/openai/disable \
     -H "Authorization: Bearer $ADMIN_TOKEN"
   ```

2. **Redistribute Traffic**
   ```bash
   # Update weights for remaining providers
   ./scripts/redistribute-audio-traffic.sh --exclude openai
   ```

3. **Monitor Capacity**
   ```bash
   # Watch metrics during transition
   watch -n 5 'curl -s http://audio-service/metrics | grep audio_operations'
   ```

### Rate Limit Emergency

1. **Enable Emergency Rate Limiting**
   ```bash
   # Apply strict limits
   curl -X POST http://audio-service/admin/rate-limit/emergency \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -d '{"requestsPerMinute": 100}'
   ```

2. **Identify Top Consumers**
   ```bash
   # Get usage by API key
   curl http://audio-service/admin/usage/top?limit=10
   ```

3. **Block Abusive Clients**
   ```bash
   # Add to blocklist
   curl -X POST http://audio-service/admin/blocklist \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -d '{"apiKey": "abusive-key"}'
   ```

## Monitoring and Alerts

### Key Metrics to Monitor

| Metric | Normal Range | Alert Threshold | Action |
|--------|-------------|-----------------|---------|
| Error Rate | < 1% | > 5% | Check provider health |
| P95 Latency | < 2s | > 5s | Scale or optimize |
| Connection Pool Usage | < 70% | > 80% | Increase pool size |
| Cache Hit Rate | > 40% | < 30% | Review cache strategy |
| Memory Usage | < 70% | > 85% | Scale or restart |

### Alert Response Procedures

#### Critical Alerts (Pager)
1. Acknowledge within 5 minutes
2. Join incident channel
3. Follow emergency procedures
4. Update status page

#### Warning Alerts
1. Investigate within 30 minutes
2. Document findings
3. Create follow-up ticket
4. Monitor for escalation

### Dashboard Links
- [Audio Service Overview](https://grafana.conduit.io/d/audio-overview)
- [Provider Health](https://grafana.conduit.io/d/audio-providers)
- [Performance Metrics](https://grafana.conduit.io/d/audio-performance)
- [Error Analysis](https://grafana.conduit.io/d/audio-errors)

## Performance Tuning

### Connection Pool Optimization
```yaml
ConnectionPool:
  MaxConnectionsPerProvider: 100  # Increase for high traffic
  ConnectionTimeout: 30           # Reduce for faster failover
  MaxIdleTime: "00:15:00"        # Adjust based on traffic patterns
```

### Cache Configuration
```yaml
Cache:
  DefaultTranscriptionTtl: "00:30:00"  # Increase for stable content
  DefaultTtsTtl: "01:00:00"            # TTS results are more stable
  MaxMemoryCacheSizeBytes: 1073741824  # 1GB, adjust based on available memory
```

### Request Timeout Tuning
```yaml
AudioService:
  RequestTimeoutSeconds: 300  # 5 minutes for large files
  Providers:
    OpenAI:
      TimeoutSeconds: 120    # Provider-specific timeouts
```

### Batch Processing
```csharp
// Enable batch processing for multiple files
services.Configure<AudioBatchOptions>(options =>
{
    options.MaxBatchSize = 10;
    options.BatchTimeoutMs = 5000;
});
```

## Disaster Recovery

### Backup Procedures

1. **Configuration Backup**
   ```bash
   # Export all ConfigMaps
   kubectl get configmap -n production -o yaml > audio-config-backup.yaml
   
   # Export secrets (encrypted)
   kubectl get secret -n production -o yaml | kubeseal > audio-secrets-backup.yaml
   ```

2. **Cache Backup**
   ```bash
   # Redis snapshot
   kubectl exec -it redis-master-0 -n production -- redis-cli BGSAVE
   ```

3. **Metrics Backup**
   ```bash
   # Export Prometheus data
   ./scripts/backup-prometheus.sh audio-metrics
   ```

### Recovery Procedures

1. **Service Recovery**
   ```bash
   # Deploy from backup
   kubectl apply -f audio-service-backup.yaml
   
   # Restore configuration
   kubectl apply -f audio-config-backup.yaml
   ```

2. **Data Recovery**
   ```bash
   # Restore Redis data
   kubectl cp redis-backup.rdb redis-master-0:/data/dump.rdb
   kubectl exec -it redis-master-0 -- redis-cli SHUTDOWN SAVE
   kubectl delete pod redis-master-0
   ```

3. **Verify Recovery**
   ```bash
   # Run health checks
   ./scripts/verify-audio-service.sh
   
   # Test each provider
   ./scripts/test-audio-providers.sh
   ```

### RTO/RPO Targets
- **RTO (Recovery Time Objective)**: 15 minutes
- **RPO (Recovery Point Objective)**: 1 hour

### Regular Drills
- Monthly failover test
- Quarterly full DR drill
- Annual cross-region recovery

## Appendix

### Useful Commands

```bash
# Get pod logs
kubectl logs -f deployment/conduit-audio-service -n production

# Execute commands in pod
kubectl exec -it deployment/conduit-audio-service -n production -- /bin/bash

# Port forward for debugging
kubectl port-forward deployment/conduit-audio-service 8080:80 -n production

# Get service endpoints
kubectl get endpoints conduit-audio-service -n production

# Describe deployment
kubectl describe deployment conduit-audio-service -n production
```

### Support Contacts

- **On-Call Engineer**: +1-xxx-xxx-xxxx
- **Escalation Manager**: escalation@conduit.io
- **Provider Support**:
  - OpenAI: support@openai.com
  - Google Cloud: cloud-support@google.com
  - AWS: https://console.aws.amazon.com/support

### Related Documentation

- [Audio API Documentation](../api/audio-api.md)
- [Provider Integration Guide](../guides/audio-providers.md)
- [Security Best Practices](../security/audio-security.md)
- [Capacity Planning Guide](../planning/audio-capacity.md)