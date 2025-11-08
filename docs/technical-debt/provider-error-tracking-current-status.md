# Provider Error Tracking - Current Status

**Last Updated:** 2025-01-07
**Feature:** Automatic Provider Error Tracking and Disabling
**Status:** Production-ready with known limitations
**Overall Risk Level:** MEDIUM-HIGH

## Executive Summary

The provider error tracking system automatically disables failing API keys and providers based on configurable error thresholds. While functional and improved from initial implementation, the system lacks automatic recovery mechanisms and circuit breaker patterns that would prevent permanent service degradation.

## System Overview

### Current Implementation

**Location:** `ConduitLLM.Core/Services/ProviderErrorTrackingService.cs`

**How It Works:**
1. LLM communication exceptions are tracked in Redis via `RedisErrorStore`
2. Fatal errors (InvalidApiKey, InsufficientBalance, AccessForbidden) trigger provider/key disabling based on configurable thresholds
3. Warning errors (RateLimitExceeded, ServiceUnavailable) are logged but don't disable providers
4. Primary key failures disable the entire provider; non-primary key failures only disable the specific key
5. Manual re-enabling available through Admin API endpoints

### Error Classification

**Fatal Errors** (can trigger automatic disabling):
- `InvalidApiKey` (401) - Disable immediately on first occurrence
- `InsufficientBalance` (402) - Disable after 2 occurrences within 5 minutes
- `AccessForbidden` (403) - Disable after 3 occurrences within 10 minutes

**Warning Errors** (logged only):
- `RateLimitExceeded` (429)
- `ServiceUnavailable` (503)

### Error Threshold Configuration

**Location:** `ConduitLLM.Core/Models/ProviderErrorModels.cs`

```csharp
public static class ErrorThresholdConfiguration
{
    public static readonly Dictionary<ProviderErrorType, DisablePolicy> FatalErrorPolicies
}
```

Each error type has a `DisablePolicy` with:
- `DisableImmediately` - Whether to disable on first occurrence
- `RequiredOccurrences` - Number of errors needed to trigger disabling
- `TimeWindow` - Time period for counting occurrences
- `RequiresManualReenable` - Whether automatic recovery is allowed

## Improvements Since Original Risk Assessment

### ✅ Implemented Features

1. **Manual Recovery Endpoints** (`ProviderErrorsController.cs`)
   - `POST /api/provider-errors/keys/{keyId}/clear` - Clear errors and optionally re-enable key
   - `POST /api/provider-errors/providers/{providerId}/clear` - Clear errors and optionally re-enable provider
   - Publishes events: `ProviderKeyReenabledEvent`, `ProviderReenabledEvent`

2. **Comprehensive Error Tracking**
   - Redis-based error storage with detailed history
   - Error count tracking with time windows
   - Recent error retrieval for debugging

3. **Monitoring Endpoints**
   - `GET /api/provider-errors/stats` - System-wide error statistics
   - `GET /api/provider-errors/recent` - Recent errors across all providers
   - `GET /api/provider-errors/keys/{keyId}` - Key-specific error details
   - `GET /api/provider-errors/providers/{providerId}` - Provider-specific error details

4. **Robust Exception Handling**
   - Centralized exception handling in `ExceptionHandler.cs`
   - StatusCode preservation across all code paths
   - Proper exception classification for tracking

5. **Clean Streaming Error Handling**
   - Refactored to use standard `IAsyncEnumerable` patterns in `StreamHelper.cs`
   - Proper exception propagation without manual enumerator complexity

## Critical Limitations (HIGH PRIORITY)

### 1. No Automatic Recovery Mechanism
**Risk Level:** HIGH

**Problem:**
- Once disabled, providers remain disabled indefinitely until manual intervention
- Temporary API outages become permanent service failures
- Renewed/fixed API keys don't automatically re-enable providers
- If all providers become disabled, the system is completely down until operator intervention

**Impact:**
- Requires active monitoring and manual remediation
- No self-healing capability
- Temporary provider issues cause permanent service degradation

**Potential Solutions:**
- Implement health check service with exponential backoff
- Time-based re-enable attempts (e.g., retry after 5min, 15min, 1hr)
- Automatic re-enable when errors are manually cleared
- Configurable recovery policies per error type

### 2. No Circuit Breaker Pattern
**Risk Level:** HIGH

**Problem:**
- Binary on/off state only - no gradual degradation
- No "half-open" state for testing recovery
- Re-enabling sends full traffic immediately, risking cascading failures
- No partial traffic routing during recovery testing

**Current Code:**
```csharp
// ProviderErrorTrackingService.cs, lines 131-135
if (provider != null && provider.IsEnabled)
{
    provider.IsEnabled = false;  // Binary state only
    await providerRepo.UpdateAsync(provider);
}
```

**Impact:**
- No graceful degradation during provider issues
- Abrupt state transitions can cause traffic spikes
- Can't safely test provider recovery under load

**Potential Solutions:**
- Implement circuit breaker using Polly library
- Add states: Closed (normal), Open (disabled), Half-Open (testing)
- Configure failure thresholds and timeout periods
- Gradual traffic ramping on recovery

**Note:** Circuit breaker implementations already exist for other systems:
- `RedisWebhookCircuitBreaker.cs` - For webhook delivery
- `RedisCircuitBreaker.cs` - For Redis availability
- These patterns could be adapted for provider error tracking

### 3. No Fallback Storage for Error Tracking
**Risk Level:** MEDIUM

