# Multi-Key Provider Rate Limiting Proposal

## Overview

This proposal outlines a system for supporting multiple API keys per LLM provider in Conduit, with intelligent key selection based on rate limit data. The goal is to maximize throughput and reliability by automatically switching between keys when approaching rate limits.

## Current State

### Rate Limit Header Support by Provider

- **OpenAI** ✅ - Provides comprehensive headers (x-ratelimit-*)
- **Azure OpenAI** ✅ - Similar headers with different time windows
- **Anthropic (Claude)** ✅ - Custom headers (anthropic-ratelimit-*)
- **AWS Bedrock (Claude)** ✅ - Uses Anthropic's headers
- **Google (Gemini/Vertex AI)** ❌ - No rate limit headers
- **Other Providers** ❌ - Most don't provide headers

### Existing Infrastructure

Conduit already has:
1. **Redis for Caching** - IDistributedCache with StackExchange.Redis
2. **MassTransit for Events** - Event-driven architecture with ordered processing
3. **Distributed Locks** - RedisDistributedLockService for coordination

## Proposed Architecture

### 1. Shared State Storage in Redis

Store rate limit metadata per provider key:
```
Key: "provider:ratelimit:{provider}:{keyHash}"
Value: {
  "requestsRemaining": 4500,
  "tokensRemaining": 89000,
  "requestsResetAt": "2025-01-08T10:45:00Z",
  "tokensResetAt": "2025-01-08T10:45:00Z",
  "lastUpdated": "2025-01-08T10:44:32Z"
}
TTL: Match the reset time
```

### 2. Key Selection Strategy

Use Redis sorted sets for key ranking:
```
Key: "provider:keys:available:{provider}"
Score: tokensRemaining (or custom score)
Member: keyHash
```

This allows atomic operations like `ZPOPMAX` to get the best available key.

### 3. Event-Driven Updates

New events for rate limit coordination:
- `RateLimitApproaching` - When a key hits 80% usage
- `RateLimitExceeded` - When a key gets 429
- `RateLimitReset` - When limits reset

### 4. Performance-Optimized Implementation

**Don't block the request path:**

```csharp
// FAST PATH - Select key and proceed immediately
var selectedKey = await _keySelector.SelectKeyAsync(provider); // <5ms

// Make the API request immediately
var response = await provider.SendRequestAsync(selectedKey, request);

// ASYNC PATH - Update usage after response
_ = Task.Run(async () => 
{
    // Parse rate limit headers from response
    var rateLimitInfo = ParseRateLimitHeaders(response.Headers);
    
    // Update Redis with actual usage (fire-and-forget)
    await _rateLimitTracker.UpdateUsageAsync(selectedKey, rateLimitInfo);
});
```

### 5. Key Selection Algorithm (Fast)

```csharp
public async Task<string> SelectKeyAsync(string provider)
{
    // Try Redis sorted set first (very fast - single command)
    var bestKey = await _redis.SortedSetPopAsync($"provider:keys:available:{provider}", Order.Descending);
    if (bestKey.HasValue)
        return bestKey.Value.Element;
    
    // Fallback to round-robin (no Redis needed)
    return _keyRotator.GetNextKey(provider);
}
```

### 6. Performance Summary

- **Added latency**: ~2-5ms (single Redis read)
- **No token counting** in request path
- **No distributed locks** for normal operations
- **Async updates** don't block responses

## Critical Questions to Address

### Architecture & Design

1. **Data Model**
   - How do we store multiple keys per provider? New table `ProviderKeys` with many-to-one relationship?
   - Should virtual keys be able to specify which provider keys they can use?
   - How do we handle key rotation/updates without breaking existing requests?

2. **Failure Scenarios**
   - What happens if all keys for a provider are rate-limited?
   - How do we handle Redis downtime? (fallback to round-robin?)
   - What if a key gets revoked mid-flight?
   - How do we prevent thundering herd when a popular key resets?

3. **Business Logic**
   - Should we support priority tiers? (premium keys for premium virtual keys?)
   - How do we audit which provider key was used for each request?
   - Do we need usage analytics per provider key?
   - Should keys have cost implications? (some OpenAI keys might have different pricing)

### Technical Implementation

4. **State Consistency**
   - How quickly do we propagate 429 errors across nodes?
   - Should we pre-warm the cache with rate limit data?
   - How do we handle clock skew between nodes and providers?

5. **Performance Edge Cases**
   - What's the behavior under extreme load (1000+ requests/second)?
   - How do we prevent Redis from becoming a bottleneck?
   - Should we batch rate limit updates?

6. **Provider-Specific Concerns**
   - How do we handle providers without rate limit headers?
   - What about providers with different rate limit windows (10s vs 60s)?
   - How do we map different header formats to a common model?

7. **Migration & Rollout**
   - How do we migrate existing single-key providers?
   - Can we feature flag this per provider?
   - What's the rollback plan?

### Key Validation Questions

**Most Important:**
1. **"What's the worst-case latency impact?"** - Ensure we don't slow down the hot path
2. **"How do we test this in a distributed environment?"** - Multi-node testing strategy
3. **"What metrics do we need?"** - Key utilization, 429 rates, failover frequency
4. **"How does this interact with existing virtual key rate limits?"** - Ensure compatibility

## Next Steps

1. Validate the data model design
2. Prototype the Redis-based key selection
3. Design the migration strategy
4. Create detailed implementation plan
5. Define success metrics and monitoring

## Open Questions

- Should we implement predictive rate limiting or just reactive?
- How do we handle providers that might add rate limit headers in the future?
- What's the appropriate level of configuration granularity?
- Should this be an enterprise-only feature?

---

*Status: Draft Proposal - Requires further discussion and refinement*