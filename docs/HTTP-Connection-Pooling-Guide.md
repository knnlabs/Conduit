# HTTP Connection Pooling Configuration Guide

## Overview

This guide covers the optimized HTTP client connection pooling configuration implemented in Conduit to support 1,000+ webhook deliveries per minute without connection exhaustion.

## Production Requirements

### Target Performance
- **1,000 webhook deliveries per minute** (16.7 requests/second sustained)
- **100 webhooks/second burst** capacity
- **<5 second latency at p95**
- **Zero connection leaks** under sustained load

### Resource Calculations

#### Connection Pool Sizing
```
Base calculation:
- 100 requests/second burst = 100 concurrent connections needed
- 50% buffer for safety = 150 connections
- Divided by 3-5 instances = 30-50 connections per instance

Selected: 50 connections per server per instance
```

#### Memory Impact
```
Per connection: ~4KB TCP buffer + ~2KB HTTP state = ~6KB
50 connections × 6KB = 300KB per endpoint
With 10 webhook endpoints = 3MB total connection memory
```

## Implementation Details

### HTTP Client Configuration

```csharp
builder.Services.AddHttpClient<IWebhookNotificationService, WebhookNotificationService>(
    "WebhookClient", 
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);  // Reduced from 30s
        client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM/1.0");
        client.DefaultRequestHeaders.ConnectionClose = false;  // Enable keep-alive
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // Connection Pool Settings
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),     // Max connection age
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),  // Idle timeout
        MaxConnectionsPerServer = 50,                           // Connection limit
        
        // HTTP/2 Support
        EnableMultipleHttp2Connections = true,                  
        KeepAlivePingTimeout = TimeSpan.FromSeconds(20),       
        KeepAlivePingDelay = TimeSpan.FromSeconds(30),         
        
        // Performance Settings
        MaxResponseHeadersLength = 64 * 1024,                   // 64KB headers
        ResponseDrainTimeout = TimeSpan.FromSeconds(5),         
        ConnectTimeout = TimeSpan.FromSeconds(5)                
    })
    .AddPolicyHandler(GetWebhookRetryPolicy())
    .AddPolicyHandler(GetWebhookCircuitBreakerPolicy())
    .AddHttpMessageHandler<WebhookMetricsHandler>();
```

### Polly Resilience Policies

#### Retry Policy
- **Retry Count**: 3 attempts
- **Backoff**: Exponential (2s, 4s, 8s)
- **Handled**: Transient errors and 5xx responses
- **Excluded**: 4xx client errors

#### Circuit Breaker
- **Failure Threshold**: 5 consecutive failures
- **Break Duration**: 1 minute
- **Purpose**: Prevent cascade failures to unhealthy endpoints

### Enhanced Webhook Service Features

1. **Custom Timeout Support**
   ```csharp
   await webhookService.SendWebhookAsync(
       url, 
       payload, 
       headers, 
       customTimeout: TimeSpan.FromSeconds(5)
   );
   ```

2. **Standard Headers**
   - `X-Webhook-Type`: Identifies webhook type
   - `X-Webhook-Timestamp`: Unix timestamp for deduplication

3. **Improved Error Handling**
   - Differentiated timeout vs cancellation
   - HTTP error details in logs
   - Connection failure tracking

## Monitoring and Health Checks

### Metrics Collected

#### WebhookMetricsHandler
- `conduit_webhook_requests_total`: Total requests by status and endpoint
- `conduit_webhook_timeouts_total`: Timeout count by endpoint
- `conduit_webhook_duration_ms`: Request duration histogram
- `conduit_webhook_active_requests`: Current active requests

### Health Check Thresholds

#### HttpConnectionPoolHealthCheck
- **Healthy**: <70% pool utilization
- **Degraded**: 70-90% pool utilization
- **Unhealthy**: >90% pool utilization

```json
{
  "status": "Healthy",
  "description": "Connections: 25/50 (50.0%)",
  "data": {
    "activeConnections": 25,
    "maxConnections": 50,
    "utilization": "50.0%",
    "idleConnections": 25,
    "pendingRequests": 0
  }
}
```

## Performance Testing Results

