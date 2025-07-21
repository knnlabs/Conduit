# Conduit Real-Time API Guide

## Overview

Conduit provides three methods for receiving updates about asynchronous image and video generation tasks:

1. **Polling** (Default) - Simple REST API polling
2. **Webhooks** - HTTP callbacks to your server
3. **SignalR** - WebSocket-based real-time updates

This guide helps you choose the right method and implement it effectively at production scale.

## Quick Decision Guide

| Use Case | Recommended Method | Why |
|----------|-------------------|-----|
| Quick prototypes | Polling | Simplest to implement |
| Server-to-server integration | Webhooks | Most scalable, no persistent connections |
| Web/mobile apps with real-time UI | SignalR | Best user experience, instant updates |
| High-volume production (1000+ requests/min) | Webhooks | Better resource utilization |
| Behind corporate firewall | Polling | No inbound connections required |
| Need progress updates | SignalR or Webhooks | Real-time progress notifications |

## Connection Limits

| Method | Rate Limits | Connection Limits |
|--------|------------|-------------------|
| Polling | Standard API rate limits apply | N/A |
| Webhooks | 1,000 deliveries/minute per virtual key | N/A |
| SignalR | 100 concurrent connections per virtual key | 10,000 total connections per instance |

## Method 1: Polling (Default)

The simplest approach - periodically check task status via REST API.

### When to Use Polling
- Development and testing
- Low-volume applications (<100 requests/minute)
- Simple integrations
- Corporate environments blocking WebSockets

### Implementation

```javascript
// JavaScript/Node.js
async function pollForCompletion(taskId, virtualKey) {
    const maxAttempts = 60; // 5 minutes with 5-second intervals
    let attempts = 0;
    
    while (attempts < maxAttempts) {
        const response = await fetch(`https://api.conduit.im/v1/videos/generations/${taskId}`, {
            headers: {
                'Authorization': `Bearer ${virtualKey}`
            }
        });
        
        const result = await response.json();
        
        if (result.status === 'completed') {
            return result;
        } else if (result.status === 'failed' || result.status === 'cancelled') {
            throw new Error(result.error || 'Task failed');
        }
        
        // Wait before next poll
        await new Promise(resolve => setTimeout(resolve, 5000));
        attempts++;
    }
    
    throw new Error('Polling timeout');
}
```

```python
# Python
import time
import requests

def poll_for_completion(task_id, virtual_key):
    max_attempts = 60  # 5 minutes with 5-second intervals
    attempts = 0
    
    while attempts < max_attempts:
        response = requests.get(
            f'https://api.conduit.im/v1/videos/generations/{task_id}',
            headers={'Authorization': f'Bearer {virtual_key}'}
        )
        
        result = response.json()
        
        if result['status'] == 'completed':
            return result
        elif result['status'] in ['failed', 'cancelled']:
            raise Exception(result.get('error', 'Task failed'))
        
        time.sleep(5)
        attempts += 1
    
    raise Exception('Polling timeout')
