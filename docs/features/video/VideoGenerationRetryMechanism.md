# Video Generation Retry Mechanism

## Overview

This document describes the retry mechanism implemented for failed video generation tasks in Conduit. The solution addresses issue #124 by implementing automatic retries with exponential backoff, manual retry capabilities, and comprehensive error handling.

## Architecture

### Database Schema

The `AsyncTask` entity has been enhanced with retry-related fields:

```csharp
public class AsyncTask
{
    // ... existing fields ...
    
    /// <summary>
    /// Number of retry attempts made for this task.
    /// </summary>
    public int RetryCount { get; set; } = 0;
    
    /// <summary>
    /// Maximum number of retry attempts allowed for this task.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Whether the task is retryable if it fails.
    /// </summary>
    public bool IsRetryable { get; set; } = true;
    
    /// <summary>
    /// When the task should be retried next (null if not scheduled for retry).
    /// </summary>
    public DateTime? NextRetryAt { get; set; }
}
```

### Retry Configuration

The retry behavior is configurable via `VideoGenerationRetryConfiguration`:

```csharp
public class VideoGenerationRetryConfiguration
{
    public int MaxRetries { get; set; } = 3;
    public int BaseDelaySeconds { get; set; } = 30;
    public int MaxDelaySeconds { get; set; } = 3600;
    public bool EnableRetries { get; set; } = true;
    public int RetryCheckIntervalSeconds { get; set; } = 30;
}
```

Configure via environment variables:
```bash
export VideoGeneration__MaxRetries=5
export VideoGeneration__BaseDelaySeconds=60
export VideoGeneration__MaxDelaySeconds=1800
export VideoGeneration__EnableRetries=true
export VideoGeneration__RetryCheckIntervalSeconds=30
```

### Retry Logic Flow

1. **Failure Detection** (VideoGenerationOrchestrator)
   - When a video generation task fails, the orchestrator determines if the error is retryable
   - Retryable errors include: timeouts, network errors, rate limits, service unavailability
   - Non-retryable errors include: validation errors, authentication failures

2. **Retry Scheduling**
   - If retryable and retry count < max retries:
     - Calculate delay using exponential backoff with jitter
     - Set task state back to `Pending`
     - Set `NextRetryAt` to current time + calculated delay
     - Increment `RetryCount`

3. **Retry Execution** (VideoGenerationBackgroundService)
   - Background worker queries pending tasks where `NextRetryAt <= now`
   - Task lease mechanism ensures only one worker processes the retry
   - Task is processed normally with updated retry metadata

4. **Manual Retry** (API Endpoint)
   - Endpoint: `POST /v1/videos/generations/tasks/{taskId}/retry`
   - Validates task is failed and hasn't exceeded max retries
   - Resets task to pending state for immediate retry

### Exponential Backoff Algorithm

```csharp
public int CalculateRetryDelay(int retryCount)
{
    // Exponential backoff: BaseDelay * 2^retryCount
    var delay = BaseDelaySeconds * Math.Pow(2, retryCount);
    
    // Add jitter (±20% randomization)
    var jitter = new Random().NextDouble() * 0.4 - 0.2;
    delay = delay * (1 + jitter);
    
    // Cap at maximum delay
    return (int)Math.Min(delay, MaxDelaySeconds);
}
```

Example delays with 30s base:
- Retry 1: ~30s (24-36s with jitter)
- Retry 2: ~60s (48-72s with jitter)
- Retry 3: ~120s (96-144s with jitter)

## Implementation Details

### VideoGenerationOrchestrator

The orchestrator handles failure scenarios with retry logic:

```csharp
catch (Exception ex)
{
    var isRetryable = IsRetryableError(ex);
    var retryCount = taskStatus?.RetryCount ?? 0;
    var maxRetries = _retryConfiguration.MaxRetries;
    
    if (isRetryable && retryCount < maxRetries)
    {
        var delaySeconds = _retryConfiguration.CalculateRetryDelay(retryCount);
        var nextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
        
        // Schedule retry
        await _taskService.UpdateTaskStatusAsync(
            request.RequestId, 
            TaskState.Pending, 
            error: $"Retry {retryCount + 1}/{maxRetries} scheduled: {ex.Message}");
        
        // Publish retry event
        await _publishEndpoint.Publish(new VideoGenerationFailed
        {
            RequestId = request.RequestId,
            IsRetryable = true,
            RetryCount = retryCount,
            MaxRetries = maxRetries,
            NextRetryAt = nextRetryAt
        });
    }
}
```

### HybridAsyncTaskService

The task service automatically sets `NextRetryAt` when a task is marked for retry:

```csharp
if (status == TaskState.Pending && dbTask.RetryCount < dbTask.MaxRetries)
{
    dbTask.RetryCount++;
    // Calculate exponential backoff with jitter
    var delaySeconds = baseDelay * Math.Pow(2, dbTask.RetryCount - 1);
    dbTask.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
}
```

### AsyncTaskRepository

The repository's `LeaseNextPendingTaskAsync` method considers retry timing:

```csharp
var query = context.AsyncTasks
    .Where(t => t.State == 0 && !t.IsArchived && 
           (t.LeasedBy == null || t.LeaseExpiryTime < now) &&
           (t.NextRetryAt == null || t.NextRetryAt <= now));
```

## Monitoring and Metrics

### Key Metrics

1. **Retry Success Rate**
   - Track successful retries vs permanent failures
   - Monitor by retry attempt number

2. **Common Failure Patterns**
   - Group failures by error type
   - Identify providers with high failure rates

3. **Retry Timing**
   - Average time between failure and successful retry
   - Distribution of retry attempts needed

### Logging

The system logs key retry events:
- Task scheduled for retry with attempt number
- Retry delay calculation
- Tasks found ready for retry
- Manual retry requests

### Webhook Notifications

Webhooks include retry information:
```json
{
    "taskId": "task_abc123",
    "status": "retrying",
    "retryCount": 1,
    "maxRetries": 3,
    "error": "Connection timeout"
}
```

## API Endpoints

### Manual Retry
```http
POST /v1/videos/generations/tasks/{taskId}/retry
Authorization: Bearer {virtual-key}

Response: 200 OK
{
    "taskId": "task_abc123",
    "status": "pending",
    "error": "Retry 1/3 scheduled"
}
```

### Task Status (includes retry info)
```http
GET /v1/videos/generations/tasks/{taskId}
Authorization: Bearer {virtual-key}

Response: 200 OK
{
    "taskId": "task_abc123",
    "status": "failed",
    "retryCount": 2,
    "maxRetries": 3,
    "isRetryable": true,
    "nextRetryAt": "2025-06-23T10:30:00Z"
}
```

## Best Practices

1. **Set Appropriate Max Retries**
   - Default of 3 retries works for most cases
   - Consider provider-specific limits

2. **Monitor Retry Patterns**
   - High retry rates may indicate infrastructure issues
   - Adjust delays based on provider recovery times

3. **Handle Non-Retryable Errors**
   - Validation errors should fail immediately
   - Clear error messages help users fix issues

4. **Use Webhook Notifications**
   - Get real-time updates on retry status
   - Implement client-side retry UX

## Future Enhancements

1. **Provider-Specific Retry Strategies**
   - Different delays per provider
   - Custom retry logic for specific error codes

2. **Circuit Breaker Integration**
   - Prevent retries when provider is down
   - Automatic recovery detection

3. **Retry Budget**
   - Limit total retries per time period
   - Prevent retry storms

4. **Advanced Metrics**
   - Retry cost tracking
   - Success prediction based on error type