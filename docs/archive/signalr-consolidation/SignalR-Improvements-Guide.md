# SignalR Improvements Guide

This guide outlines recommended improvements to enhance the reliability, performance, and monitoring of SignalR message processing in Conduit.

## 1. **Message Acknowledgment Pattern**

### Problem
Currently, there's no way to confirm that clients have received critical messages. Messages can be lost due to network issues or client disconnections.

### Solution
Implement a message acknowledgment pattern with unique message IDs:

```csharp
// In SignalRMessage.cs
public abstract class SignalRMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
    public int RetryCount { get; set; }
}
```

### Benefits
- Track message delivery confirmation
- Implement retry logic for unacknowledged messages
- Correlate related messages
- Audit trail for message flow

## 2. **Message Queue with Retry Logic**

### Problem
Transient network issues can cause message delivery failures. There's no retry mechanism for failed messages.

### Solution
Implement `SignalRMessageQueueService` with:
- Concurrent queue for messages
- Polly retry policy with exponential backoff
- Circuit breaker to prevent cascading failures
- Dead letter queue for persistent failures

### Benefits
- Automatic retry on transient failures
- Protection against thundering herd
- Graceful degradation during outages
- Message persistence options

## 3. **Connection State Monitoring**

### Problem
No visibility into active connections, subscription patterns, or connection health.

### Solution
Implement `SignalRConnectionMonitor` that tracks:
- Active connections per hub
- Group subscriptions per connection
- Connection duration and activity
- Stale connection cleanup

### Benefits
- Real-time connection metrics
- Detect and clean up zombie connections
- Track subscription patterns
- Performance troubleshooting data

## 4. **Message Batching**

### Problem
Sending many individual messages causes network overhead and can overwhelm clients.

### Solution
Implement `SignalRMessageBatcher` that:
- Batches messages within a time window (100ms)
- Groups messages by type
- Sends as single payload
- Respects maximum batch size

### Benefits
- Reduced network overhead
- Better client performance
- Lower latency for bulk updates
- Configurable batch windows

## 5. **Enhanced Hub Features**

### Recommendations for SecureHub base class:

### a) **Connection Lifecycle Hooks**
```csharp
protected virtual async Task OnConnectionEstablishedAsync(ConnectionContext context)
{
    // Log connection metrics
    // Initialize connection-specific state
    // Send welcome message
}

protected virtual async Task OnConnectionLostAsync(ConnectionContext context, Exception? ex)
{
    // Clean up resources
    // Log disconnection reason
    // Notify other services
}
```

### b) **Message Send Confirmation**
```csharp
protected async Task<bool> SendWithConfirmationAsync(
    string method, 
    object message, 
    TimeSpan timeout)
{
    var messageId = Guid.NewGuid().ToString();
    var tcs = new TaskCompletionSource<bool>();
    
    // Store pending acknowledgment
    _pendingAcks[messageId] = tcs;
    
    // Send message with ID
    await Clients.Caller.SendAsync(method, new { messageId, data = message });
    
    // Wait for acknowledgment
    using var cts = new CancellationTokenSource(timeout);
    try
    {
        return await tcs.Task.WaitAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        return false;
    }
}
```

### c) **Group Management Enhancement**
```csharp
protected async Task AddToGroupWithMetadataAsync(
    string groupName, 
    Dictionary<string, object> metadata)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    _connectionMonitor.AddGroupSubscription(Context.ConnectionId, groupName);
    
    // Store metadata for analytics
    _groupMetadata[Context.ConnectionId] = metadata;
}
```

## 6. **Client-Side Improvements**

### a) **Automatic Reconnection with Backoff**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: retryContext => {
            if (retryContext.elapsedMilliseconds < 60000) {
                // First 60 seconds: exponential backoff
                return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
            } else {
                // After 60 seconds: every 30 seconds
                return 30000;
            }
        }
    })
    .build();