```

```csharp
// C#/.NET
public async Task<VideoGenerationResult> PollForCompletionAsync(
    string taskId, 
    string virtualKey,
    CancellationToken cancellationToken = default)
{
    const int maxAttempts = 60;
    int attempts = 0;
    
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", virtualKey);
    
    while (attempts < maxAttempts)
    {
        var response = await client.GetAsync(
            $"https://api.conduit.im/v1/videos/generations/{taskId}",
            cancellationToken);
        
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<VideoGenerationResult>(json);
        
        switch (result.Status)
        {
            case "completed":
                return result;
            case "failed":
            case "cancelled":
                throw new Exception(result.Error ?? "Task failed");
        }
        
        await Task.Delay(5000, cancellationToken);
        attempts++;
    }
    
    throw new TimeoutException("Polling timeout");
}
```

### Best Practices for Polling
1. **Use exponential backoff**: Start with 1s, increase to 5s, cap at 30s
2. **Set a maximum timeout**: Don't poll forever
3. **Handle rate limits**: Respect 429 responses
4. **Cache results**: Don't re-poll completed tasks

## Method 2: Webhooks

Receive HTTP POST callbacks when tasks complete or update.

### When to Use Webhooks
- Production applications
- High-volume processing (100-10,000 requests/minute)
- Server-side applications
- Need reliable delivery with retries

### Webhook Configuration

Include webhook details in your generation request:

```json
POST /v1/images/generations/async
{
    "model": "dall-e-3",
    "prompt": "A beautiful sunset",
    "webhook_url": "https://your-app.com/webhooks/conduit",
    "webhook_headers": {
        "Authorization": "Bearer your-webhook-secret",
        "X-Custom-Header": "custom-value"
    },
    "webhook_metadata": {
        "user_id": "12345",
        "session_id": "abc-def-ghi"
    }
}
```

### Webhook Payload Examples

#### Task Completed
```json
{
    "task_id": "550e8400-e29b-41d4-a716-446655440000",
    "status": "completed",
    "webhook_type": "image_generation_completed",
    "timestamp": "2024-01-20T15:30:45.123Z",
    "image_url": "https://cdn.conduit.im/images/generated-image.png",
    "generation_duration_seconds": 3.5,
    "model": "dall-e-3",
    "prompt": "A beautiful sunset",
    "metadata": {
        "user_id": "12345",
        "session_id": "abc-def-ghi"
    }
}
```

#### Task Failed
```json
{
    "task_id": "550e8400-e29b-41d4-a716-446655440000",
    "status": "failed",
    "webhook_type": "image_generation_completed",
    "timestamp": "2024-01-20T15:30:45.123Z",
    "error": "Content policy violation detected",
    "error_code": "content_policy_violation",
    "model": "dall-e-3",
    "prompt": "A beautiful sunset",
    "metadata": {
        "user_id": "12345",
        "session_id": "abc-def-ghi"
    }
}
```

#### Progress Update
```json
{
    "task_id": "550e8400-e29b-41d4-a716-446655440000",
    "status": "processing",
    "webhook_type": "video_generation_progress",
    "timestamp": "2024-01-20T15:29:30.123Z",
    "progress_percentage": 50,
    "message": "Rendering frames",
    "estimated_seconds_remaining": 30
}
```

### Webhook Handler Examples

#### Node.js/Express
```javascript
const express = require('express');
const crypto = require('crypto');

app.post('/webhooks/conduit', express.json(), async (req, res) => {
    // Verify webhook authenticity
    const expectedAuth = `Bearer ${process.env.WEBHOOK_SECRET}`;
    if (req.headers.authorization !== expectedAuth) {
        return res.status(401).send('Unauthorized');
    }
    
    // Process webhook asynchronously
    setImmediate(async () => {
        try {
            await processWebhook(req.body);
        } catch (error) {
            console.error('Webhook processing failed:', error);
        }
    });
    
    // Respond immediately
    res.status(200).send('OK');
});

async function processWebhook(payload) {
    const { task_id, status, webhook_type } = payload;
    
    switch (status) {
        case 'completed':
            if (webhook_type === 'image_generation_completed') {
                await handleImageCompleted(payload);
            } else if (webhook_type === 'video_generation_completed') {
                await handleVideoCompleted(payload);
            }
            break;
        
        case 'failed':
            await handleTaskFailed(payload);
            break;
        
        case 'processing':
            await updateProgressUI(payload);
            break;
    }
}
```

#### Python/FastAPI
```python
from fastapi import FastAPI, Header, HTTPException, BackgroundTasks
from pydantic import BaseModel
import os

app = FastAPI()

class WebhookPayload(BaseModel):
    task_id: str
    status: str
    webhook_type: str
    timestamp: str
    # ... other fields

