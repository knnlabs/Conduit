# Video Generation Architecture Issues Analysis

## Executive Summary

The video generation implementation in Conduit has significant architectural issues that prevent true asynchronous operation. While the system appears to support async video generation, multiple layers of the stack impose timeouts and synchronous behaviors that will cause failures for operations taking longer than 2-3 minutes.

## Critical Issues Found

### 1. **Synchronous Endpoint with 2-Minute Timeout**
- **Location**: `VideosController.cs` line 26
- **Issue**: The sync endpoint `/v1/videos/generations` has a hard-coded 2-minute timeout
- **Impact**: Any video generation taking longer than 2 minutes will fail with a 408 timeout error
- **Code**:
  ```csharp
  private const int SyncTimeoutSeconds = 120; // 2 minutes timeout for sync requests
  ```

### 2. **Fire-and-Forget Pattern Without Proper Task Management**
- **Location**: `VideoGenerationOrchestrator.cs` lines 237-266
- **Issue**: The orchestrator uses `Task.Run` in a fire-and-forget manner without proper task tracking
- **Impact**: 
  - No way to cancel in-flight video generation
  - Memory leaks from untracked tasks
  - Loss of work on service restart
- **Code**:
  ```csharp
  _ = Task.Run(async () =>
  {
      // Long-running video generation without proper tracking
      response = await task;
  });
  ```

### 3. **HTTP Client Factory Timeout Policies**
- **Location**: `HttpClientExtensions.cs` lines 235-257
- **Issue**: MiniMaxClient is registered with standard timeout policies (100 seconds default)
- **Impact**: Even async video generation will timeout at the HTTP client level
- **Workaround Attempted**: `MiniMaxClient.CreateVideoHttpClient()` creates a new HttpClient to bypass factory policies

### 4. **WebUI Client Timeout Layering**
- **Location**: `HttpClientBuilderExtensions.cs` lines 36-45
- **Issue**: While video endpoints skip timeout policies, the base HttpClient still has timeouts
- **Impact**: Multiple timeout layers can interfere with long-running operations
- **Code**:
  ```csharp
  // Attempts to skip timeout for video endpoints but still subject to base client timeout
  if (requestUri.Contains("/videos/generations", StringComparison.OrdinalIgnoreCase))
  {
      logger.LogWarning("Skipping timeout policy for video generation endpoint");
      return AdminApiResiliencePolicies.GetRetryPolicy(logger, retryCount: 3);
  }
  ```

### 5. **Polling Implementation Issues**
- **Location**: `MiniMaxClient.cs` lines 458-689
- **Issues**:
  - Polls for up to 10 minutes (120 attempts * 5 seconds)
  - No ability to resume polling after client disconnection
  - No persistent task state for recovery
  - Exponential backoff can delay completion detection
- **Impact**: Videos completing after 10 minutes are lost

### 6. **Missing Event-Driven Architecture Integration**
- **Location**: Throughout video generation flow
- **Issues**:
  - Video generation events are published but not properly consumed
  - No persistent task queue for reliable processing
  - Background service only performs cleanup, not actual processing
  - Task state is only in-memory (or Redis cache)
- **Impact**: 
  - Service restarts lose all in-progress video generations
  - No horizontal scaling capability
  - No retry mechanism for failed generations

### 7. **Async Task Service Limitations**
- **Location**: `AsyncTaskService.cs` (inferred from usage)
- **Issues**:
  - Tasks are stored in cache with TTL
  - No persistent backing store
  - No work queue integration
  - Status updates are fire-and-forget
- **Impact**: Task state can be lost, no durability guarantees

### 8. **WebUI HttpClient Configuration**
- **Location**: `Program.cs` lines 238-254
- **Issue**: Base HttpClient timeout of 1 hour might not be sufficient for very long videos
- **Code**:
  ```csharp
  .ConfigureHttpClient(client =>
  {
      // Set a very long timeout at the HttpClient level for video generation
      client.Timeout = TimeSpan.FromHours(1);
  });
  ```

### 9. **No Proper Cancellation Token Propagation**
- **Location**: `VideoGenerationOrchestrator.cs` line 244
- **Issue**: Uses `CancellationToken.None` instead of propagating cancellation
- **Impact**: Cannot cancel in-progress video generation at provider level
- **Code**:
  ```csharp
  var task = createVideoMethod.Invoke(clientToCheck, new object?[] { videoRequest, null, CancellationToken.None });
  ```

### 10. **Background Service Architecture**
- **Location**: `VideoGenerationBackgroundService.cs`
- **Issues**:
  - Only performs cleanup and metrics collection
  - Doesn't actually process video generation tasks
  - No integration with proper job queue
- **Impact**: Async tasks aren't processed by a dedicated worker

## Architectural Recommendations

### 1. **Implement Proper Job Queue**
- Use a persistent job queue (e.g., Hangfire, Azure Service Bus, AWS SQS)
- Store job state in database, not just cache
- Implement proper worker pattern for processing

### 2. **Remove All Timeouts for Video Operations**
- Create dedicated HTTP clients without any timeout policies
- Use webhook callbacks instead of polling
- Implement server-sent events or WebSockets for real-time updates

### 3. **Fix Task Management**
- Track all background tasks properly
- Implement graceful shutdown that waits for tasks
- Use `IHostedService` or similar for long-running operations

### 4. **Enhance Event-Driven Architecture**
- Create `VideoGenerationWorker` consumer
- Use message queue for reliable processing
- Implement retry policies at the message level

### 5. **Implement Webhook Support**
- Add webhook URL to video generation requests
- Provider callbacks on completion
- Eliminate polling entirely

### 6. **Database-Backed Task State**
- Create `VideoGenerationTasks` table
- Store all task metadata persistently
- Enable resume after restart

### 7. **Proper Cancellation Support**
- Propagate cancellation tokens throughout
- Implement provider-level cancellation
- Clean up resources on cancellation

### 8. **Separate Video Generation Service**
- Consider extracting video generation to a separate microservice
- Use appropriate infrastructure for long-running tasks
- Scale workers independently

## Code Locations Summary

| Issue | File | Line(s) | Severity |
|-------|------|---------|----------|
| Sync timeout | VideosController.cs | 26, 98-100 | Critical |
| Fire-and-forget | VideoGenerationOrchestrator.cs | 237-266 | Critical |
| HTTP factory timeout | HttpClientExtensions.cs | 235-257 | High |
| Polling limits | MiniMaxClient.cs | 458-689 | High |
| No cancellation | VideoGenerationOrchestrator.cs | 244 | Medium |
| No persistent tasks | Throughout | - | Critical |
| WebUI timeouts | HttpClientBuilderExtensions.cs | 36-45 | Medium |

## Conclusion

The current video generation implementation is fundamentally synchronous despite having async endpoints. The architecture needs significant refactoring to support truly asynchronous, long-running video generation operations. The primary issues are:

1. Multiple timeout layers that will kill long-running operations
2. Lack of persistent task state and proper job queue
3. Fire-and-forget patterns without proper tracking
4. No recovery mechanism for failed or interrupted operations

These issues will cause production failures for any video generation taking longer than 2-10 minutes, depending on which timeout is hit first.