---
sidebar_position: 1
title: Real-Time Communication Overview
description: Complete guide to real-time updates in Conduit using webhooks, SignalR, and polling
---

# Real-Time Communication Overview

Conduit provides multiple real-time communication methods to keep your applications updated with task progress, completion notifications, and system events. Choose the right approach based on your architecture, requirements, and technical constraints.

## Communication Methods

Conduit supports three primary real-time communication patterns:

- **Webhooks**: HTTP callbacks for server-to-server communication
- **SignalR**: WebSocket-based real-time updates for web applications
- **Polling**: Simple HTTP requests for basic integrations

## Decision Matrix

Use this matrix to choose the best communication method for your use case:

| Use Case | Primary Method | Alternative | Reason |
|----------|---------------|-------------|---------|
| **Web Dashboard** | SignalR | Polling | Real-time UI updates, WebSocket efficiency |
| **Mobile Applications** | Webhooks | Polling | Push notifications, battery optimization |
| **Server-to-Server** | Webhooks | Polling | Reliable delivery, retry mechanisms |
| **Development/Testing** | Polling | SignalR | Simple implementation, no infrastructure |
| **High-Frequency Updates** | SignalR | Webhooks | Low latency, persistent connections |
| **Batch Processing** | Webhooks | Polling | Efficient for bulk operations |
| **Legacy Systems** | Polling | Webhooks | Compatible with older architectures |
| **Microservices** | Webhooks | SignalR | Service decoupling, scalability |

## Architecture Overview

### Event Flow

```
Conduit Core API → Event Bus → Real-Time Dispatcher → Multiple Channels
                                        ↓
                   ┌─────────────────────┼─────────────────────┐
                   ↓                     ↓                     ↓
               Webhooks              SignalR               Polling
                   ↓                     ↓                     ↓
            External APIs         Web Applications      Any HTTP Client
```

### Supported Events

All communication methods support the same event types:

| Event Category | Event Types | Description |
|---------------|-------------|-------------|
| **Image Generation** | `image.started`, `image.completed`, `image.failed` | Async image generation events |
| **Video Generation** | `video.started`, `video.progress`, `video.completed`, `video.failed` | Async video generation events |
| **Audio Processing** | `audio.transcription.completed`, `audio.synthesis.completed` | Audio processing events |
| **Virtual Key Events** | `key.disabled`, `key.budget.exceeded`, `key.rate_limited` | Virtual key status changes |
| **System Events** | `provider.health_changed`, `system.maintenance` | System-wide notifications |

## Quick Start Examples

### Webhooks (Recommended for Production)

```javascript
// Configure webhook endpoint
app.post('/webhooks/conduit', (req, res) => {
  const event = req.body;
  
  switch (event.type) {
    case 'image.completed':
      console.log('Image generated:', event.data.imageUrl);
      // Process completed image
      break;
    case 'video.progress':
      console.log('Video progress:', event.data.progress);
      // Update progress bar
      break;
  }
  
  res.status(200).send('OK');
});

// Start async image generation with webhook
const response = await fetch('https://api.conduit.yourdomain.com/v1/images/generations', {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    prompt: 'A beautiful sunset over mountains',
    model: 'dall-e-3',
    async: true,
    webhook_url: 'https://yourapp.com/webhooks/conduit'
  })
});

const task = await response.json();
console.log('Task started:', task.task_id);
```

### SignalR (Recommended for Web UIs)

```javascript
import { HubConnectionBuilder } from '@microsoft/signalr';

// Connect to SignalR hub
const connection = new HubConnectionBuilder()
  .withUrl('https://api.conduit.yourdomain.com/hubs/image-generation', {
    accessTokenFactory: () => 'condt_your_virtual_key'
  })
  .build();

// Listen for image generation events
connection.on('ImageGenerationStarted', (data) => {
  console.log('Generation started:', data.taskId);
  showProgressIndicator(data.taskId);
});

connection.on('ImageGenerationCompleted', (data) => {
  console.log('Image completed:', data.imageUrl);
  displayImage(data.imageUrl);
  hideProgressIndicator(data.taskId);
});

connection.on('ImageGenerationFailed', (data) => {
  console.log('Generation failed:', data.error);
  showError(data.error);
  hideProgressIndicator(data.taskId);
});

// Start connection
await connection.start();

// Start async image generation (automatically sends updates via SignalR)
const response = await fetch('https://api.conduit.yourdomain.com/v1/images/generations', {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    prompt: 'A beautiful sunset over mountains',
    model: 'dall-e-3',
    async: true
  })
});
```