@app.post("/webhooks/conduit")
async def handle_webhook(
    payload: WebhookPayload,
    background_tasks: BackgroundTasks,
    authorization: str = Header(None)
):
    # Verify webhook authenticity
    expected_auth = f"Bearer {os.environ['WEBHOOK_SECRET']}"
    if authorization != expected_auth:
        raise HTTPException(status_code=401, detail="Unauthorized")
    
    # Process asynchronously
    background_tasks.add_task(process_webhook, payload)
    
    # Respond immediately
    return {"status": "accepted"}

async def process_webhook(payload: WebhookPayload):
    if payload.status == "completed":
        if payload.webhook_type == "image_generation_completed":
            await handle_image_completed(payload)
        elif payload.webhook_type == "video_generation_completed":
            await handle_video_completed(payload)
    elif payload.status == "failed":
        await handle_task_failed(payload)
    elif payload.status == "processing":
        await update_progress_ui(payload)
```

#### C#/.NET
```csharp
[ApiController]
[Route("webhooks")]
public class WebhookController : ControllerBase
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IConfiguration _configuration;
    
    [HttpPost("conduit")]
    public async Task<IActionResult> HandleConduitWebhook(
        [FromBody] WebhookPayload payload,
        [FromHeader(Name = "Authorization")] string authorization)
    {
        // Verify webhook authenticity
        var expectedAuth = $"Bearer {_configuration["WebhookSecret"]}";
        if (authorization != expectedAuth)
        {
            return Unauthorized();
        }
        
        // Queue for background processing
        await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            await ProcessWebhookAsync(payload, token);
        });
        
        // Respond immediately
        return Ok(new { status = "accepted" });
    }
    
    private async Task ProcessWebhookAsync(
        WebhookPayload payload, 
        CancellationToken cancellationToken)
    {
        switch (payload.Status)
        {
            case "completed":
                await HandleCompletedAsync(payload, cancellationToken);
                break;
            case "failed":
                await HandleFailedAsync(payload, cancellationToken);
                break;
            case "processing":
                await HandleProgressAsync(payload, cancellationToken);
                break;
        }
    }
}
```

### Webhook Security Best Practices

1. **Use HTTPS**: Always use TLS-encrypted endpoints
2. **Authenticate Webhooks**: Include a secret token in headers
3. **Verify Timestamps**: Reject old webhooks to prevent replay attacks
4. **IP Allowlisting**: Restrict webhook sources if possible
5. **Idempotency**: Handle duplicate deliveries gracefully
6. **Quick Response**: Return 2xx within 5 seconds
7. **Async Processing**: Process webhook data in background

### Webhook Reliability

Conduit implements robust webhook delivery:
- **Retry Policy**: 3 attempts with exponential backoff (2s, 4s, 8s)
- **Circuit Breaker**: Stops attempting failed endpoints after 5 failures
- **Deduplication**: Prevents duplicate deliveries across retries
- **Timeout**: 10-second timeout per delivery attempt

## Method 3: SignalR (WebSockets)

Real-time bidirectional communication for instant updates.

### When to Use SignalR
- Web and mobile applications
- Real-time progress updates needed
- Low-latency requirements
- Interactive user interfaces

### Available Hubs

| Hub | Endpoint | Purpose | Authentication |
|-----|----------|---------|----------------|
| Image Generation | `wss://api.conduit.im/hubs/image-generation` | Image task updates | Required |
| Video Generation | `wss://api.conduit.im/hubs/video-generation` | Video task updates | Required |

### SignalR Client Examples

