# Redis Resilience Improvements Proposal

## Executive Summary

This proposal outlines simple, pragmatic improvements to enhance Conduit's already robust Redis failure handling. While the current implementation gracefully handles Redis outages, implementing a circuit breaker pattern will reduce unnecessary connection attempts and improve system efficiency during Redis downtime.

## Current State

Conduit's Redis implementation demonstrates excellent resilience:

- ✅ **Automatic fallback**: When Redis is unavailable, the system automatically falls back to in-memory caching
- ✅ **Non-blocking failures**: All Redis operations are wrapped in try-catch blocks, preventing cascading failures
- ✅ **Service continuity**: LLM requests continue to work even without caching
- ✅ **Graceful degradation**: Only performance is impacted, not functionality

### Key Implementation Details

1. **Connection Configuration**: `AbortOnConnectFail = false` ensures the application doesn't crash
2. **Service Factory Fallback**: Automatically creates memory cache service if Redis connection fails
3. **Operation-Level Safety**: Each cache operation returns default values on failure rather than throwing exceptions
4. **Caching Layer Resilience**: Falls back to direct LLM calls if cache operations fail

## Problem Statement

While the current implementation handles Redis failures well, there are efficiency concerns:

1. **Repeated Connection Attempts**: During extended Redis outages, the system repeatedly attempts connections
2. **Resource Consumption**: Failed connection attempts consume CPU and network resources
3. **Log Noise**: Continuous error logging during outages can obscure other important logs
4. **Latency Impact**: Each failed Redis operation adds latency before falling back

## Proposed Solution: Circuit Breaker Pattern

Implement a circuit breaker to intelligently manage Redis connection attempts:

### How It Works

1. **Closed State**: Normal operation, all requests go to Redis
2. **Open State**: After threshold failures, stop attempting Redis operations
3. **Half-Open State**: Periodically test if Redis has recovered

### Benefits

- **Reduced Resource Usage**: Prevents unnecessary connection attempts during outages
- **Improved Performance**: Faster fallback to memory cache when Redis is down
- **Cleaner Logs**: Reduces repetitive error logging
- **Smart Recovery**: Automatically detects when Redis is available again

### Implementation Approach

```csharp
public class RedisCacheService : ICacheService
{
    private readonly ICircuitBreaker _circuitBreaker;
    
    public RedisCacheService(/* existing parameters */)
    {
        _circuitBreaker = new CircuitBreaker(
            failureThreshold: 5,
            resetTimeout: TimeSpan.FromMinutes(1)
        );
    }
    
    public async Task<T?> GetAsync<T>(string key)
    {
        if (_circuitBreaker.State == CircuitState.Open)
        {
            return default; // Fast fail
        }
        
        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                // Existing Redis operation
            });
        }
        catch
        {
            // Circuit breaker handles state transitions
            return default;
        }
    }
}
```

## Implementation Plan

### Phase 1: Circuit Breaker (2-3 days)
1. Add Polly NuGet package for circuit breaker implementation
2. Wrap Redis operations in circuit breaker
3. Add configuration for circuit breaker thresholds
4. Update unit tests

### Phase 2: Monitoring (1 day)
1. Add metrics for circuit breaker state changes
2. Log circuit breaker open/close events
3. Add dashboard visibility for Redis health

### Phase 3: Testing (1 day)
1. Test Redis failure scenarios
2. Verify automatic recovery
3. Performance testing under failure conditions

## Alternative Options Considered

1. **Hybrid Cache Strategy**: Always write to memory first, then Redis
   - Pros: Guaranteed memory cache hit
   - Cons: Increased memory usage, complexity

2. **Health-Based Routing**: Periodic health checks to determine cache service
   - Pros: Proactive detection
   - Cons: Additional health check overhead

3. **Metrics Only**: Just add failure tracking
   - Pros: Minimal change
   - Cons: Doesn't solve the efficiency problem

## Cost-Benefit Analysis

### Benefits
- **Reduced AWS costs**: Fewer failed connection attempts = less network traffic
- **Improved performance**: ~50ms faster fallback during outages
- **Better observability**: Clear indication of Redis health status
- **Reduced operational noise**: Cleaner logs during incidents

### Costs
- **Development time**: ~5 days total
- **Additional dependency**: Polly library (well-maintained, Microsoft-recommended)
- **Minimal complexity**: Circuit breaker is a well-understood pattern

## Recommendation

Implement the circuit breaker pattern as the primary improvement. This solution:
- Is simple and well-tested
- Requires minimal code changes
- Provides immediate benefits
- Maintains the existing resilience while improving efficiency

The current Redis implementation is already production-ready and handles failures gracefully. This improvement is about optimization rather than fixing a critical issue.

## Success Metrics

1. **Reduced error logs**: 90% reduction in Redis connection errors during outages
2. **Faster fallback**: <10ms decision time when Redis is unavailable
3. **Automatic recovery**: Redis operations resume within 1 minute of service restoration
4. **No functional impact**: Zero LLM request failures due to Redis issues

## Conclusion

While Conduit's current Redis implementation is robust and production-ready, adding a circuit breaker pattern will improve efficiency during Redis outages. This simple enhancement maintains all existing resilience while reducing unnecessary resource consumption and improving observability.

The implementation is straightforward, low-risk, and provides clear operational benefits without adding significant complexity to the codebase.