# Distributed Spend Notification Service

## Overview
The `DistributedSpendNotificationService` replaces the in-memory `SpendNotificationService` to provide multi-instance consistency for spend tracking and budget alert notifications. This implementation addresses issue #817 by using Redis for centralized state management.

## Problem Solved
Previously, the spend notification service used in-memory collections (`ConcurrentDictionary`) which caused:
- Duplicate budget alerts when running multiple instances
- Inconsistent spending pattern tracking across instances
- Lost alert history on service restarts
- Alert storms during major budget events

## Solution Architecture

### Key Components

1. **Redis-Based Storage**
   - Spending patterns stored in Redis Hash with TTL
   - Alert tracking using Redis keys with expiration
   - Spend history stream for analysis
   - Instance registration for monitoring

2. **Distributed Locking**
   - Uses `IDistributedLockService` for alert deduplication
   - Prevents multiple instances from sending same alert
   - 5-second timeout for lock acquisition

3. **Alert Deduplication**
   - Cooldown periods for each alert type
   - Unique keys per virtual key and threshold
   - 4-hour default cooldown for budget alerts
   - 1-hour cooldown for unusual spending alerts

## Redis Key Structure

```
spend:patterns:{virtualKeyId}              # Spending pattern data (Hash)
spend:alerts:sent:{virtualKeyId}:{threshold}  # Sent alert tracking
spend:alerts:cooldown:{virtualKeyId}:{type}   # Alert cooldown tracking
spend:history:stream                       # Spend history for analysis
spend:notification:instances:{instanceId}  # Instance registration
lock:alert:vk:{virtualKeyId}:threshold:{threshold}  # Distributed locks
```

## Features

### Budget Alert Thresholds
- Monitors at 50%, 75%, 80%, 90%, 95%, and 100% thresholds
- Different severity levels: info, warning, high, critical, exceeded
- Automatic reset when spending drops below 50%
- Recommendations provided based on threshold

### Spending Pattern Analysis
- Tracks hourly and daily spending patterns
- Detects unusual patterns:
  - High frequency (>60 requests/hour)
  - Spending spikes (>10% of daily average in one hour)
- Pattern data retained for 24 hours
- Periodic analysis every 5 minutes

### Alert Types

1. **Budget Alerts**
   - Sent when budget thresholds are crossed
   - Include current spend, budget limit, percentage used
   - Severity-based recommendations

2. **Unusual Spending Notifications**
   - Detect anomalous spending patterns
   - Include deviation percentage and recommendations
   - Help identify potential issues early

## Configuration

### Service Registration
```csharp
// In Program.CoreServices.cs
builder.Services.AddSingleton<ISpendNotificationService, DistributedSpendNotificationService>();
builder.Services.AddHostedService<DistributedSpendNotificationService>(sp => 
    (DistributedSpendNotificationService)sp.GetRequiredService<ISpendNotificationService>());
```

### Configurable Parameters
- Alert cooldown period: 4 hours (budget), 1 hour (unusual)
- Pattern retention: 24 hours
- Analysis interval: 5 minutes
- Lock timeout: 5 seconds
- Budget thresholds: [50, 75, 80, 90, 95, 100]

## Fallback Behavior
When Redis is unavailable:
- Service falls back to direct SignalR notifications
- No deduplication or pattern tracking
- Logs warnings about degraded functionality
- Continues to send basic spend updates

## Benefits

1. **Horizontal Scalability**
   - Multiple instances share state via Redis
   - Consistent alert behavior across instances
   - No duplicate notifications

2. **Reliability**
   - Alert history persists across restarts
   - Distributed locks ensure single delivery
   - Cooldown periods prevent spam

3. **Performance**
   - Redis Streams for efficient spend history
   - Batched pattern analysis
   - Async operations throughout

4. **Observability**
   - Instance registration and heartbeats
   - Detailed logging with instance IDs
   - Spending pattern metrics

## Testing Checklist
- [x] Build compiles successfully
- [x] Service registers correctly
- [x] Interface methods implemented
- [ ] Redis connectivity verified
- [ ] Alert deduplication tested
- [ ] Pattern analysis functional
- [ ] Multi-instance behavior validated
- [ ] Fallback mode tested

## Migration Notes
- Existing `SpendNotificationService` replaced transparently
- No changes required to consuming code
- All interface methods maintained for compatibility
- Legacy `NotifySpendUpdatedAsync` method preserved

## Related Issues
- Issue #817: [CRITICAL] Spend Notifications Track Patterns In-Memory - Duplicate Alerts
- PR: (To be created)

## Future Enhancements
- Configurable thresholds per virtual key
- Custom alert channels (email, Slack)
- Machine learning for pattern detection
- Historical trend analysis
- Budget forecast predictions