### Polling (Simple Integration)

```javascript
class TaskPoller {
  constructor(apiKey, baseUrl) {
    this.apiKey = apiKey;
    this.baseUrl = baseUrl;
    this.activeTasks = new Set();
  }

  async startImageGeneration(prompt, options = {}) {
    const response = await fetch(`${this.baseUrl}/v1/images/generations`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        prompt,
        async: true,
        ...options
      })
    });

    const task = await response.json();
    this.activeTasks.add(task.task_id);
    this.pollTask(task.task_id);
    
    return task.task_id;
  }

  async pollTask(taskId) {
    const pollInterval = 2000; // 2 seconds
    
    const poll = async () => {
      try {
        const response = await fetch(`${this.baseUrl}/v1/tasks/${taskId}`, {
          headers: {
            'Authorization': `Bearer ${this.apiKey}`
          }
        });

        const task = await response.json();
        
        switch (task.status) {
          case 'completed':
            this.onTaskCompleted(task);
            this.activeTasks.delete(taskId);
            return;
          case 'failed':
            this.onTaskFailed(task);
            this.activeTasks.delete(taskId);
            return;
          case 'processing':
            this.onTaskProgress(task);
            break;
        }
        
        // Continue polling
        setTimeout(poll, pollInterval);
      } catch (error) {
        console.error('Polling error:', error);
        setTimeout(poll, pollInterval * 2); // Backoff on error
      }
    };

    poll();
  }

  onTaskCompleted(task) {
    console.log('Task completed:', task.result);
  }

  onTaskFailed(task) {
    console.log('Task failed:', task.error);
  }

  onTaskProgress(task) {
    console.log('Task progress:', task.progress);
  }
}

// Usage
const poller = new TaskPoller('condt_your_virtual_key', 'https://api.conduit.yourdomain.com');
const taskId = await poller.startImageGeneration('A beautiful sunset over mountains');
```

## Method Comparison

### Performance Characteristics

| Method | Latency | Throughput | Resource Usage | Complexity |
|--------|---------|------------|----------------|------------|
| **SignalR** | 50-200ms | Very High | Medium | Medium |
| **Webhooks** | 100-500ms | High | Low | Medium |
| **Polling** | 2-30s | Low | High | Low |

### Reliability Features

| Feature | SignalR | Webhooks | Polling |
|---------|---------|----------|---------|
| **Guaranteed Delivery** | No | Yes | Yes |
| **Automatic Retry** | Yes | Yes | Manual |
| **Message Ordering** | Yes | Yes | No |
| **Connection Recovery** | Yes | N/A | N/A |
| **Backpressure Handling** | Yes | Yes | Manual |

### Scalability Considerations

| Aspect | SignalR | Webhooks | Polling |
|--------|---------|----------|---------|
| **Horizontal Scaling** | Redis Backplane | Load Balancer | Any |
| **Connection Limits** | 10,000+ per instance | Unlimited | Unlimited |
| **Memory Usage** | Per-connection | Per-request | Minimal |
| **Network Efficiency** | High | Medium | Low |

## Use Case Scenarios

### Real-Time Dashboard

**Recommended: SignalR**

```javascript
class RealTimeDashboard {
  constructor() {
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/navigation-state', {
        accessTokenFactory: () => this.getAuthToken()
      })
      .build();
    
    this.setupEventHandlers();
  }

  setupEventHandlers() {
    // Provider health updates
    this.connection.on('ProviderHealthChanged', (data) => {
      this.updateProviderStatus(data.provider, data.status);
    });

    // Real-time usage metrics
    this.connection.on('UsageMetricsUpdated', (data) => {
      this.updateUsageCharts(data.metrics);
    });

    // Task completion notifications
    this.connection.on('TaskCompleted', (data) => {
      this.showTaskNotification(data);
    });
  }

  async start() {
    await this.connection.start();
    console.log('Dashboard connected to real-time updates');
  }
}
```

### Mobile Application

**Recommended: Webhooks → Push Notifications**