#### JavaScript/TypeScript
```javascript
import * as signalR from '@microsoft/signalr';

class ConduitRealtimeClient {
    constructor(virtualKey) {
        this.virtualKey = virtualKey;
        this.connection = null;
    }
    
    async connect(hubUrl) {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => this.virtualKey
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    // Exponential backoff: 0s, 2s, 4s, 8s, 16s, 30s, 30s...
                    if (retryContext.previousRetryCount === 0) return 0;
                    if (retryContext.previousRetryCount === 1) return 2000;
                    if (retryContext.previousRetryCount === 2) return 4000;
                    if (retryContext.previousRetryCount === 3) return 8000;
                    if (retryContext.previousRetryCount === 4) return 16000;
                    return 30000;
                }
            })
            .configureLogging(signalR.LogLevel.Information)
            .build();
        
        // Set up event handlers
        this.setupEventHandlers();
        
        // Handle connection lifecycle
        this.connection.onreconnecting(error => {
            console.log('Reconnecting...', error);
        });
        
        this.connection.onreconnected(connectionId => {
            console.log('Reconnected:', connectionId);
            // Re-subscribe to any active tasks
            this.resubscribeToActiveTasks();
        });
        
        this.connection.onclose(error => {
            console.error('Connection closed:', error);
        });
        
        // Start connection
        await this.connection.start();
        console.log('Connected to SignalR hub');
    }
    
    setupEventHandlers() {
        // Image generation events
        this.connection.on('TaskProgress', (taskId, progress) => {
            this.onProgress?.(taskId, progress);
        });
        
        this.connection.on('TaskCompleted', (taskId, result) => {
            this.onCompleted?.(taskId, result);
            this.unsubscribeFromTask(taskId);
        });
        
        this.connection.on('TaskFailed', (taskId, error) => {
            this.onFailed?.(taskId, error);
            this.unsubscribeFromTask(taskId);
        });
    }
    
    async subscribeToTask(taskId) {
        try {
            await this.connection.invoke('SubscribeToTask', taskId);
            this.activeTasks.add(taskId);
        } catch (error) {
            console.error('Failed to subscribe:', error);
            throw error;
        }
    }
    
    async unsubscribeFromTask(taskId) {
        try {
            await this.connection.invoke('UnsubscribeFromTask', taskId);
            this.activeTasks.delete(taskId);
        } catch (error) {
            console.error('Failed to unsubscribe:', error);
        }
    }
    
    async disconnect() {
        if (this.connection) {
            await this.connection.stop();
        }
    }
}

// Usage example
const client = new ConduitRealtimeClient('condt_your_virtual_key');

// Connect to image generation hub
await client.connect('wss://api.conduit.im/hubs/image-generation');

// Set up handlers
client.onProgress = (taskId, progress) => {
    console.log(`Task ${taskId}: ${progress}% complete`);
    updateProgressBar(taskId, progress);
};

client.onCompleted = (taskId, result) => {
    console.log(`Task ${taskId} completed:`, result);
    displayImage(result.imageUrl);
};

client.onFailed = (taskId, error) => {
    console.error(`Task ${taskId} failed:`, error);
    showError(error);
};

// Subscribe to a task
await client.subscribeToTask('550e8400-e29b-41d4-a716-446655440000');
```

