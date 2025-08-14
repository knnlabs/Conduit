# SignalR Hubs and Event Handlers Error Handling Analysis

## Executive Summary

After analyzing the SignalR hubs and event handlers in ConduitLLM, I've identified several areas where error boundaries and fallback mechanisms could be improved. While the codebase has good foundational error handling, there are opportunities to enhance resilience, particularly in SignalR hubs that lack proper try-catch blocks in critical methods and event handlers that could benefit from better fallback mechanisms.

## Key Findings

### 1. SignalR Hubs - Missing Error Boundaries

#### TaskHub.cs Issues:
- **SubscribeToTask method** (lines 36-59): No try-catch wrapper around `_taskService.GetTaskStatusAsync()`
- **ITaskHub implementation methods** (lines 95-142): No error handling in notification methods
- **Risk**: Unhandled exceptions could crash SignalR connections

#### ImageGenerationHub.cs & VideoGenerationHub.cs:
- **CanAccessTaskAsync calls** (lines 35, 35): No try-catch wrapper
- **Risk**: Authentication failures could terminate connections unexpectedly

#### WebhookDeliveryHub.cs:
- **GetWebhookGroupName method** (line 234): Uses `new Uri()` without try-catch
- **Risk**: Invalid webhook URLs could throw `UriFormatException`

### 2. Event Handlers - Limited Fallback Mechanisms

#### SpendUpdateProcessor.cs:
- **Good**: Has basic error handling and re-throws for MassTransit retry
- **Missing**: No circuit breaker or fallback when database is unavailable
- **Missing**: No handling for concurrent update conflicts

#### ModelCapabilitiesDiscoveredHandler.cs:
- **Good**: Has try-catch with re-throw for MassTransit retry
- **Missing**: No fallback when cache operations fail
- **Missing**: No resilience for SignalR notifications (commented out)

#### ProviderCredentialEventHandler.cs:
- **Good**: Graceful degradation when capability discovery fails (line 72-78)
- **Missing**: No circuit breaker for repeated provider discovery failures
- **Missing**: No exponential backoff for transient failures

### 3. Services Interacting with External Systems

#### WebhookDeliveryService.cs:
- **Issue**: Stub implementation with no actual error handling
- **Missing**: No retry policies, circuit breakers, or timeout handling

#### WebhookDeliveryConsumer.cs:
- **Good**: Has circuit breaker integration
- **Good**: Implements retry with exponential backoff
- **Missing**: No fallback for deserialization failures (lines 107-108, 119-120)
- **Missing**: No handling for poison messages

### 4. Existing Resilience Patterns

#### Positive Findings:
- **ResiliencePolicies.cs**: Well-implemented Polly policies with retry and timeout
- **WebhookCircuitBreaker.cs**: Good circuit breaker implementation for webhooks
- **BaseHub.cs & SecureHub.cs**: Good foundation with connection lifecycle handling

#### Gaps:
- Polly policies not consistently applied across all external service calls
- No circuit breakers for database operations
- Limited use of fallback patterns in SignalR hubs

## Recommended Improvements

### 1. SignalR Hub Error Boundaries

Add comprehensive error handling wrapper for all hub methods:

```csharp
// Example for TaskHub.SubscribeToTask
public async Task SubscribeToTask(string taskId)
{
    try
    {
        var virtualKeyId = RequireVirtualKeyId();
        
        // Add resilience for external service calls
        var taskStatus = await Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            .ExecuteAsync(async () => await _taskService.GetTaskStatusAsync(taskId));
            
        // ... rest of method
    }
    catch (HubException)
    {
        throw; // Re-throw hub-specific exceptions
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error subscribing to task {TaskId}", taskId);
        
        // Send error notification to client
        await Clients.Caller.SendAsync("SubscriptionError", new 
        { 
            taskId, 
            error = "Unable to subscribe to task updates. Please try again." 
        });
        
        // Don't crash the connection
        return;
    }
}
```

### 2. Event Handler Resilience

Implement circuit breakers and fallbacks for critical operations:

```csharp
// Example for SpendUpdateProcessor
public async Task Consume(ConsumeContext<SpendUpdateRequested> context)
{
    try
    {
        // Add circuit breaker for database operations
        await _databaseCircuitBreaker.ExecuteAsync(async () =>
        {
            // Existing spend update logic
        });
    }
    catch (CircuitBreakerOpenException)
    {
        // Queue for later processing when circuit closes
        await _fallbackQueue.EnqueueAsync(context.Message);
        
        // Don't fail the message - acknowledge it
        return;
    }
}
```

### 3. Fallback Mechanisms

Add fallback strategies for non-critical operations:

```csharp
// Example for ModelCapabilitiesDiscoveredHandler
public async Task Consume(ConsumeContext<ModelCapabilitiesDiscovered> context)
{
    try
    {
        // Primary cache update
        _cache.Set(cacheKey, message.ModelCapabilities, TimeSpan.FromHours(24));
    }
    catch (Exception cacheEx)
    {
        // Fallback to secondary cache or in-memory store
        _logger.LogWarning(cacheEx, "Primary cache failed, using fallback");
        _fallbackCache.Set(cacheKey, message.ModelCapabilities);
    }
    
    // Continue with SignalR notifications even if cache fails
}
```

### 4. Webhook Delivery Enhancements

Improve error handling for webhook payload processing:

```csharp
// WebhookDeliveryConsumer enhancement
object payload;
try
{
    payload = JsonSerializer.Deserialize<object>(request.PayloadJson);
}
catch (JsonException ex)
{
    _logger.LogError(ex, "Failed to deserialize webhook payload for task {TaskId}", request.TaskId);
    
    // Create error payload instead of crashing
    payload = new 
    { 
        error = "Payload deserialization failed",
        originalPayload = request.PayloadJson,
        taskId = request.TaskId
    };
    
    // Continue with delivery using error payload
}
```

### 5. Global SignalR Error Handling

Implement a global error filter for all SignalR hubs:

```csharp
public class SignalRErrorFilter : IHubFilter
{
    private readonly ILogger<SignalRErrorFilter> _logger;
    
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, 
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hub method {Method} failed", invocationContext.HubMethodName);
            
            // Send error to client
            await invocationContext.Hub.Clients.Caller.SendAsync("Error", new 
            {
                method = invocationContext.HubMethodName,
                error = "An error occurred processing your request"
            });
            
            // Return null instead of throwing
            return null;
        }
    }
}
```

## Priority Recommendations

1. **High Priority**: Add try-catch blocks to all SignalR hub methods that interact with external services
2. **High Priority**: Implement circuit breakers for database operations in event handlers
3. **Medium Priority**: Add fallback mechanisms for cache operations
4. **Medium Priority**: Improve webhook payload deserialization error handling
5. **Low Priority**: Implement global SignalR error filters

## Conclusion

While Conduit has good foundational error handling with MassTransit retry logic and some circuit breakers, there are opportunities to improve resilience, particularly in SignalR hubs and event handlers. Implementing these recommendations will make the system more robust and provide better user experience during partial failures.