```

### b) **Message Acknowledgment**
```javascript
connection.on("TaskProgress", async (message) => {
    try {
        // Process message
        await processTaskProgress(message);
        
        // Send acknowledgment
        if (message.messageId) {
            await connection.invoke("AcknowledgeMessage", message.messageId);
        }
    } catch (error) {
        console.error("Failed to process message:", error);
        if (message.messageId) {
            await connection.invoke("NackMessage", message.messageId, error.message);
        }
    }
});
```

### c) **Connection Quality Monitoring**
```javascript
let lastPingTime = Date.now();
setInterval(async () => {
    if (connection.state === signalR.HubConnectionState.Connected) {
        const startTime = Date.now();
        try {
            await connection.invoke("Ping");
            const latency = Date.now() - startTime;
            updateConnectionQuality(latency);
        } catch (error) {
            handleConnectionError(error);
        }
    }
}, 30000); // Every 30 seconds
```

## 7. **Performance Optimizations**

### a) **Message Compression**
Enable compression in Program.cs:
```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 512 * 1024; // 512KB
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
})
.AddMessagePackProtocol(); // Binary protocol for better performance
```

### b) **Connection Pooling**
```csharp
services.AddSingleton<IHubConnectionPool>(sp =>
{
    return new HubConnectionPool(
        maxPoolSize: 100,
        connectionTimeout: TimeSpan.FromSeconds(30),
        idleTimeout: TimeSpan.FromMinutes(5));
});
```

### c) **Selective Updates**
Only send data that has changed:
```csharp
public async Task SendProgressUpdate(string taskId, TaskProgress progress)
{
    var delta = new
    {
        taskId,
        progressPercentage = progress.HasProgressChanged ? progress.Percentage : null,
        status = progress.HasStatusChanged ? progress.Status : null,
        message = progress.HasMessageChanged ? progress.Message : null,
        timestamp = DateTime.UtcNow
    };
    
    await Clients.Group($"task-{taskId}").SendAsync("TaskProgressDelta", delta);
}
```

## 8. **Monitoring and Diagnostics**

### a) **Health Checks**
```csharp
public class SignalRHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var metrics = _connectionMonitor.GetMetrics();
        
        if (metrics.StaleConnections > metrics.ActiveConnections * 0.1)
        {
            return HealthCheckResult.Degraded(
                $"High stale connection ratio: {metrics.StaleConnections}/{metrics.ActiveConnections}");
        }
        
        return HealthCheckResult.Healthy(
            $"Active connections: {metrics.ActiveConnections}, Groups: {metrics.TotalGroups}");
    }
}
```

### b) **Metrics Collection**
```csharp
// In SignalRMetrics.cs
public class SignalRMetrics
{
    public readonly Counter<long> MessagesDelivered;
    public readonly Counter<long> MessagesFailed;
    public readonly Histogram<double> MessageDeliveryDuration;
    public readonly ObservableGauge<int> ActiveConnections;
    public readonly ObservableGauge<int> ActiveGroups;
    
    // Track per hub and method
    public void RecordMessageDelivery(string hub, string method, double duration, bool success)
    {
        var tags = new TagList 
        { 
            { "hub", hub }, 
            { "method", method },
            { "success", success }
        };
        
        if (success)
            MessagesDelivered.Add(1, tags);
        else
            MessagesFailed.Add(1, tags);
            
        MessageDeliveryDuration.Record(duration, tags);
    }
}
```

### c) **Diagnostic Logging**
```csharp
services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // In development
})
.AddHubOptions<VideoGenerationHub>(options =>
{
    options.AddFilter<SignalRDiagnosticFilter>();
});

public class SignalRDiagnosticFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await next(invocationContext);
            _logger.LogDebug(
                "Hub method {Method} completed in {Duration}ms",
                invocationContext.HubMethodName,
                stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Hub method {Method} failed after {Duration}ms",
                invocationContext.HubMethodName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

## 9. **Security Enhancements**

### a) **Rate Limiting**
```csharp
services.AddRateLimiter(options =>
{
    options.AddPolicy("signalr", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 50
            }));
});
```

### b) **Message Validation**
```csharp
protected override async Task OnConnectedAsync()
{
    // Validate connection token
    var token = Context.GetHttpContext()?.Request.Query["access_token"];
    if (!await ValidateTokenAsync(token))
    {
        Context.Abort();
        return;
    }
    
    await base.OnConnectedAsync();
}
```

## 10. **Testing Improvements**

### a) **SignalR Integration Tests**
```csharp
[Fact]
public async Task Should_Receive_Progress_Updates()
{
    // Arrange
    var connection = new HubConnectionBuilder()
        .WithUrl($"http://localhost/hubs/video-generation", options =>
        {
            options.AccessTokenProvider = () => Task.FromResult(_testToken);
        })
        .Build();
        
    var progressReceived = new TaskCompletionSource<int>();
    connection.On<TaskProgressMessage>("TaskProgress", message =>
    {
        progressReceived.SetResult(message.ProgressPercentage);
    });
    
    // Act
    await connection.StartAsync();
    await connection.InvokeAsync("SubscribeToTask", "test-task-123");
    
    // Simulate progress update
    await _notificationService.NotifyProgressAsync("test-task-123", 50);
    
    // Assert
    var progress = await progressReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(50, progress);
}
```

### b) **Load Testing**
```csharp
[Fact]
public async Task Should_Handle_Concurrent_Connections()
{
    var tasks = new List<Task>();
    var connectionCount = 1000;
    
    for (int i = 0; i < connectionCount; i++)
    {
        tasks.Add(Task.Run(async () =>
        {
            var connection = CreateConnection();
            await connection.StartAsync();
            await Task.Delay(Random.Next(1000, 5000));
            await connection.DisposeAsync();
        }));
    }
    
    await Task.WhenAll(tasks);
    
    var metrics = _connectionMonitor.GetMetrics();
    Assert.Equal(0, metrics.ActiveConnections);
}
```

## Implementation Priority

1. **High Priority**
   - Message acknowledgment pattern
   - Connection monitoring
   - Enhanced error handling

2. **Medium Priority**
   - Message batching
   - Retry logic with circuit breaker
   - Health checks

3. **Low Priority**
   - Message compression
   - Advanced metrics
   - Load testing framework

## Conclusion

These improvements will significantly enhance the reliability, performance, and observability of SignalR in Conduit. Start with high-priority items and gradually implement others based on system requirements and usage patterns.