#### Python
```python
import asyncio
import json
from signalrcore.hub_connection_builder import HubConnectionBuilder
import logging

class ConduitRealtimeClient:
    def __init__(self, virtual_key):
        self.virtual_key = virtual_key
        self.connection = None
        self.active_tasks = set()
        
    async def connect(self, hub_url):
        # Build connection with authentication
        self.connection = HubConnectionBuilder()\
            .with_url(hub_url, options={
                "headers": {
                    "Authorization": f"Bearer {self.virtual_key}"
                }
            })\
            .configure_logging(logging.INFO)\
            .with_automatic_reconnect({
                "type": "raw",
                "keep_alive_interval": 10,
                "reconnect_interval": 5,
                "max_attempts": 5
            })\
            .build()
        
        # Register event handlers
        self.connection.on("TaskProgress", self._on_progress)
        self.connection.on("TaskCompleted", self._on_completed)
        self.connection.on("TaskFailed", self._on_failed)
        
        # Start connection
        self.connection.start()
        
    def _on_progress(self, args):
        task_id, progress = args
        print(f"Task {task_id}: {progress}% complete")
        # Update your UI or state
        
    def _on_completed(self, args):
        task_id, result = args
        print(f"Task {task_id} completed:", result)
        self.active_tasks.discard(task_id)
        # Handle completion
        
    def _on_failed(self, args):
        task_id, error = args
        print(f"Task {task_id} failed:", error)
        self.active_tasks.discard(task_id)
        # Handle failure
        
    async def subscribe_to_task(self, task_id):
        self.connection.send("SubscribeToTask", [task_id])
        self.active_tasks.add(task_id)
        
    async def unsubscribe_from_task(self, task_id):
        self.connection.send("UnsubscribeFromTask", [task_id])
        self.active_tasks.discard(task_id)
        
    def disconnect(self):
        if self.connection:
            self.connection.stop()

# Usage
async def main():
    client = ConduitRealtimeClient("condt_your_virtual_key")
    
    # Connect to video generation hub
    await client.connect("wss://api.conduit.im/hubs/video-generation")
    
    # Subscribe to a task
    await client.subscribe_to_task("550e8400-e29b-41d4-a716-446655440000")
    
    # Keep connection alive
    try:
        await asyncio.Event().wait()
    except KeyboardInterrupt:
        client.disconnect()

if __name__ == "__main__":
    asyncio.run(main())
```

#### C#/.NET
```csharp
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

public class ConduitRealtimeClient : IAsyncDisposable
{
    private readonly string _virtualKey;
    private HubConnection? _connection;
    private readonly HashSet<string> _activeTasks = new();
    private readonly ILogger<ConduitRealtimeClient> _logger;
    
    public event Action<string, int>? OnProgress;
    public event Action<string, object>? OnCompleted;
    public event Action<string, string>? OnFailed;
    
    public ConduitRealtimeClient(string virtualKey, ILogger<ConduitRealtimeClient> logger)
    {
        _virtualKey = virtualKey;
        _logger = logger;
    }
    
    public async Task ConnectAsync(string hubUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Headers.Add("Authorization", $"Bearer {_virtualKey}");
            })
            .WithAutomaticReconnect(new[] { 
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4),
                TimeSpan.FromSeconds(8),
                TimeSpan.FromSeconds(16),
                TimeSpan.FromSeconds(30)
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();
        
        // Register event handlers
        _connection.On<string, int>("TaskProgress", (taskId, progress) =>
        {
            _logger.LogInformation("Task {TaskId}: {Progress}% complete", taskId, progress);
            OnProgress?.Invoke(taskId, progress);
        });
        
        _connection.On<string, object>("TaskCompleted", (taskId, result) =>
        {
            _logger.LogInformation("Task {TaskId} completed", taskId);
            OnCompleted?.Invoke(taskId, result);
            _activeTasks.Remove(taskId);
        });
        
        _connection.On<string, string>("TaskFailed", (taskId, error) =>
        {
            _logger.LogError("Task {TaskId} failed: {Error}", taskId, error);
            OnFailed?.Invoke(taskId, error);
            _activeTasks.Remove(taskId);
        });
        
        // Handle reconnection
        _connection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "Connection lost. Attempting to reconnect...");
            return Task.CompletedTask;
        };
        
        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Reconnected with ID: {ConnectionId}", connectionId);
            return ResubscribeToActiveTasksAsync();
        };
        
        _connection.Closed += error =>
        {
            _logger.LogError(error, "Connection closed");
            return Task.CompletedTask;
        };
        
        // Start connection
        await _connection.StartAsync();
        _logger.LogInformation("Connected to SignalR hub");
    }
    
    public async Task SubscribeToTaskAsync(string taskId)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to hub");
        }
        
        try
        {
            await _connection.InvokeAsync("SubscribeToTask", taskId);
            _activeTasks.Add(taskId);
            _logger.LogInformation("Subscribed to task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to task {TaskId}", taskId);
            throw;
        }
    }
    
    public async Task UnsubscribeFromTaskAsync(string taskId)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            return;
        }
        
        try
        {
            await _connection.InvokeAsync("UnsubscribeFromTask", taskId);
            _activeTasks.Remove(taskId);
            _logger.LogInformation("Unsubscribed from task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from task {TaskId}", taskId);
        }
    }
    
    private async Task ResubscribeToActiveTasksAsync()
    {
        foreach (var taskId in _activeTasks.ToList())
        {
            try
            {
                await _connection!.InvokeAsync("SubscribeToTask", taskId);
                _logger.LogInformation("Re-subscribed to task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-subscribe to task {TaskId}", taskId);
                _activeTasks.Remove(taskId);
            }
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}

// Usage example
public class VideoGenerationService
{
    private readonly ConduitRealtimeClient _realtimeClient;
    private readonly HttpClient _httpClient;
    
    public async Task<string> GenerateVideoAsync(string prompt)
    {
        // 1. Start video generation
        var response = await _httpClient.PostAsJsonAsync(
            "https://api.conduit.im/v1/videos/generations/async",
            new
            {
                model = "minimax-video",
                prompt = prompt,
                duration = 6,
                size = "1280x720"
            });
        
        var result = await response.Content.ReadFromJsonAsync<GenerationResponse>();
        var taskId = result.TaskId;
        
        // 2. Subscribe to real-time updates
        await _realtimeClient.SubscribeToTaskAsync(taskId);
        
        // 3. Updates will be received via events
        return taskId;
    }
}
```

