# Real-Time API Guide

Comprehensive guide to Conduit's real-time capabilities including polling, webhooks, and SignalR integration for live progress tracking and status updates.

## Overview

Conduit provides multiple real-time communication methods to keep your applications synchronized with long-running operations like image and video generation. This guide covers all available real-time features and their optimal usage patterns.

## Documentation Structure

The real-time API documentation has been organized by communication method and use case:

### 🔄 Communication Methods
- **[Polling Strategies](./realtime/polling-strategies.md)** - HTTP polling patterns and best practices
- **[Webhook Integration](./realtime/webhooks.md)** - Event-driven notifications and handlers
- **[SignalR WebSockets](./realtime/signalr-websockets.md)** - Real-time bidirectional communication

### 📡 SignalR Features
- **[Connection Management](./realtime/connection-management.md)** - Connection lifecycle and error handling
- **[Progress Tracking](./realtime/progress-tracking.md)** - Real-time progress updates
- **[Event Handling](./realtime/event-handling.md)** - Event types and message processing

### 🚀 Client Implementations
- **[JavaScript Client](./real-time/javascript-client.md)** - Complete TypeScript/JavaScript client
- **[Python Client](./real-time/python-client.md)** - Python implementation with AsyncIO
- **[C# Client](./real-time/csharp-client.md)** - .NET SignalR client integration

## Quick Start

### Polling for Task Status

```javascript
// Simple polling example
async function pollTaskStatus(taskId) {
  const maxAttempts = 60; // 5 minutes at 5-second intervals
  let attempts = 0;

  while (attempts < maxAttempts) {
    try {
      const response = await fetch(`/api/tasks/${taskId}/status`);
      const data = await response.json();

      if (data.status === 'completed') {
        return data.result;
      } else if (data.status === 'failed') {
        throw new Error(data.error);
      }

      // Wait 5 seconds before next poll
      await new Promise(resolve => setTimeout(resolve, 5000));
      attempts++;
    } catch (error) {
      console.error('Polling error:', error);
      attempts++;
    }
  }

  throw new Error('Task did not complete within timeout period');
}

// Usage
try {
  const result = await pollTaskStatus('task_123');
  console.log('Task completed:', result);
} catch (error) {
  console.error('Task failed or timed out:', error);
}
```

### Webhook Notifications

```javascript
// Express webhook handler
app.post('/webhooks/conduit', express.json(), (req, res) => {
  const { event_type, task_id, status, result, error } = req.body;

  switch (event_type) {
    case 'task.completed':
      console.log(`Task ${task_id} completed:`, result);
      // Update your application state
      updateTaskResult(task_id, result);
      break;

    case 'task.failed':
      console.error(`Task ${task_id} failed:`, error);
      // Handle failure
      handleTaskFailure(task_id, error);
      break;

    case 'task.progress':
      console.log(`Task ${task_id} progress: ${result.progress}%`);
      // Update progress UI
      updateTaskProgress(task_id, result.progress);
      break;
  }

  res.status(200).json({ received: true });
});
```

### SignalR Real-Time Connection

```javascript
import * as signalR from '@microsoft/signalr';

// Initialize SignalR connection
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/image-generation', {
    accessTokenFactory: () => virtualKey
  })
  .withAutomaticReconnect()
  .build();

// Handle events
connection.on('TaskProgress', (taskId, progress) => {
  console.log(`Task ${taskId}: ${progress}% complete`);
  updateProgressBar(taskId, progress);
});

connection.on('TaskCompleted', (taskId, result) => {
  console.log(`Task ${taskId} completed:`, result);
  displayResult(taskId, result);
});

connection.on('TaskFailed', (taskId, error) => {
  console.error(`Task ${taskId} failed:`, error);
  showError(taskId, error);
});

// Start connection
await connection.start();

// Subscribe to specific task
await connection.invoke('SubscribeToTask', taskId);
```

## Communication Methods Comparison

| Method | Latency | Reliability | Complexity | Best For |
|--------|---------|-------------|------------|----------|
| **Polling** | High (5-30s) | High | Low | Simple status checks |
| **Webhooks** | Low (< 1s) | Medium | Medium | Event-driven architectures |
| **SignalR** | Very Low (< 100ms) | High | High | Real-time progress tracking |

### When to Use Each Method

#### Polling
- **Best for**: Simple applications, infrequent updates, testing
- **Pros**: Simple to implement, works with any HTTP client
- **Cons**: Higher latency, more server load, less efficient

#### Webhooks  
- **Best for**: Event-driven architectures, microservices, batch processing
- **Pros**: Low latency, efficient, decoupled architecture
- **Cons**: Requires public endpoint, retry logic needed, security considerations

#### SignalR
- **Best for**: Real-time dashboards, progress tracking, interactive applications
- **Pros**: Very low latency, bidirectional, automatic reconnection
- **Cons**: More complex, requires WebSocket support, connection management

## Real-Time Architecture

### System Overview
```
┌─────────────────┐    WebSocket     ┌─────────────────┐
│   Client App    │ ←─────────────→  │  SignalR Hub    │
│                 │                  │                 │
│ - Subscribe     │                  │ - Image Hub     │
│ - Listen        │                  │ - Video Hub     │
│ - Display       │                  │ - Admin Hub     │
└─────────────────┘                  └─────────────────┘
        │                                    │
        │ HTTP API                           │ Events
        ↓                                    ↓
┌─────────────────┐    Events        ┌─────────────────┐
│   REST API      │ ←─────────────→  │  Event Bus      │
│                 │                  │                 │
│ - Create Task   │                  │ - Task Events   │
│ - Check Status  │                  │ - Progress      │
│ - Get Results   │                  │ - Completion    │
└─────────────────┘                  └─────────────────┘
```

### Event Flow
1. **Task Creation** - Client creates task via REST API
2. **Subscription** - Client subscribes to task updates via SignalR
3. **Processing** - Backend processes task and emits events
4. **Real-time Updates** - SignalR hub broadcasts events to subscribers
5. **Completion** - Final result delivered via both webhook and SignalR

## SignalR Hub Architecture

### Available Hubs

#### Image Generation Hub (`/hubs/image-generation`)
```javascript
// Methods
- SubscribeToTask(taskId)
- UnsubscribeFromTask(taskId)

// Events
- TaskProgress(taskId, progress)
- TaskCompleted(taskId, result)
- TaskFailed(taskId, error)
```

#### Video Generation Hub (`/hubs/video-generation`)
```javascript
// Methods  
- SubscribeToRequest(requestId)
- UnsubscribeFromRequest(requestId)

// Events
- RequestProgress(requestId, progress)
- RequestCompleted(requestId, result)
- RequestFailed(requestId, error)
```

#### Navigation State Hub (`/hubs/navigation-state`)
```javascript
// Events (WebUI specific)
- NavigationStateChanged(state)
- PageDataUpdated(pageData)
```

### Connection Authentication

```javascript
// Virtual key authentication
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/image-generation', {
    accessTokenFactory: () => 'vk_your_virtual_key_here'
  })
  .build();

// Alternative: Query string authentication
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/image-generation?access_token=vk_your_virtual_key_here')
  .build();
```

## Error Handling & Resilience

### Connection Error Handling

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/image-generation')
  .withAutomaticReconnect({
    nextRetryDelayInMilliseconds: retryContext => {
      // Exponential backoff: 0, 2, 10, 30 seconds, then 30 seconds
      return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
    }
  })
  .build();

// Handle connection events
connection.onreconnecting(error => {
  console.log('Connection lost, attempting to reconnect...', error);
  showConnectionStatus('reconnecting');
});

connection.onreconnected(connectionId => {
  console.log('Reconnected with ID:', connectionId);
  showConnectionStatus('connected');
  
  // Resubscribe to active tasks
  resubscribeToActiveTasks();
});

connection.onclose(error => {
  console.log('Connection closed:', error);
  showConnectionStatus('disconnected');
  
  // Attempt manual reconnection after delay
  setTimeout(() => {
    connection.start().catch(console.error);
  }, 5000);
});
```

### Fallback Strategies

```javascript
class RealTimeClient {
  constructor(options) {
    this.useSignalR = options.enableRealTime;
    this.fallbackToPoll = true;
    this.activeTasks = new Map();
  }

  async trackTask(taskId) {
    if (this.useSignalR && this.connection?.state === 'Connected') {
      // Primary: Use SignalR for real-time updates
      await this.connection.invoke('SubscribeToTask', taskId);
      this.activeTasks.set(taskId, 'signalr');
    } else {
      // Fallback: Use polling
      this.startPolling(taskId);
      this.activeTasks.set(taskId, 'polling');
    }
  }

  startPolling(taskId) {
    const interval = setInterval(async () => {
      try {
        const status = await this.checkTaskStatus(taskId);
        
        if (status.completed || status.failed) {
          clearInterval(interval);
          this.activeTasks.delete(taskId);
          this.emit('taskCompleted', taskId, status);
        } else {
          this.emit('taskProgress', taskId, status.progress);
        }
      } catch (error) {
        console.error(`Polling error for task ${taskId}:`, error);
      }
    }, 5000);
  }
}
```

## Performance Optimization

### Connection Pooling

```javascript
class SignalRConnectionPool {
  constructor() {
    this.connections = new Map();
    this.maxConnections = 5;
  }

  async getConnection(hubType, virtualKey) {
    const key = `${hubType}:${virtualKey}`;
    
    if (this.connections.has(key)) {
      return this.connections.get(key);
    }

    if (this.connections.size >= this.maxConnections) {
      // Remove oldest connection
      const firstKey = this.connections.keys().next().value;
      const oldConnection = this.connections.get(firstKey);
      await oldConnection.stop();
      this.connections.delete(firstKey);
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/${hubType}`, {
        accessTokenFactory: () => virtualKey
      })
      .withAutomaticReconnect()
      .build();

    await connection.start();
    this.connections.set(key, connection);
    
    return connection;
  }
}
```

### Efficient Event Handling

```javascript
// Debounced progress updates
const debouncedProgressUpdate = debounce((taskId, progress) => {
  updateUI(taskId, progress);
}, 100);

connection.on('TaskProgress', (taskId, progress) => {
  // Update internal state immediately
  taskStates.set(taskId, { progress, timestamp: Date.now() });
  
  // Debounce UI updates to prevent excessive rendering
  debouncedProgressUpdate(taskId, progress);
});

// Batch multiple events
const eventBatch = [];
const processBatch = () => {
  if (eventBatch.length > 0) {
    processEvents(eventBatch);
    eventBatch.length = 0;
  }
};

connection.on('TaskProgress', (taskId, progress) => {
  eventBatch.push({ type: 'progress', taskId, progress });
});

// Process batches every 50ms
setInterval(processBatch, 50);
```

## Security Considerations

### Virtual Key Security

```javascript
// ❌ Bad: Exposing virtual key in client-side code
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/image-generation', {
    accessTokenFactory: () => 'vk_exposed_key_here' // Never do this!
  })
  .build();

// ✅ Good: Get token from secure endpoint
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/image-generation', {
    accessTokenFactory: async () => {
      const response = await fetch('/api/auth/realtime-token');
      const data = await response.json();
      return data.token;
    }
  })
  .build();
```

### Webhook Security

```javascript
// Verify webhook signatures
const crypto = require('crypto');

function verifyWebhookSignature(payload, signature, secret) {
  const expectedSignature = crypto
    .createHmac('sha256', secret)
    .update(payload)
    .digest('hex');
  
  return crypto.timingSafeEqual(
    Buffer.from(signature),
    Buffer.from(expectedSignature)
  );
}

app.post('/webhooks/conduit', (req, res) => {
  const signature = req.headers['x-conduit-signature'];
  const payload = JSON.stringify(req.body);
  
  if (!verifyWebhookSignature(payload, signature, process.env.WEBHOOK_SECRET)) {
    return res.status(401).json({ error: 'Invalid signature' });
  }
  
  // Process webhook...
});
```

## Testing Real-Time Features

### Unit Testing SignalR

```javascript
// Mock SignalR connection for testing
const mockConnection = {
  state: 'Connected',
  invoke: jest.fn(),
  on: jest.fn(),
  start: jest.fn().mockResolvedValue(undefined),
  stop: jest.fn().mockResolvedValue(undefined),
};

jest.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: jest.fn(() => ({
    withUrl: jest.fn().mockReturnThis(),
    withAutomaticReconnect: jest.fn().mockReturnThis(),
    build: jest.fn(() => mockConnection),
  })),
}));

describe('RealTimeClient', () => {
  it('subscribes to task updates', async () => {
    const client = new RealTimeClient();
    await client.connect('image');
    await client.subscribeToTask('task_123');
    
    expect(mockConnection.invoke).toHaveBeenCalledWith('SubscribeToTask', 'task_123');
  });
});
```

### Integration Testing

```javascript
// Test webhook endpoints
describe('Webhook Handler', () => {
  it('processes task completion events', async () => {
    const webhookPayload = {
      event_type: 'task.completed',
      task_id: 'task_123',
      result: { image_url: 'https://example.com/image.jpg' }
    };

    const response = await request(app)
      .post('/webhooks/conduit')
      .send(webhookPayload)
      .expect(200);

    expect(response.body.received).toBe(true);
    
    // Verify task was updated in database
    const task = await getTask('task_123');
    expect(task.status).toBe('completed');
  });
});
```

## Best Practices

### Connection Management
- Always implement automatic reconnection
- Handle connection state changes gracefully
- Clean up subscriptions when components unmount
- Use connection pooling for multiple hub types

### Performance
- Debounce frequent updates (progress events)
- Batch multiple events when possible
- Limit the number of concurrent connections
- Implement proper cleanup to prevent memory leaks

### Reliability
- Implement fallback to polling when SignalR unavailable
- Add retry logic for failed operations
- Monitor connection health and metrics
- Handle partial failures gracefully

### Security
- Never expose virtual keys in client-side code
- Verify webhook signatures
- Implement proper authentication flows
- Use secure WebSocket connections (WSS) in production

## Related Documentation

- [JavaScript Client](./real-time/javascript-client.md) - Complete TypeScript client implementation
- [Integration Examples](./examples/INTEGRATION-EXAMPLES.md) - Real-world integration patterns
- [WebUI API Reference](./api-reference/webui-api-reference.md) - WebUI real-time features