```javascript
// Server-side webhook handler
app.post('/webhooks/conduit', async (req, res) => {
  const event = req.body;
  
  if (event.type === 'image.completed') {
    // Send push notification to mobile app
    await sendPushNotification({
      userId: event.data.userId,
      title: 'Image Generated',
      message: 'Your AI-generated image is ready!',
      data: {
        imageUrl: event.data.imageUrl,
        taskId: event.data.taskId
      }
    });
  }
  
  res.status(200).send('OK');
});

// Mobile app push notification handler
function handlePushNotification(notification) {
  if (notification.data.imageUrl) {
    // Download and display image
    showGeneratedImage(notification.data.imageUrl);
  }
}
```

### Batch Processing System

**Recommended: Webhooks**

```javascript
class BatchProcessor {
  constructor() {
    this.pendingTasks = new Map();
    this.completedTasks = new Map();
  }

  async processBatch(prompts) {
    const tasks = [];
    
    // Start all generations
    for (const prompt of prompts) {
      const response = await fetch('/v1/images/generations', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${this.apiKey}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          prompt,
          async: true,
          webhook_url: 'https://yourapp.com/webhooks/conduit'
        })
      });

      const task = await response.json();
      tasks.push(task.task_id);
      this.pendingTasks.set(task.task_id, prompt);
    }

    return tasks;
  }

  handleWebhook(event) {
    const taskId = event.data.taskId;
    
    if (event.type === 'image.completed') {
      const prompt = this.pendingTasks.get(taskId);
      this.completedTasks.set(taskId, event.data);
      this.pendingTasks.delete(taskId);
      
      console.log(`Completed: ${prompt} → ${event.data.imageUrl}`);
      
      // Check if batch is complete
      if (this.pendingTasks.size === 0) {
        this.onBatchComplete();
      }
    }
  }

  onBatchComplete() {
    console.log('Batch processing complete!');
    console.log('Results:', Array.from(this.completedTasks.values()));
  }
}
```

### Legacy System Integration

**Recommended: Polling**

```javascript
// Simple polling for systems that can't receive webhooks
class LegacyIntegration {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.checkInterval = 5000; // 5 seconds
  }

  async generateAndWait(prompt) {
    // Start generation
    const response = await this.makeRequest('/v1/images/generations', {
      prompt,
      async: true
    });

    const taskId = response.task_id;
    
    // Poll until complete
    return new Promise((resolve, reject) => {
      const check = async () => {
        try {
          const status = await this.makeRequest(`/v1/tasks/${taskId}`);
          
          if (status.status === 'completed') {
            resolve(status.result);
          } else if (status.status === 'failed') {
            reject(new Error(status.error));
          } else {
            setTimeout(check, this.checkInterval);
          }
        } catch (error) {
          reject(error);
        }
      };
      
      check();
    });
  }

  async makeRequest(path, body = null) {
    const options = {
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json'
      }
    };

    if (body) {
      options.method = 'POST';
      options.body = JSON.stringify(body);
    }

    const response = await fetch(`https://api.conduit.yourdomain.com${path}`, options);
    return await response.json();
  }
}
```

## Implementation Guidelines

### When to Use Each Method

**Use SignalR when:**
- Building interactive web applications
- Need real-time UI updates
- Users expect immediate feedback
- Handling multiple concurrent tasks
- Building dashboards or monitoring interfaces

**Use Webhooks when:**
- Building server-to-server integrations
- Need guaranteed delivery
- Processing batch operations
- Building mobile applications (via push notifications)
- Integration with external systems

**Use Polling when:**
- Simple integration requirements
- Working with legacy systems
- Can't receive incoming HTTP requests
- Development and testing
- Low-frequency updates are acceptable

### Hybrid Approaches

Many applications benefit from combining methods:

```javascript
class HybridNotificationSystem {
  constructor() {
    this.signalRConnection = this.setupSignalR();
    this.webhookFallback = true;
    this.pollingBackup = true;
  }