### SignalR Connection Best Practices

1. **Connection Management**
   - Reuse connections for multiple subscriptions
   - Implement automatic reconnection with exponential backoff
   - Clean up subscriptions when no longer needed
   - Monitor connection state

2. **Error Handling**
   - Handle authentication failures (401/403)
   - Implement reconnection logic for network issues
   - Log all errors for debugging
   - Gracefully degrade to polling if SignalR fails

3. **Performance Optimization**
   - Limit concurrent subscriptions per connection
   - Unsubscribe from completed tasks
   - Use connection pooling for multiple users
   - Monitor memory usage with many connections

4. **Security**
   - Always use WSS (WebSocket Secure) in production
   - Rotate virtual keys regularly
   - Monitor for abnormal connection patterns
   - Implement client-side rate limiting

## Production Considerations

### Scalability by Method

| Method | Scalability | Resource Usage | Complexity |
|--------|-------------|----------------|------------|
| Polling | Limited (wastes resources) | High (constant requests) | Low |
| Webhooks | Excellent (fire-and-forget) | Low (event-driven) | Medium |
| SignalR | Good (with Redis backplane) | Medium (persistent connections) | High |

### High-Volume Recommendations (1000+ requests/minute)

1. **Use Webhooks as Primary Method**
   - Most efficient resource utilization
   - No persistent connections to manage
   - Built-in retry and circuit breaker patterns
   - Scales horizontally without coordination

2. **SignalR for User-Facing Features Only**
   - Reserve for real-time UI updates
   - Implement connection limits per user
   - Use Redis backplane for multiple instances
   - Monitor connection pool health

3. **Polling as Fallback**
   - Implement for webhook failures
   - Use for debugging and testing
   - Longer intervals in production (10-30s)

### Error Handling Strategies

#### Network Failures
```javascript
// Exponential backoff with jitter
function calculateBackoff(attempt) {
    const baseDelay = 1000; // 1 second
    const maxDelay = 30000; // 30 seconds
    const jitter = Math.random() * 1000; // 0-1s jitter
    
    const delay = Math.min(
        baseDelay * Math.pow(2, attempt) + jitter,
        maxDelay
    );
    
    return delay;
}
```