**Problem:**
- If Redis is unavailable, error tracking fails silently
- No local file fallback or queue mechanism
- Lost visibility during infrastructure issues
- Could miss critical error patterns

**Current Code:**
```csharp
// ProviderErrorTrackingService.cs, lines 64-68
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to track provider error for key {KeyId}", error.KeyCredentialId);
    // Don't throw - error tracking should not break the main flow
    // But no fallback storage mechanism
}
```

**Impact:**
- Error tracking data loss during Redis outages
- Can't retrospectively analyze errors during incidents
- No guarantee of error tracking reliability

**Potential Solutions:**
- Implement local file-based fallback storage
- Queue errors for later processing when Redis recovers
- Add health check endpoint for error tracking system availability
- Consider dual-write to Redis + persistent storage

## Medium Priority Limitations

### 4. Race Conditions in Provider State Changes
**Risk Level:** LOW-MEDIUM

**Problem:**
- Provider disabling operations are not atomic
- Concurrent requests could cause duplicate operations
- State consistency not guaranteed under high concurrency

**Practical Impact:**
- Low probability in normal scenarios
- Both racing operations want same outcome (disable), minimizing harm
- Logs would detect inconsistencies

**Potential Solutions:**
- Distributed locking using Redis
- Database-level optimistic concurrency control
- Idempotent update operations with version checking

### 5. Missing Observability Infrastructure
**Risk Level:** MEDIUM

**What Exists:**
- Error tracking APIs for querying current state
- Statistics endpoints for aggregated metrics
- Recent errors endpoint for debugging

**What's Missing:**
- Real-time alerting when providers auto-disable
- Critical alert for "all providers disabled" condition
- Dashboard UI for monitoring provider health
- Time-series metrics for historical analysis
- Circuit breaker state visualization
- Prometheus/Grafana integration

**Impact:**
- Operators must actively poll APIs to detect issues
- No proactive notification of service degradation
- Difficult to spot trends or patterns
- Delayed response to critical failures

## Performance Characteristics

### Measured Impact
- Error tracking decorator adds ~1-5ms latency per request
- Exception processing only occurs on actual errors
- Redis operations are async and non-blocking
- No measurable impact on happy path performance

### Optimization Opportunities
- Batch Redis operations for high-error scenarios
- Cache provider enabled/disabled status locally with TTL
- Async fire-and-forget for non-critical error tracking operations

## Recommended Implementation Phases

### Phase 1: Critical Safety (Priority: HIGH)
**Estimated Effort:** 1-2 weeks

1. **Automatic Recovery Service**
   - Health check based recovery with exponential backoff
   - Configurable recovery policies per error type
   - Event publishing on successful recovery

2. **Basic Circuit Breaker**
   - Integrate Polly for circuit breaker pattern
   - Three states: Closed, Open, Half-Open
   - Configurable thresholds and timeouts

3. **Critical Alerting**
   - Alert when any provider is auto-disabled
   - Critical alert when all providers are disabled
   - Alert on high error rates crossing thresholds

### Phase 2: Production Hardening (Priority: MEDIUM)
**Estimated Effort:** 2-3 weeks

1. **Distributed Locking**
   - Redis-based locks for state changes
   - Prevent race conditions in provider disabling

2. **Fallback Storage**
   - Local file-based error tracking fallback
   - Queue for delayed Redis writes

3. **Observability Dashboard**
   - Real-time provider health status UI
   - Historical error pattern visualization
   - Circuit breaker state monitoring

### Phase 3: Advanced Features (Priority: LOW)
**Estimated Effort:** 3-4 weeks

1. **Intelligent Recovery**
   - Machine learning-based failure prediction
   - Adaptive recovery timing based on error patterns
   - Provider reputation scoring

2. **Automatic Key Rotation**
   - Support for rotating API keys without manual intervention
   - Graceful key deprecation with traffic shifting

## Testing Recommendations

### Scenarios to Test
1. **Provider Recovery:**
   - Provider disabled by errors, API key fixed externally, verify recovery
   - Multiple providers disabled simultaneously
   - All providers disabled (disaster recovery)

2. **High Error Rates:**
   - Sustained error rates from single provider
   - Error bursts across multiple providers
   - Redis unavailability during high error rates

3. **Concurrency:**
   - Concurrent requests causing same provider to be disabled
   - Race conditions in error tracking and state changes

4. **Load Testing:**
   - Behavior under high request volume with errors
   - Circuit breaker state transitions under load
   - Recovery performance when re-enabling providers

## Related Documentation

- **Architecture:**
  - [Provider Multi-Instance Architecture](/docs/architecture/provider-multi-instance.md)

- **Historical Context:**
  - [Original Risk Assessment](/docs/archive/obsolete-technical-debt/provider-error-tracking-risks.md) (archived)

- **Configuration:**
  - Error threshold configuration: `ConduitLLM.Core/Models/ProviderErrorModels.cs`
  - Provider error tracking: `ConduitLLM.Core/Services/ProviderErrorTrackingService.cs`

## Decision Log

| Date | Decision | Rationale | Status |
|------|----------|-----------|--------|
| TBD | Automatic Recovery Strategy | TBD | Pending |
| TBD | Circuit Breaker Implementation | TBD | Pending |
| TBD | Monitoring/Alerting Approach | TBD | Pending |
| TBD | Redis Fallback Mechanism | TBD | Pending |

---

**Note:** This document should be reviewed quarterly and updated as improvements are implemented and new issues are discovered.