  async startTask(params) {
    const options = {
      ...params,
      async: true
    };

    // Try SignalR first for real-time updates
    if (this.signalRConnection.state === 'Connected') {
      return await this.startWithSignalR(options);
    }
    
    // Fallback to webhooks if available
    if (this.webhookFallback && this.webhookUrl) {
      options.webhook_url = this.webhookUrl;
      return await this.startWithWebhook(options);
    }
    
    // Final fallback to polling
    if (this.pollingBackup) {
      return await this.startWithPolling(options);
    }
    
    throw new Error('No communication method available');
  }
}
```

## Error Handling and Reliability

### Connection Recovery

```javascript
class ResilientConnection {
  constructor(hubUrl, accessToken) {
    this.hubUrl = hubUrl;
    this.accessToken = accessToken;
    this.reconnectAttempts = 0;
    this.maxReconnectAttempts = 10;
    this.setupConnection();
  }

  setupConnection() {
    this.connection = new HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => this.accessToken
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 2s, 4s, 8s, 16s, 30s max
          return Math.min(30000, Math.pow(2, retryContext.previousRetryCount) * 1000);
        }
      })
      .build();

    this.connection.onreconnecting((error) => {
      console.log('Connection lost, reconnecting...', error);
      this.onConnectionLost();
    });

    this.connection.onreconnected((connectionId) => {
      console.log('Reconnected:', connectionId);
      this.reconnectAttempts = 0;
      this.onConnectionRestored();
    });

    this.connection.onclose((error) => {
      console.log('Connection closed:', error);
      this.onConnectionClosed();
    });
  }

  onConnectionLost() {
    // Switch to polling as backup
    this.startPollingBackup();
  }

  onConnectionRestored() {
    // Stop polling backup
    this.stopPollingBackup();
  }

  startPollingBackup() {
    if (this.pollingInterval) return;
    
    this.pollingInterval = setInterval(() => {
      this.pollForUpdates();
    }, 5000);
  }

  stopPollingBackup() {
    if (this.pollingInterval) {
      clearInterval(this.pollingInterval);
      this.pollingInterval = null;
    }
  }
}
```

### Webhook Reliability

```javascript
// Webhook endpoint with reliability features
app.post('/webhooks/conduit', async (req, res) => {
  const event = req.body;
  const signature = req.headers['x-conduit-signature'];
  
  try {
    // Verify webhook signature
    if (!verifyWebhookSignature(event, signature)) {
      return res.status(401).send('Invalid signature');
    }

    // Idempotency check
    if (await isDuplicateEvent(event.id)) {
      return res.status(200).send('Already processed');
    }

    // Process event
    await processEvent(event);
    
    // Mark as processed
    await markEventProcessed(event.id);
    
    res.status(200).send('OK');
  } catch (error) {
    console.error('Webhook processing error:', error);
    
    // Return 500 to trigger retry
    res.status(500).send('Processing failed');
  }
});

function verifyWebhookSignature(payload, signature) {
  const expectedSignature = crypto
    .createHmac('sha256', process.env.WEBHOOK_SECRET)
    .update(JSON.stringify(payload))
    .digest('hex');
  
  return signature === `sha256=${expectedSignature}`;
}
```

## Security Considerations

### Virtual Key Authentication

All real-time communication methods support virtual key authentication:

```javascript
// SignalR with virtual key
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/image-generation', {
    accessTokenFactory: () => 'condt_your_virtual_key'
  })
  .build();

// Webhooks with signature verification
app.post('/webhooks', (req, res) => {
  const signature = req.headers['x-conduit-signature'];
  const isValid = verifySignature(req.body, signature, webhookSecret);
  
  if (!isValid) {
    return res.status(401).send('Unauthorized');
  }
  
  // Process webhook
});

// Polling with virtual key
const response = await fetch('/v1/tasks/task-id', {
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key'
  }
});
```

### Network Security

```javascript
// HTTPS enforcement
app.use((req, res, next) => {
  if (req.header('x-forwarded-proto') !== 'https') {
    res.redirect(`https://${req.header('host')}${req.url}`);
  } else {
    next();
  }
});

// Rate limiting for webhooks
const webhookLimiter = rateLimit({
  windowMs: 15 * 60 * 1000, // 15 minutes
  max: 1000, // Limit each IP to 1000 requests per windowMs
  message: 'Too many webhook requests'
});

app.use('/webhooks', webhookLimiter);
```

## Next Steps

- **Webhooks**: Implement reliable [webhook endpoints](webhooks)
- **SignalR**: Build real-time web applications with [SignalR integration](signalr)
- **Polling**: Set up simple [polling patterns](polling)
- **Media Generation**: Apply real-time updates to [async media generation](../media/async-processing)