### Load Test Configuration
- **Duration**: 60 minutes
- **Request Rate**: 1,000 webhooks/minute sustained
- **Burst Tests**: 100 webhooks/second for 10 seconds

### Expected Results
- **Connection Pool Utilization**: ~40-60% under normal load
- **p95 Latency**: <5 seconds
- **Success Rate**: >99.5%
- **Circuit Breaker Trips**: <0.1% of endpoints

## Operational Procedures

### Monitoring Connection Pools

1. **Check Health Endpoint**
   ```bash
   curl http://localhost:5000/health/ready | jq '.checks.http_connection_pool'
   ```

2. **Monitor Prometheus Metrics**
   ```promql
   # Active connections
   conduit_webhook_active_requests
   
   # Connection pool utilization
   conduit_http_pool_active_connections / conduit_http_pool_max_connections
   
   # Webhook success rate
   rate(conduit_webhook_requests_total{status="200"}[5m]) / 
   rate(conduit_webhook_requests_total[5m])
   ```

3. **Alert Thresholds**
   - Connection pool >80% utilized for 5 minutes
   - Webhook timeout rate >5% for 5 minutes
   - Circuit breaker open for >10 minutes

### Troubleshooting

#### High Connection Pool Utilization
1. Check for slow webhook endpoints
2. Review timeout configuration
3. Consider increasing MaxConnectionsPerServer
4. Scale out with more instances

#### Frequent Timeouts
1. Identify problematic endpoints in metrics
2. Review endpoint response times
3. Consider endpoint-specific timeout adjustments
4. Check network connectivity

#### Circuit Breaker Trips
1. Identify failing endpoints
2. Check endpoint health independently
3. Review retry policy effectiveness
4. Consider manual circuit breaker reset

## Configuration Tuning

### Environment Variables
```bash
# Webhook-specific timeouts (future enhancement)
CONDUITLLM__WEBHOOKS__DEFAULT_TIMEOUT=10
CONDUITLLM__WEBHOOKS__MAX_CONNECTIONS_PER_SERVER=50
CONDUITLLM__WEBHOOKS__CONNECTION_LIFETIME_MINUTES=5
CONDUITLLM__WEBHOOKS__IDLE_TIMEOUT_MINUTES=2
```

### Per-Endpoint Configuration (future enhancement)
```json
{
  "webhookEndpoints": {
    "https://fast-endpoint.com": {
      "timeout": 5,
      "maxConnections": 20
    },
    "https://slow-endpoint.com": {
      "timeout": 30,
      "maxConnections": 10
    }
  }
}
```

## Best Practices

1. **Connection Reuse**
   - Keep-alive enabled by default
   - 5-minute connection lifetime balances reuse vs staleness
   - 2-minute idle timeout prevents resource waste

2. **Timeout Strategy**
   - 10-second default balances reliability vs resource usage
   - Custom timeouts for known slow endpoints
   - Circuit breaker prevents repeated timeouts

3. **Monitoring**
   - Track connection pool utilization trends
   - Alert on degraded health before critical
   - Regular review of timeout and error metrics

4. **Scaling**
   - Horizontal scaling preferred over increasing connection limits
   - Monitor per-instance connection usage
   - Consider geographic distribution for global webhooks

## Security Considerations

1. **TLS/SSL**
   - All webhook connections use HTTPS
   - TLS 1.2+ enforced
   - Certificate validation enabled

2. **Headers**
   - User-Agent identifies Conduit
   - Timestamp prevents replay attacks
   - Custom headers support authentication

3. **Retry Safety**
   - Idempotency assumed for webhook delivery
   - Exponential backoff prevents thundering herd
   - Circuit breaker limits retry storms

## Future Enhancements

1. **Dynamic Configuration**
   - Per-endpoint connection limits
   - Runtime adjustment of pool sizes
   - Adaptive timeout based on endpoint performance

2. **Advanced Monitoring**
   - Connection reuse efficiency metrics
   - Geographic latency tracking
   - Endpoint reliability scoring

3. **Enhanced Resilience**
   - Bulkhead isolation per endpoint
   - Priority-based connection allocation
   - Graceful degradation under load