#### Circuit Breaker Pattern
```javascript
class CircuitBreaker {
    constructor(failureThreshold = 5, resetTimeout = 60000) {
        this.failureThreshold = failureThreshold;
        this.resetTimeout = resetTimeout;
        this.failures = 0;
        this.lastFailureTime = null;
        this.state = 'CLOSED'; // CLOSED, OPEN, HALF_OPEN
    }
    
    async execute(fn) {
        if (this.state === 'OPEN') {
            if (Date.now() - this.lastFailureTime > this.resetTimeout) {
                this.state = 'HALF_OPEN';
            } else {
                throw new Error('Circuit breaker is OPEN');
            }
        }
        
        try {
            const result = await fn();
            this.onSuccess();
            return result;
        } catch (error) {
            this.onFailure();
            throw error;
        }
    }
    
    onSuccess() {
        this.failures = 0;
        this.state = 'CLOSED';
    }
    
    onFailure() {
        this.failures++;
        this.lastFailureTime = Date.now();
        
        if (this.failures >= this.failureThreshold) {
            this.state = 'OPEN';
        }
    }
}
```

### Monitoring and Observability

#### Key Metrics to Track

1. **Polling**
   - Request rate and 429 responses
   - Average polling interval
   - Timeout rate

2. **Webhooks**
   - Delivery success rate
   - Retry rate
   - Average delivery latency
   - Circuit breaker trips

3. **SignalR**
   - Active connections
   - Connection duration
   - Reconnection rate
   - Message latency

#### Health Check Endpoints

```bash
# Check SignalR connection health
curl https://api.conduit.im/health/signalr

# Check webhook delivery health
curl https://api.conduit.im/health/webhooks
```

## Migration Guide

### From Polling to Webhooks

1. **Add webhook URL to existing requests**
   ```javascript
   // Before (polling only)
   const response = await createImageGeneration({
       model: 'dall-e-3',
       prompt: 'A sunset'
   });
   const taskId = response.task_id;
   pollForCompletion(taskId);
   
   // After (webhook with polling fallback)
   const response = await createImageGeneration({
       model: 'dall-e-3',
       prompt: 'A sunset',
       webhook_url: 'https://your-app.com/webhooks/conduit',
       webhook_headers: {
           'Authorization': 'Bearer webhook-secret'
       }
   });
   
   // Still poll as fallback
   setTimeout(() => pollForCompletion(response.task_id), 30000);
   ```

2. **Implement webhook handler**
3. **Test with both methods in parallel**
4. **Gradually reduce polling frequency**
5. **Monitor webhook delivery rates**

### From Polling to SignalR

1. **Keep polling as fallback**
2. **Implement SignalR connection management**
3. **Add reconnection logic**
4. **Test with unstable networks**
5. **Monitor connection stability**

## Security Best Practices

1. **Virtual Key Management**
   - Use separate keys for different environments
   - Rotate keys regularly
   - Monitor key usage patterns
   - Implement key revocation procedures

2. **Network Security**
   - Always use HTTPS/WSS
   - Implement request signing for webhooks
   - Use IP allowlisting where possible
   - Monitor for abnormal traffic patterns

3. **Data Protection**
   - Don't log sensitive data
   - Encrypt webhook payloads if needed
   - Implement audit trails
   - Use short-lived task IDs

## FAQ

### Q: Can I use multiple methods simultaneously?
A: Yes! We recommend webhooks as primary with SignalR for real-time UI and polling as fallback.

### Q: What happens if my webhook endpoint is down?
A: Conduit retries failed webhooks 3 times with exponential backoff. After that, you'll need to poll for results.

### Q: How many SignalR connections can I have?
A: 100 concurrent connections per virtual key, with a global limit of 10,000 connections per instance.

### Q: Do webhooks guarantee delivery order?
A: No. Webhooks may arrive out of order. Use timestamps to sequence events.

### Q: Can I filter which events I receive?
A: Currently, you receive all events for subscribed tasks. Event filtering is planned for future releases.

## Support

For additional help:
- API Reference: https://docs.conduit.im/api-reference
- Status Page: https://status.conduit.im
- Support: support@conduit.im