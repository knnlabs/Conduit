# SignalR Troubleshooting Guide

*Last Updated: 2025-08-01*

This guide consolidates troubleshooting information for SignalR implementation issues in Conduit.

## Table of Contents
- [Common Connection Issues](#common-connection-issues)
- [Authentication Problems](#authentication-problems)
- [Message Delivery Issues](#message-delivery-issues)
- [Performance Problems](#performance-problems)
- [Redis Backplane Issues](#redis-backplane-issues)
- [Testing and Debugging](#testing-and-debugging)

## Common Connection Issues

### Connection Fails Immediately

**Symptoms:**
- SignalR connection never establishes
- Immediate connection failure in browser console
- WebSocket upgrade fails

**Common Causes & Solutions:**

1. **CORS Configuration**
   ```csharp
   // Ensure CORS allows SignalR origins
   services.AddCors(options =>
   {
       options.AddPolicy("SignalRPolicy", builder =>
       {
           builder.WithOrigins("https://your-frontend.com")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // Critical for SignalR
       });
   });
   ```

2. **WebSocket Support**
   ```javascript
   // Check WebSocket support
   if (!window.WebSocket) {
       console.error('WebSocket is not supported');
       // Fallback to Long Polling
       const connection = new signalR.HubConnectionBuilder()
           .withUrl(hubUrl, {
               transport: signalR.HttpTransportType.LongPolling
           })
           .build();
   }
   ```

3. **Proxy/Load Balancer Configuration**
   - Ensure WebSocket upgrades are allowed
   - Configure sticky sessions if using multiple instances
   - Set appropriate timeout values

### Connection Drops Frequently

**Symptoms:**
- Frequent reconnection attempts
- Connection state changes rapidly
- Messages lost during reconnection

**Solutions:**

1. **Configure Keep-Alive Settings**
   ```csharp
   services.AddSignalR(options =>
   {
       options.KeepAliveInterval = TimeSpan.FromSeconds(15);
       options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
   });
   ```

2. **Implement Robust Reconnection**
   ```javascript
   const connection = new signalR.HubConnectionBuilder()
       .withUrl(hubUrl)
       .withAutomaticReconnect([0, 2000, 10000, 30000]) // Custom retry delays
       .build();

   connection.onreconnecting(() => {
       console.log('Connection lost, attempting to reconnect...');
       showReconnectionStatus(true);
   });

   connection.onreconnected(() => {
       console.log('Successfully reconnected');
       showReconnectionStatus(false);
       resubscribeToEvents(); // Re-establish subscriptions
   });
   ```

3. **Network Diagnostics**
   ```javascript
   // Monitor connection quality
   connection.onclose((error) => {
       console.error('Connection closed:', error);
       if (error) {
           // Log specific error details for debugging
           console.error('Error details:', {
               message: error.message,
               stack: error.stack,
               timestamp: new Date().toISOString()
           });
       }
   });
   ```

## Authentication Problems

### Virtual Key Authentication Failures

**Symptoms:**
- 401 Unauthorized responses
- Connection established but hub methods fail
- "Access denied" errors in hub methods

**Diagnostic Steps:**

1. **Verify Token Format**
   ```javascript
   // Ensure token is properly formatted
   const connection = new signalR.HubConnectionBuilder()
       .withUrl(hubUrl, {
           accessTokenFactory: () => {
               const token = getVirtualKey();
               console.log('Using token:', token ? 'present' : 'missing');
               return token;
           }
       })
       .build();
   ```

2. **Check Server-Side Authentication**
   ```csharp
   [Authorize(AuthenticationSchemes = "VirtualKeySignalR")]
   public class MyHub : Hub
   {
       public override async Task OnConnectedAsync()
       {
           var userId = Context.UserIdentifier;
           var connectionId = Context.ConnectionId;
           
           _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", 
               userId, connectionId);
               
           if (string.IsNullOrEmpty(userId))
           {
               _logger.LogWarning("Connection {ConnectionId} has no user identifier", connectionId);
               Context.Abort();
               return;
           }
           
           await base.OnConnectedAsync();
       }
   }
   ```

3. **Validate Virtual Key**
   ```csharp
   // In your authentication handler
   public class VirtualKeySignalRAuthenticationHandler : AuthenticationHandler<VirtualKeySignalROptions>
   {
       protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
       {
           var token = Request.Query["access_token"].FirstOrDefault() ??
                      Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
                      
           if (string.IsNullOrEmpty(token))
           {
               _logger.LogWarning("No access token provided for SignalR authentication");
               return AuthenticateResult.Fail("No access token provided");
           }
           
           var virtualKey = await _virtualKeyService.ValidateKeyAsync(token);
           if (virtualKey == null)
           {
               _logger.LogWarning("Invalid virtual key provided: {Token}", token[..8] + "...");
               return AuthenticateResult.Fail("Invalid virtual key");
           }
           
           // Success - create claims principal
           var claims = new[] { new Claim("virtualKeyId", virtualKey.Id.ToString()) };
           var identity = new ClaimsIdentity(claims, "VirtualKeySignalR");
           var principal = new ClaimsPrincipal(identity);
           
           return AuthenticateResult.Success(new AuthenticationTicket(principal, "VirtualKeySignalR"));
       }
   }
   ```

### Master Key Authentication Issues

**Symptoms:**
- Admin hub connections fail
- Master key rejected

**Solutions:**

1. **Verify Master Key Configuration**
   ```csharp
   // Check configuration
   services.Configure<ConduitSettings>(configuration.GetSection("Conduit"));
   
   // In authentication handler
   var masterKey = _configuration["Conduit:MasterKey"];
   if (string.IsNullOrEmpty(masterKey))
   {
       _logger.LogError("Master key not configured");
       return AuthenticateResult.Fail("Master key not configured");
   }
   ```

2. **Test Master Key Authentication**
   ```javascript
   // Test admin connection
   const adminConnection = new signalR.HubConnectionBuilder()
       .withUrl('/hubs/admin-notifications', {
           accessTokenFactory: () => masterKey
       })
       .build();
       
   adminConnection.start()
       .then(() => console.log('Admin connection successful'))
       .catch(err => console.error('Admin connection failed:', err));
   ```

## Message Delivery Issues

### Messages Not Received by Clients

**Symptoms:**
- Server sends messages but clients don't receive them
- Some clients receive messages, others don't
- Events triggered but no client response

**Diagnostic Steps:**

1. **Verify Group Membership**
   ```csharp
   public class VideoGenerationHub : Hub
   {
       private readonly ILogger<VideoGenerationHub> _logger;
       
       public async Task SubscribeToVideoGeneration(string requestId)
       {
           var groupName = $"video-{requestId}";
           await Groups.AddToGroupAsync(ConnectionId, groupName);
           
           _logger.LogInformation("Connection {ConnectionId} joined group {GroupName}", 
               ConnectionId, groupName);
       }
       
       public async Task TestMessageDelivery(string requestId)
       {
           var groupName = $"video-{requestId}";
           await Clients.Group(groupName).SendAsync("TestMessage", 
               $"Test message for {requestId} at {DateTime.UtcNow}");
           
           _logger.LogInformation("Sent test message to group {GroupName}", groupName);
       }
   }
   ```

2. **Check Client Event Handlers**
   ```javascript
   // Ensure event handlers are registered before connection start
   const connection = new signalR.HubConnectionBuilder()
       .withUrl(hubUrl)
       .build();
   
   // Register handlers BEFORE starting connection
   connection.on('VideoProgress', (data) => {
       console.log('Received VideoProgress:', data);
   });
   
   connection.on('TestMessage', (message) => {
       console.log('Test message received:', message);
   });
   
   await connection.start();
   
   // Then subscribe to events
   await connection.invoke('SubscribeToVideoGeneration', requestId);
   await connection.invoke('TestMessageDelivery', requestId);
   ```

3. **Monitor Hub Context Usage**
   ```csharp
   public class VideoGenerationService
   {
       private readonly IHubContext<VideoGenerationHub> _hubContext;
       private readonly ILogger<VideoGenerationService> _logger;
       
       public async Task NotifyProgress(string requestId, int progress)
       {
           var groupName = $"video-{requestId}";
           
           _logger.LogInformation("Sending progress {Progress}% to group {GroupName}", 
               progress, groupName);
               
           await _hubContext.Clients.Group(groupName)
               .SendAsync("VideoProgress", new { RequestId = requestId, Progress = progress });
               
           _logger.LogInformation("Progress notification sent to group {GroupName}", groupName);
       }
   }
   ```

### Case Sensitivity Issues

**Common Problem:**
```javascript
// Server sends 'VideoProgress' but client listens for 'videoProgress'
connection.on('videoProgress', handler); // Wrong - won't receive messages

// Correct - match server casing exactly
connection.on('VideoProgress', handler);
```

**Solution:**
```csharp
// Document event names in constants
public static class SignalREvents
{
    public const string VideoProgress = nameof(VideoProgress);
    public const string VideoCompleted = nameof(VideoCompleted);
    public const string SpendUpdate = nameof(SpendUpdate);
}

// Use constants in hub
await Clients.Group(groupName).SendAsync(SignalREvents.VideoProgress, data);
```

## Performance Problems

### High Memory Usage

**Symptoms:**
- Memory usage grows continuously
- OutOfMemoryException
- Slow message delivery

**Solutions:**

1. **Connection Cleanup**
   ```csharp
   public class VideoGenerationHub : Hub
   {
       private readonly IMemoryCache _connectionGroups;
       
       public override async Task OnConnectedAsync()
       {
           // Track connection groups for cleanup
           _connectionGroups.Set(ConnectionId, new List<string>());
           await base.OnConnectedAsync();
       }
       
       public override async Task OnDisconnectedAsync(Exception exception)
       {
           // Clean up all groups for this connection
           if (_connectionGroups.TryGetValue(ConnectionId, out List<string> groups))
           {
               foreach (var group in groups)
               {
                   await Groups.RemoveFromGroupAsync(ConnectionId, group);
               }
               _connectionGroups.Remove(ConnectionId);
           }
           
           await base.OnDisconnectedAsync(exception);
       }
       
       public async Task SubscribeToVideoGeneration(string requestId)
       {
           var groupName = $"video-{requestId}";
           await Groups.AddToGroupAsync(ConnectionId, groupName);
           
           // Track this group membership
           if (_connectionGroups.TryGetValue(ConnectionId, out List<string> groups))
           {
               groups.Add(groupName);
           }
       }
   }
   ```

2. **Message Size Optimization**
   ```csharp
   // Don't send large payloads via SignalR
   public class VideoProgressDto
   {
       public string RequestId { get; set; }
       public int Progress { get; set; }
       public string Status { get; set; }
       // Don't include large data like video URLs or binary data
   }
   
   // If you need to send large data, use regular HTTP endpoints
   // and notify via SignalR that data is ready
   await Clients.Group(groupName).SendAsync("VideoCompleted", new
   {
       RequestId = requestId,
       DownloadUrl = $"/api/videos/{requestId}/download" // Reference, not data
   });
   ```

3. **Connection Limits**
   ```csharp
   services.AddSignalR(options =>
   {
       options.MaximumReceiveMessageSize = 64 * 1024; // 64KB limit
       options.StreamBufferCapacity = 10;
   });
   ```

### Slow Message Delivery

**Symptoms:**
- Delayed message arrival
- Messages arrive out of order
- Poor real-time experience

**Solutions:**

1. **Optimize Message Batching**
   ```csharp
   public class SpendNotificationBatcher
   {
       private readonly ConcurrentDictionary<string, SpendUpdate> _pendingUpdates;
       private readonly Timer _flushTimer;
       
       public SpendNotificationBatcher()
       {
           _pendingUpdates = new ConcurrentDictionary<string, SpendUpdate>();
           // Flush every 100ms for good real-time feel
           _flushTimer = new Timer(FlushBatch, null, 
               TimeSpan.FromMilliseconds(100), 
               TimeSpan.FromMilliseconds(100));
       }
       
       public void QueueUpdate(string virtualKeyId, decimal delta)
       {
           _pendingUpdates.AddOrUpdate(virtualKeyId,
               new SpendUpdate { VirtualKeyId = virtualKeyId, Delta = delta },
               (key, existing) => 
               {
                   existing.Delta += delta;
                   existing.LastUpdate = DateTime.UtcNow;
                   return existing;
               });
       }
   }
   ```

2. **Use MessagePack Protocol**
   ```csharp
   services.AddSignalR()
       .AddMessagePackProtocol(); // More efficient than JSON
   ```
   
   ```javascript
   const connection = new signalR.HubConnectionBuilder()
       .withUrl(hubUrl)
       .withHubProtocol(new signalR.protocols.msgpack.MessagePackHubProtocol())
       .build();
   ```

3. **Monitor Connection Quality**
   ```javascript
   let messageCount = 0;
   let startTime = Date.now();
   
   connection.on('VideoProgress', (data) => {
       messageCount++;
       const elapsed = Date.now() - startTime;
       const messagesPerSecond = messageCount / (elapsed / 1000);
       
       if (messagesPerSecond < expectedRate) {
           console.warn(`Low message rate: ${messagesPerSecond.toFixed(2)} msg/sec`);
       }
   });
   ```

## Redis Backplane Issues

### Redis Connection Problems

**Symptoms:**
- Messages only reach clients connected to the same server instance
- Redis connection timeouts
- Intermittent message delivery

**Diagnostic Steps:**

1. **Verify Redis Configuration**
   ```csharp
   services.AddStackExchangeRedisCache(options =>
   {
       options.Configuration = "localhost:6379";
       options.InstanceName = "ConduitSignalR";
   });
   
   services.AddSignalR()
       .AddStackExchangeRedis("localhost:6379", options =>
       {
           options.Configuration.ChannelPrefix = "conduit-signalr";
       });
   ```

2. **Test Redis Connectivity**
   ```bash
   # Test Redis connection
   redis-cli ping
   # Should return PONG
   
   # Monitor Redis traffic
   redis-cli monitor
   # Should show SignalR messages when they're sent
   ```

3. **Check Redis Logs**
   ```csharp
   // Add detailed logging
   services.AddLogging(builder =>
   {
       builder.AddConsole()
              .SetMinimumLevel(LogLevel.Debug);
   });
   
   // Monitor for Redis errors
   services.Configure<StackExchangeRedisSignalROptions>(options =>
   {
       options.Configuration.ConfigurationChannel = "__keyevent@0__:expired";
   });
   ```

### Multi-Instance Message Delivery

**Problem:** Messages sent from one server instance don't reach clients on other instances.

**Solution:**

1. **Verify Backplane Setup**
   ```csharp
   // Program.cs - Ensure this is configured on ALL instances
   services.AddSignalR()
       .AddStackExchangeRedis(connectionString, options =>
       {
           options.Configuration.ChannelPrefix = "conduit-signalr";
           options.Configuration.ClientName = Environment.MachineName;
       });
   ```

2. **Test Cross-Instance Delivery**
   ```csharp
   [ApiController]
   [Route("api/test")]
   public class SignalRTestController : ControllerBase
   {
       private readonly IHubContext<VideoGenerationHub> _hubContext;
       
       [HttpPost("broadcast")]
       public async Task<IActionResult> TestBroadcast([FromBody] string message)
       {
           // This should reach clients on all instances
           await _hubContext.Clients.All.SendAsync("TestMessage", 
               $"Broadcast from {Environment.MachineName}: {message}");
           return Ok();
       }
   }
   ```

3. **Monitor Redis Traffic**
   ```bash
   # Watch SignalR messages in Redis
   redis-cli psubscribe "*signalr*"
   ```

## Testing and Debugging

### Enable Debug Logging

**Server-Side:**
```csharp
services.AddLogging(builder =>
{
    builder.AddConsole()
           .AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug)
           .AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);
});
```

**Client-Side:**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .configureLogging(signalR.LogLevel.Debug) // Enable debug logging
    .build();
```

### Connection State Monitoring

```javascript
class SignalRMonitor {
    constructor(connection) {
        this.connection = connection;
        this.stats = {
            connectTime: null,
            reconnectCount: 0,
            messagesSent: 0,
            messagesReceived: 0,
            lastActivity: null
        };
        
        this.setupMonitoring();
    }
    
    setupMonitoring() {
        this.connection.onclose(() => {
            console.log('Connection closed');
            this.logStats();
        });
        
        this.connection.onreconnecting(() => {
            this.stats.reconnectCount++;
            console.log(`Reconnection attempt #${this.stats.reconnectCount}`);
        });
        
        this.connection.onreconnected(() => {
            console.log('Reconnected successfully');
        });
        
        // Monitor all incoming messages
        const originalOn = this.connection.on;
        this.connection.on = (methodName, handler) => {
            return originalOn.call(this.connection, methodName, (...args) => {
                this.stats.messagesReceived++;
                this.stats.lastActivity = new Date();
                console.log(`Received ${methodName}:`, args);
                return handler(...args);
            });
        };
        
        // Monitor all outgoing messages
        const originalInvoke = this.connection.invoke;
        this.connection.invoke = (methodName, ...args) => {
            this.stats.messagesSent++;
            this.stats.lastActivity = new Date();
            console.log(`Sending ${methodName}:`, args);
            return originalInvoke.call(this.connection, methodName, ...args);
        };
    }
    
    logStats() {
        console.table(this.stats);
    }
    
    getConnectionState() {
        return {
            state: this.connection.state,
            connectionId: this.connection.connectionId,
            stats: this.stats
        };
    }
}

// Usage
const monitor = new SignalRMonitor(connection);
await connection.start();

// Check stats anytime
console.log(monitor.getConnectionState());
```

### Load Testing SignalR

```csharp
[Test]
public async Task LoadTest_1000Connections()
{
    const int connectionCount = 1000;
    const int messagesPerConnection = 100;
    
    var connections = new List<HubConnection>();
    var messagesReceived = new ConcurrentBag<string>();
    
    try
    {
        // Create connections
        for (int i = 0; i < connectionCount; i++)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/hubs/test")
                .Build();
                
            connection.On<string>("TestMessage", message =>
            {
                messagesReceived.Add(message);
            });
            
            await connection.StartAsync();
            connections.Add(connection);
        }
        
        // Send messages
        var tasks = connections.Select(async (connection, index) =>
        {
            for (int i = 0; i < messagesPerConnection; i++)
            {
                await connection.InvokeAsync("SendMessage", $"Message {i} from connection {index}");
            }
        });
        
        await Task.WhenAll(tasks);
        
        // Wait for message delivery
        await Task.Delay(5000);
        
        // Verify results
        var expectedMessages = connectionCount * messagesPerConnection;
        Assert.That(messagesReceived.Count, Is.EqualTo(expectedMessages));
    }
    finally
    {
        // Cleanup
        await Task.WhenAll(connections.Select(c => c.DisposeAsync().AsTask()));
    }
}
```

### Event Flow Verification

```csharp
public class SignalREventFlowTest
{
    [Test]
    public async Task VideoGeneration_EventFlow_CompletesSuccessfully()
    {
        // Arrange
        using var app = CreateTestApp();
        var client = CreateSignalRClient(app);
        
        var events = new List<string>();
        
        client.On<object>("VideoStarted", data => events.Add("Started"));
        client.On<object>("VideoProgress", data => events.Add("Progress"));
        client.On<object>("VideoCompleted", data => events.Add("Completed"));
        
        await client.StartAsync();
        await client.InvokeAsync("SubscribeToVideoGeneration", "test-request");
        
        // Act - Trigger video generation workflow
        var videoService = app.Services.GetRequiredService<IVideoGenerationService>();
        await videoService.GenerateVideoAsync("test-request");
        
        // Wait for all events
        await WaitForCondition(() => events.Contains("Completed"), TimeSpan.FromSeconds(30));
        
        // Assert
        Assert.That(events, Is.EqualTo(new[] { "Started", "Progress", "Completed" }));
    }
}
```

## Common Error Messages

### "Connection started reconnecting before invocation result was received"

**Cause:** Connection lost during a hub method invocation.

**Solution:**
```javascript
// Add retry logic for hub method calls
async function invokeWithRetry(connection, methodName, ...args) {
    const maxRetries = 3;
    let attempt = 0;
    
    while (attempt < maxRetries) {
        try {
            return await connection.invoke(methodName, ...args);
        } catch (error) {
            attempt++;
            if (attempt >= maxRetries) throw error;
            
            console.warn(`Hub method ${methodName} failed, retry ${attempt}/${maxRetries}`);
            await new Promise(resolve => setTimeout(resolve, 1000 * attempt));
        }
    }
}
```

### "Cannot send data if the connection is not in the 'Connected' state"

**Cause:** Attempting to send messages before connection is established or after it's closed.

**Solution:**
```javascript
class SafeSignalRConnection {
    constructor(hubUrl) {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .build();
            
        this.isConnected = false;
        this.messageQueue = [];
        
        this.connection.onclose(() => this.isConnected = false);
        this.connection.onreconnected(() => {
            this.isConnected = true;
            this.flushMessageQueue();
        });
    }
    
    async start() {
        await this.connection.start();
        this.isConnected = true;
        this.flushMessageQueue();
    }
    
    async safeInvoke(methodName, ...args) {
        if (this.isConnected) {
            return await this.connection.invoke(methodName, ...args);
        } else {
            // Queue the message for later
            this.messageQueue.push({ methodName, args });
            console.warn(`Connection not ready, queued message: ${methodName}`);
        }
    }
    
    flushMessageQueue() {
        while (this.messageQueue.length > 0) {
            const { methodName, args } = this.messageQueue.shift();
            this.connection.invoke(methodName, ...args)
                .catch(error => console.error(`Failed to send queued message ${methodName}:`, error));
        }
    }
}
```

### "Hub method not found"

**Cause:** Method name mismatch between client and server.

**Solution:**
```csharp
// Use explicit method names to avoid casing issues
public class VideoGenerationHub : Hub
{
    [HubMethodName("subscribeToVideo")]
    public async Task SubscribeToVideoGeneration(string requestId)
    {
        // Implementation
    }
}
```

```javascript
// Client calls explicit method name
await connection.invoke('subscribeToVideo', requestId);
```

## Performance Monitoring

### Key Metrics to Monitor

1. **Connection Count**
   - Active connections per instance
   - Connection churn rate
   - Failed connection attempts

2. **Message Throughput**
   - Messages per second
   - Message delivery latency
   - Failed message deliveries

3. **Memory Usage**
   - SignalR connection memory
   - Group membership overhead
   - Message buffering

4. **Redis Metrics** (if using backplane)
   - Redis connection pool utilization
   - Message queue depth
   - Redis memory usage

### Monitoring Implementation

```csharp
public class SignalRMetricsCollector : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SignalRMetricsCollector> _logger;
    private Timer _metricsTimer;
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _metricsTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }
    
    private async void CollectMetrics(object state)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<VideoGenerationHub>>();
        
        // Collect connection metrics
        var connectionCount = GetActiveConnectionCount();
        var memoryUsage = GC.GetTotalMemory(false);
        
        _logger.LogInformation("SignalR Metrics - Connections: {ConnectionCount}, Memory: {MemoryMB}MB",
            connectionCount, memoryUsage / 1024 / 1024);
    }
}
```

---

*This troubleshooting guide consolidates information from multiple SignalR debugging and error handling documents. For additional help, see [SignalR Implementation Guide](./implementation.md) and [Quick Reference](./quick-reference.md).*