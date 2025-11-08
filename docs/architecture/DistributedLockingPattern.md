# Distributed Locking Pattern

## Overview

This document describes the distributed locking implementation in Conduit that prevents race conditions when multiple workers process async tasks. The solution addresses issue #123 by implementing task leasing, distributed locks, and optimistic concurrency control.

## The Problem

Without distributed locking, multiple worker instances can:
- Process the same task simultaneously
- Overwrite each other's progress updates  
- Create inconsistent state in the database
- Waste resources on duplicate work

## The Solution

### 1. Task Lease Pattern

Tasks are "leased" to specific workers for a limited time:

```csharp
// Worker attempts to lease a task
var leasedTask = await repository.LeaseNextPendingTaskAsync(
    workerId: "worker-instance-123",
    leaseDuration: TimeSpan.FromMinutes(10),
    taskType: "video_generation");

if (leasedTask != null)
{
    // Worker has exclusive access to process this task
    await ProcessTaskAsync(leasedTask);
}
```

#### Database Schema Changes

```sql
-- New columns added to AsyncTasks table
ALTER TABLE AsyncTasks ADD COLUMN LeasedBy VARCHAR(100);
ALTER TABLE AsyncTasks ADD COLUMN LeaseExpiryTime DATETIME;
ALTER TABLE AsyncTasks ADD COLUMN Version INT DEFAULT 0;

-- Index for efficient lease queries
CREATE INDEX IX_AsyncTasks_Lease ON AsyncTasks 
    (State, IsArchived, LeaseExpiryTime, CreatedAt);
```

### 2. Distributed Lock Service

For operations requiring exclusive access beyond task leasing:

```csharp
public interface IDistributedLockService
{
    Task<IDistributedLock?> AcquireLockAsync(
        string key, 
        TimeSpan expiry, 
        CancellationToken cancellationToken = default);
}

// Usage example
using (var lockHandle = await lockService.AcquireLockAsync("critical-operation", TimeSpan.FromSeconds(30)))
{
    if (lockHandle != null)
    {
        // Exclusive access to perform operation
    }
}
```

#### Redis Implementation

Uses Redis SET NX EX for atomic lock acquisition:

```lua
-- Acquire lock
SET lock:key lockValue NX EX 30

-- Release lock (Lua script ensures we only delete our own lock)
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
else
    return 0
end
```

### 3. Optimistic Concurrency Control

Version tracking prevents lost updates:

```csharp
// Task has Version property that increments on each update
public async Task<bool> UpdateWithVersionCheckAsync(
    AsyncTask task, 
    int expectedVersion)
{
    if (currentVersion != expectedVersion)
    {
        // Another worker updated the task
        return false;
    }
    
    task.Version = expectedVersion + 1;
    await SaveAsync(task);
    return true;
}
```

### 4. Lease Recovery

Background process recovers tasks from crashed workers:

```csharp
private async Task RunExpiredLeaseRecoveryAsync()
{
    // Find tasks with expired leases
    var expiredTasks = await repository.GetExpiredLeaseTasksAsync();
    
    foreach (var task in expiredTasks)
    {
        // Reset to pending state for re-processing
        task.State = TaskState.Pending;
        task.LeasedBy = null;
        task.LeaseExpiryTime = null;
        await repository.UpdateAsync(task);
    }
}
```

## Implementation Details

### Worker Pattern Changes

The VideoGenerationBackgroundService now:
1. Leases tasks atomically instead of bulk fetching
2. Maintains lease during processing
3. Releases lease on completion
4. Handles expired leases from crashed workers

```csharp
// Old pattern (race condition prone)
var pendingTasks = await GetPendingTasksAsync();
foreach (var task in pendingTasks) { /* process */ }

// New pattern (lease-based)
while (!cancellationToken.IsCancellationRequested)
{
    var leasedTask = await LeaseNextPendingTaskAsync(workerId, leaseDuration);
    if (leasedTask != null) { /* process with exclusive access */ }
}
```

### Configuration

#### Redis Mode (Production)
```csharp
services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnectionString));
services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();
```

#### In-Memory Mode (Development)
```csharp
services.AddSingleton<IDistributedLockService, InMemoryDistributedLockService>();
```

## Testing Scenarios

### 1. Concurrent Worker Test
```csharp
// Spawn multiple workers
var workers = Enumerable.Range(1, 5)
    .Select(i => Task.Run(() => RunWorkerAsync($"worker-{i}")))
    .ToArray();

// Verify no duplicate processing
Assert.Equal(tasksCreated, tasksProcessed);
Assert.True(processedTaskIds.Distinct().Count() == processedTaskIds.Count);
```

### 2. Lease Expiry Test
```csharp
// Lease a task
var task = await LeaseNextPendingTaskAsync("worker-1", TimeSpan.FromSeconds(5));

// Wait for lease to expire
await Task.Delay(TimeSpan.FromSeconds(6));

// Another worker should be able to lease it
var reacquired = await LeaseNextPendingTaskAsync("worker-2", TimeSpan.FromMinutes(10));
Assert.NotNull(reacquired);
```

### 3. Version Conflict Test
```csharp
// Two workers read same task
var task1 = await GetTaskAsync(taskId);
var task2 = await GetTaskAsync(taskId);

// Both try to update
var result1 = await UpdateWithVersionCheckAsync(task1, task1.Version);
var result2 = await UpdateWithVersionCheckAsync(task2, task2.Version);

// Only one should succeed
Assert.True(result1 ^ result2);
```

## Performance Considerations

### Database Queries
- Lease acquisition uses row-level locking
- Indexed on (State, IsArchived, LeaseExpiryTime, CreatedAt)
- Single query to find and lease task atomically

### Redis Operations
- Lock acquisition: O(1) 
- Lock release: O(1)
- No polling or spinning

### Scalability
- Supports unlimited worker instances
- No central coordinator bottleneck
- Lease duration configurable per workload

## Migration Guide

### Phase 1: Add Schema
1. Run migration to add lease columns
2. Deploy with backward compatibility

### Phase 2: Enable Leasing
1. Update workers to use lease pattern
2. Monitor for proper lease acquisition
3. Verify no duplicate processing

### Phase 3: Cleanup
1. Remove old GetPendingTasksAsync usage
2. Enable strict lease enforcement
3. Optimize lease durations

## Monitoring

Key metrics to track:
- **Lease acquisition rate**: Tasks leased per minute
- **Lease conflicts**: Failed acquisition attempts  
- **Expired leases**: Tasks recovered from dead workers
- **Version conflicts**: Optimistic locking failures
- **Lock wait time**: Time to acquire distributed locks

## Related Issues
- #116: Video generation not truly asynchronous
- #118: AsyncTaskService storage issues  
- #122: Background service worker pattern
- #123: Race conditions in async task processing (this implementation)