# Video Generation Architecture Improvements

## Overview

This document describes the improvements made to the video generation architecture to eliminate the fire-and-forget anti-pattern and implement proper event-driven progress tracking.

## Previous Issues

The original `VideoGenerationOrchestrator` used a dangerous fire-and-forget pattern:

```csharp
// OLD ANTI-PATTERN - DO NOT USE
_ = Task.Run(async () => await TrackProgressAsync(request, taskCts.Token), taskCts.Token);
```

This pattern caused several issues:
- **Memory Leaks**: Unobserved task exceptions could cause memory leaks
- **Lost Tasks**: No way to track or manage the spawned tasks
- **No Cancellation**: Progress tracking continued even after task failure
- **Poor Observability**: No visibility into running progress tasks

## New Event-Driven Architecture

### 1. Progress Tracking Events

Two new domain events handle progress tracking lifecycle:

```csharp
// Requests a progress check at a scheduled time
public record VideoProgressCheckRequested : DomainEvent
{
    public string RequestId { get; init; }
    public string VirtualKeyId { get; init; }
    public DateTime ScheduledAt { get; init; }
    public int IntervalIndex { get; init; }
    public DateTime StartTime { get; init; }
    public string PartitionKey => VirtualKeyId;
}

// Cancels progress tracking for a task
public record VideoProgressTrackingCancelled : DomainEvent
{
    public string RequestId { get; init; }
    public string VirtualKeyId { get; init; }
    public string Reason { get; init; }
    public string PartitionKey => VirtualKeyId;
}
```

### 2. VideoProgressTrackingOrchestrator

A dedicated orchestrator handles progress tracking:

```csharp
public class VideoProgressTrackingOrchestrator : IConsumer<VideoProgressCheckRequested>
{
    public async Task Consume(ConsumeContext<VideoProgressCheckRequested> context)
    {
        // Check task status
        // Update progress if needed
        // Schedule next check or stop tracking
    }
}
```

Key features:
- **Self-scheduling**: Each progress check schedules the next one
- **Automatic cancellation**: Stops when task is no longer processing
- **Error isolation**: Exceptions don't affect main task
- **Time-sensitive processing**: No retries for progress checks

### 3. Progress Lifecycle Management

The `VideoGenerationOrchestrator` now properly manages progress tracking:

```csharp
// Start progress tracking
await StartProgressTrackingAsync(request);

// Cancel progress tracking on completion/failure/cancellation
await _publishEndpoint.Publish(new VideoProgressTrackingCancelled
{
    RequestId = request.RequestId,
    VirtualKeyId = request.VirtualKeyId,
    Reason = "Video generation completed successfully"
});
```

### 4. MassTransit Configuration

Progress tracking uses partitioned endpoints for ordered processing:

```csharp
cfg.ReceiveEndpoint("video-progress-tracking", e =>
{
    // Partition by virtual key for ordered processing
    e.ConfigurePartitioner<VideoProgressCheckRequested>(
        context,
        p => p.Message.PartitionKey);
    
    e.ConfigureConsumer<VideoProgressTrackingOrchestrator>(context);
    
    // No retry for time-sensitive progress checks
    e.UseMessageRetry(r => r.None());
});
```

## Benefits

### 1. Proper Resource Management
- No untracked background tasks
- Clean cancellation when tasks complete
- No memory leaks from unobserved exceptions

### 2. Better Observability
- All progress updates go through MassTransit
- Full tracing and logging support
- Clear event flow in logs

### 3. Scalability
- Progress tracking works across multiple instances
- Partitioned processing ensures consistency
- No in-memory state required

### 4. Reliability
- Progress tracking survives service restarts
- Automatic cleanup on task completion
- No zombie progress trackers

## Migration Guide

If you have similar fire-and-forget patterns in your code:

1. **Identify fire-and-forget usage**:
   ```csharp
   _ = Task.Run(...);  // Look for discarded tasks
   ```

2. **Create domain events** for the background work

3. **Implement event consumers** to handle the work

4. **Use self-scheduling pattern** for periodic tasks:
   ```csharp
   // In consumer
   if (shouldContinue)
   {
       await _publishEndpoint.Publish(new NextCheck { ScheduledAt = DateTime.UtcNow.AddSeconds(5) });
   }
   ```

5. **Add lifecycle management** to cancel background work when main task completes

## Monitoring

Key metrics to monitor:
- **Progress event throughput**: Number of progress checks per minute
- **Progress tracking duration**: How long progress tracking runs per task
- **Orphaned progress trackers**: Progress checks for non-existent tasks
- **Event processing latency**: Time between scheduled and actual progress check

## Testing

When testing video generation with progress tracking:

1. **Verify progress starts**: Check that VideoProgressCheckRequested is published
2. **Verify progress updates**: Ensure progress events are published at intervals
3. **Verify cleanup**: Confirm VideoProgressTrackingCancelled is published on completion
4. **Test failure scenarios**: Ensure progress stops when task fails
5. **Test cancellation**: Verify progress stops when task is cancelled

## Future Improvements

1. **Dynamic progress intervals**: Adjust check frequency based on task duration
2. **Progress prediction**: Use historical data to predict completion time
3. **Adaptive scheduling**: Reduce checks for long-running stable tasks
4. **Progress aggregation**: Batch progress updates for multiple tasks