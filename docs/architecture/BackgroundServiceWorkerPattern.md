# Background Service Worker Pattern

## Overview

This document describes the proper implementation of background service workers in Conduit for processing asynchronous tasks. The pattern has been implemented to fix issue #122, which identified that the background service was only performing cleanup instead of actually processing video generation tasks.

## The Problem

The original implementation had a fundamental misunderstanding of background service patterns:

1. **Background Service Only Did Cleanup**: The `VideoGenerationBackgroundService` only performed cleanup and metrics collection
2. **Direct Task Processing**: The orchestrator used `Task.Run` to process tasks directly (fire-and-forget anti-pattern)
3. **No Work Queue Processing**: Background service didn't pull tasks from a queue or execute actual work

## The Solution

The background service has been refactored to implement a proper worker pattern:

### 1. Work Queue Consumer

The `VideoGenerationBackgroundService` now actively polls for pending tasks:

```csharp
private async Task RunVideoGenerationWorkerAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        // Pull pending tasks from queue
        var pendingTasks = await _taskService.GetPendingTasksAsync("video_generation", limit: 10);
        
        foreach (var task in pendingTasks)
        {
            // Update status to processing
            await _taskService.UpdateTaskStatusAsync(task.Id, TaskState.Processing);
            
            // Publish event for processing
            await _publishEndpoint.Publish(videoGenerationRequest);
        }
        
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }
}
```

### 2. Queue-Based Task Submission

Tasks are now queued in the database with a "Pending" state, and the background service picks them up:

```csharp
// Controller creates task and returns immediately
var taskId = await _taskService.CreateTaskAsync("video_generation", metadata);
return Accepted(new { taskId });

// Background service processes the task asynchronously
```

### 3. Removal of Task.Run Anti-Pattern

The `VideoGenerationOrchestrator` no longer uses `Task.Run` for fire-and-forget execution. Instead, it processes tasks synchronously within the worker thread:

```csharp
// OLD (anti-pattern)
_ = Task.Run(async () => await ProcessVideoAsync(...));

// NEW (proper pattern)
await ProcessVideoAsync(...); // Runs within worker thread
```

## Architecture

### Components

1. **IAsyncTaskService**: Extended with `GetPendingTasksAsync` method
2. **VideoGenerationBackgroundService**: Implements the worker pattern
3. **VideoGenerationOrchestrator**: Processes tasks synchronously when called by worker
4. **Task Storage**: Database-backed queue for durability

### Task Lifecycle

1. **Task Creation**: API endpoint creates task with "Pending" state
2. **Task Discovery**: Background worker polls for pending tasks
3. **Task Processing**: Worker updates status to "Processing" and executes work
4. **Task Completion**: Status updated to "Completed" with results
5. **Task Cleanup**: Old tasks cleaned up after retention period

## Benefits

1. **Resilience**: Tasks survive service restarts
2. **Scalability**: Can run multiple workers
3. **Separation of Concerns**: API just queues, workers process
4. **Resource Management**: Control concurrent processing
5. **Monitoring**: Easy to track queue depth and processing rate

## Configuration

### Worker Configuration

```csharp
// In Program.cs
builder.Services.AddHostedService<VideoGenerationBackgroundService>();

// Configure worker behavior
builder.Services.Configure<WorkerOptions>(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(1);
    options.BatchSize = 10;
    options.MaxConcurrentTasks = 5;
});
```

### Task Retention

```csharp
// Configure how long to keep completed tasks
builder.Services.Configure<TaskRetentionOptions>(options =>
{
    options.CompletedTaskRetention = TimeSpan.FromHours(24);
    options.FailedTaskRetention = TimeSpan.FromDays(7);
});
```

## Migration Path

### From Fire-and-Forget to Worker Pattern

1. **Phase 1**: Add queue-based processing (completed)
2. **Phase 2**: Remove direct processing from orchestrator (completed)
3. **Phase 3**: Add distributed processing support (future)

### Database Considerations

Tasks are stored in the `AsyncTasks` table with the following states:
- `0`: Pending (waiting to be processed)
- `1`: Processing (currently being worked on)
- `2`: Completed (finished successfully)
- `3`: Failed (encountered error)
- `4`: Cancelled (cancelled by user)
- `5`: TimedOut (exceeded time limit)

## Monitoring

### Key Metrics

1. **Queue Depth**: Number of pending tasks
2. **Processing Rate**: Tasks processed per minute
3. **Failure Rate**: Percentage of failed tasks
4. **Processing Time**: Average time to complete tasks

### Health Checks

The background service includes health monitoring:
- Checks if worker is running
- Monitors task processing rate
- Alerts on high queue depth

## Future Enhancements

### Distributed Processing

For multi-instance deployments:
1. Use distributed locks for task claiming
2. Implement work stealing for load balancing
3. Add instance affinity for certain task types

### Priority Queues

Support for task prioritization:
1. High priority tasks processed first
2. SLA-based scheduling
3. Fair queuing to prevent starvation

### Dead Letter Queue

Handle permanently failed tasks:
1. Retry logic with exponential backoff
2. Move to DLQ after max retries
3. Manual intervention workflow

## Related Issues

- #116: Video generation not truly asynchronous
- #117: Fire-and-forget anti-pattern
- #118: AsyncTaskService storage issues
- #121: Webhook implementation (uses proper async pattern)
- #122: Background service architecture (this implementation)