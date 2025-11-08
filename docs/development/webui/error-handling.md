# Error Boundaries and Fallback Mechanisms

This document outlines the error handling strategies, resilience patterns, and fallback mechanisms implemented in Conduit.

## Overview

Conduit implements multiple layers of error handling to ensure system resilience:

1. **SignalR Error Boundaries** - Global error handling for all hub methods
2. **Resilient Event Handlers** - Base class with circuit breakers and retries
3. **Fallback Mechanisms** - Graceful degradation when primary operations fail
4. **Service-Level Circuit Breakers** - Protection for external service calls

## SignalR Error Handling

### Global Error Filter

The `SignalRErrorHandlingFilter` provides consistent error handling across all hubs:

```csharp
[VirtualKeyHubAuthorization]
public class MyHub : SecureHub
{
    // All methods automatically protected by error filter
}
```

Features:
- Catches and logs all exceptions
- Converts exceptions to client-friendly HubExceptions
- Records metrics for monitoring
- Handles timeouts and cancellations gracefully

### Hub Method Error Boundaries

Individual hub methods should implement local error handling for specific scenarios:

```csharp
public async Task SubscribeToTask(string taskId)
{
    // Input validation
    if (string.IsNullOrWhiteSpace(taskId))
    {
        throw new HubException("Invalid task ID");
    }
    
    try
    {
        // Protected operation
        var taskStatus = await _taskService.GetTaskStatusAsync(taskId);
        // ... rest of method
    }
    catch (HubException)
    {
        throw; // Client-intended errors pass through
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error in hub method");
        throw new HubException("Operation failed. Please try again.");
    }
}
```

## Resilient Event Handlers

### Base Class Pattern

The `ResilientEventHandlerBase<TEvent>` provides:

1. **Retry Policy** - Automatic retries for transient failures
2. **Circuit Breaker** - Prevents cascading failures
3. **Timeout Protection** - Prevents hanging operations
4. **Fallback Mechanism** - Alternative processing when primary fails

### Implementation Example

```csharp
public class MyEventHandler : ResilientEventHandlerBase<MyEvent>
{
    protected override async Task HandleEventAsync(MyEvent message, CancellationToken ct)
    {
        // Primary processing logic
        await _service.ProcessAsync(message);
    }
    
    protected override async Task HandleEventFallbackAsync(MyEvent message, CancellationToken ct)
    {
        // Fallback when circuit is open or primary fails
        await _cache.StoreForLaterAsync(message);
    }
    
    // Customize resilience settings
    protected override int GetCircuitBreakerThreshold() => 10;
    protected override TimeSpan GetTimeout() => TimeSpan.FromSeconds(15);
}
```

## Fallback Mechanisms

### 1. Spend Update Fallback

When database updates fail, spend updates fallback to:
1. **Cache Storage** - Store in Redis for later processing
2. **File Logging** - Write to disk as last resort
3. **Eventual Consistency** - Background job processes pending updates

### 2. Cache Operation Fallback

When cache operations fail:
1. **Direct Database Access** - Bypass cache temporarily
2. **In-Memory Cache** - Use local cache for critical data
3. **Graceful Degradation** - Continue with reduced performance

### 3. External Service Fallback

When external services fail:
1. **Default Values** - Use sensible defaults
2. **Cached Results** - Use last known good values
3. **Queued Processing** - Store for retry when service recovers

## Circuit Breaker Patterns

### Configuration

Circuit breakers are configured per service/operation:

```csharp
// Webhook delivery circuit breaker
services.AddSingleton<IWebhookCircuitBreaker>(sp =>
    new WebhookCircuitBreaker(
        cache, 
        logger, 
        failureThreshold: 5,
        openDuration: TimeSpan.FromMinutes(5),
        counterResetDuration: TimeSpan.FromMinutes(15)
    ));
```

### States

1. **Closed** - Normal operation, requests pass through
2. **Open** - Failures exceeded threshold, requests fail fast
3. **Half-Open** - Testing if service recovered

## Monitoring and Alerting

### Metrics

Error boundaries record metrics for monitoring:
- Method invocation success/failure rates
- Circuit breaker state changes
- Fallback execution counts
- Error types and frequencies

### Logging

Structured logging captures:
- Error context (virtual key, operation, timing)
- Error IDs for support correlation
- Circuit breaker state transitions
- Fallback activation reasons

## Best Practices

1. **Layer Error Handling**
   - Global filters for cross-cutting concerns
   - Local try-catch for specific scenarios
   - Fallback mechanisms for critical operations

2. **Fail Fast**
   - Validate inputs early
   - Use circuit breakers to prevent cascading failures
   - Set appropriate timeouts

3. **Graceful Degradation**
   - Always have a fallback plan
   - Log but don't crash on non-critical failures
   - Maintain partial functionality when possible

4. **Clear Error Messages**
   - Provide actionable error messages to clients
   - Include error IDs for support
   - Log detailed errors server-side

5. **Test Resilience**
   - Test circuit breaker behavior
   - Verify fallback mechanisms work
   - Simulate failures in staging

## Implementation Checklist

When adding new features:

- [ ] Inherit from `SecureHub` for SignalR hubs
- [ ] Use `ResilientEventHandlerBase` for event handlers
- [ ] Implement fallback for critical operations
- [ ] Add circuit breakers for external service calls
- [ ] Configure appropriate timeouts
- [ ] Add metrics and logging
- [ ] Test error scenarios
- [ ] Document fallback behavior