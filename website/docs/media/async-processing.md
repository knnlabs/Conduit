---
sidebar_position: 4
title: Async Processing
description: Master asynchronous media generation with real-time updates, webhooks, and task management
---

# Async Processing

Conduit's async processing system handles long-running media generation tasks efficiently, providing real-time progress updates, reliable task management, and multiple notification methods to keep you informed throughout the generation process.

## Async Processing Overview

### Why Async Processing?

Media generation tasks can take anywhere from 30 seconds to 10 minutes depending on:
- **Model complexity** - DALL-E 3 vs. MiniMax Image
- **Resolution** - Higher resolution = longer processing time
- **Queue load** - Provider capacity and current demand
- **Content complexity** - Detailed prompts may take longer

### Task Lifecycle

```
Request → Validation → Queue → Processing → Storage → Notification
    ↓         ↓         ↓         ↓          ↓          ↓
  Task ID   Parameters  RabbitMQ  Provider    S3      Webhook/SignalR
```

**Task States:**
- `queued` - Task waiting for processing
- `processing` - Generation in progress
- `completed` - Generation successful
- `failed` - Generation failed
- `cancelled` - Task cancelled by user

## Basic Async Operations

### Starting Async Video Generation

```javascript
// Start async video generation
const response = await fetch('https://api.conduit.yourdomain.com/v1/videos/generations/async', {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    model: 'video-01',
    prompt: 'A futuristic cityscape at sunset with flying cars',
    resolution: '720x480',
    webhook_url: 'https://yourapp.com/webhooks/conduit' // Optional
  })
});

const task = await response.json();
console.log('Task started:', task.taskId);
console.log('Estimated completion:', task.estimatedCompletionTime);
```

### Checking Task Status

```javascript
async function checkTaskStatus(taskId) {
  const response = await fetch(`https://api.conduit.yourdomain.com/v1/videos/generations/tasks/${taskId}`, {
    headers: {
      'Authorization': 'Bearer condt_your_virtual_key'
    }
  });

  const task = await response.json();
  
  console.log('Task Status:', task.status);
  console.log('Progress:', task.progress || 0);
  
  if (task.status === 'completed') {
    console.log('Result:', task.videoResponse);
    return task.videoResponse;
  } else if (task.status === 'failed') {
    console.log('Error:', task.error);
    throw new Error(task.error);
  }
  
  return null; // Still processing
}

// Usage
const result = await checkTaskStatus(task.taskId);
if (result) {
  console.log('Video URL:', result.data[0].url);
}
```

### Polling for Completion

```javascript
class TaskPoller {
  constructor(apiKey, pollInterval = 2000) {
    this.apiKey = apiKey;
    this.pollInterval = pollInterval;
    this.activeTasks = new Map();
  }

  async waitForCompletion(taskId, onProgress = null) {
    return new Promise((resolve, reject) => {
      const poll = async () => {
        try {
          const response = await fetch(`https://api.conduit.yourdomain.com/v1/tasks/${taskId}`, {
            headers: {
              'Authorization': `Bearer ${this.apiKey}`
            }
          });

          const task = await response.json();
          
          // Call progress callback if provided
          if (onProgress) {
            onProgress(task);
          }

          switch (task.status) {
            case 'completed':
              this.activeTasks.delete(taskId);
              resolve(task.result);
              return;
              
            case 'failed':
              this.activeTasks.delete(taskId);
              reject(new Error(task.error));
              return;
              
            case 'cancelled':
              this.activeTasks.delete(taskId);
              reject(new Error('Task was cancelled'));
              return;
              
            default:
              // Continue polling
              setTimeout(poll, this.pollInterval);
          }
        } catch (error) {
          console.error('Polling error:', error);
          // Retry with exponential backoff
          setTimeout(poll, this.pollInterval * 2);
        }
      };

      this.activeTasks.set(taskId, { resolve, reject });
      poll();
    });
  }

  async waitForMultiple(taskIds, onProgress = null) {
    const promises = taskIds.map(taskId => 
      this.waitForCompletion(taskId, onProgress)
    );

    try {
      const results = await Promise.allSettled(promises);
      
      return results.map((result, index) => ({
        taskId: taskIds[index],
        success: result.status === 'fulfilled',
        result: result.status === 'fulfilled' ? result.value : null,
        error: result.status === 'rejected' ? result.reason.message : null
      }));
    } catch (error) {
      throw new Error('Multiple task polling failed: ' + error.message);
    }
  }

  cancelAllTasks() {
    this.activeTasks.forEach(({ reject }, taskId) => {
      reject(new Error('Polling cancelled'));
    });
    this.activeTasks.clear();
  }
}

// Usage
const poller = new TaskPoller('condt_your_virtual_key', 3000);

// Wait for single task with progress updates
const result = await poller.waitForCompletion(taskId, (task) => {
  console.log(`Progress: ${task.progress}% - Stage: ${task.stage}`);
});

console.log('Generation completed:', result.url);

// Wait for multiple tasks
const taskIds = ['task1', 'task2', 'task3'];
const results = await poller.waitForMultiple(taskIds, (task) => {
  console.log(`Task ${task.task_id}: ${task.progress}%`);
});

results.forEach(result => {
  if (result.success) {
    console.log(`${result.taskId}: ${result.result.url}`);
  } else {
    console.log(`${result.taskId}: Failed - ${result.error}`);
  }
});
```

## Webhook Integration

### Setting Up Webhook Endpoints

```javascript
import express from 'express';
import crypto from 'crypto';

const app = express();
app.use(express.json());

// Webhook signature verification
function verifyWebhookSignature(payload, signature, secret) {
  const expectedSignature = crypto
    .createHmac('sha256', secret)
    .update(JSON.stringify(payload))
    .digest('hex');
  
  return signature === `sha256=${expectedSignature}`;
}

// Webhook endpoint
app.post('/webhooks/conduit', (req, res) => {
  const signature = req.headers['x-conduit-signature'];
  const payload = req.body;

  // Verify signature (optional but recommended)
  if (!verifyWebhookSignature(payload, signature, process.env.WEBHOOK_SECRET)) {
    return res.status(401).send('Invalid signature');
  }

  // Process the event
  try {
    handleWebhookEvent(payload);
    res.status(200).send('OK');
  } catch (error) {
    console.error('Webhook processing error:', error);
    res.status(500).send('Processing failed');
  }
});

function handleWebhookEvent(event) {
  console.log('Received webhook:', event.type);

  switch (event.type) {
    case 'image.generation.started':
      handleImageGenerationStarted(event.data);
      break;
      
    case 'image.generation.progress':
      handleImageGenerationProgress(event.data);
      break;
      
    case 'image.generation.completed':
      handleImageGenerationCompleted(event.data);
      break;
      
    case 'image.generation.failed':
      handleImageGenerationFailed(event.data);
      break;
      
    case 'video.generation.started':
      handleVideoGenerationStarted(event.data);
      break;
      
    case 'video.generation.progress':
      handleVideoGenerationProgress(event.data);
      break;
      
    case 'video.generation.completed':
      handleVideoGenerationCompleted(event.data);
      break;
      
    case 'video.generation.failed':
      handleVideoGenerationFailed(event.data);
      break;
      
    default:
      console.log('Unknown event type:', event.type);
  }
}

function handleImageGenerationCompleted(data) {
  console.log(`Image completed: ${data.task_id}`);
  console.log(`URL: ${data.image_url}`);
  console.log(`Generation time: ${data.generation_time}ms`);

  // Update database
  updateTaskInDatabase(data.task_id, {
    status: 'completed',
    image_url: data.image_url,
    completed_at: new Date()
  });

  // Notify user (push notification, email, etc.)
  notifyUser(data.user_id, {
    type: 'image_ready',
    task_id: data.task_id,
    image_url: data.image_url
  });
}

function handleVideoGenerationProgress(data) {
  console.log(`Video progress: ${data.task_id} - ${data.progress}%`);
  
  // Update real-time dashboard
  updateProgressInDatabase(data.task_id, data.progress, data.stage);
  
  // Send to connected WebSocket clients
  notifyWebSocketClients(data.task_id, {
    progress: data.progress,
    stage: data.stage,
    estimated_time_remaining: data.estimated_time_remaining
  });
}

app.listen(3000, () => {
  console.log('Webhook server listening on port 3000');
});
```

### Webhook Retry and Reliability

```javascript
class WebhookHandler {
  constructor() {
    this.processedEvents = new Set(); // For idempotency
    this.retryAttempts = new Map(); // Track retry attempts
  }

  async handleWebhook(req, res) {
    const eventId = req.headers['x-conduit-event-id'];
    const signature = req.headers['x-conduit-signature'];
    const payload = req.body;

    try {
      // Idempotency check
      if (this.processedEvents.has(eventId)) {
        console.log(`Event ${eventId} already processed, skipping`);
        return res.status(200).send('Already processed');
      }

      // Verify signature
      if (!this.verifySignature(payload, signature)) {
        return res.status(401).send('Invalid signature');
      }

      // Process event
      await this.processEvent(payload);
      
      // Mark as processed
      this.processedEvents.add(eventId);
      
      // Clean up old events (prevent memory leak)
      if (this.processedEvents.size > 10000) {
        const eventsArray = Array.from(this.processedEvents);
        this.processedEvents = new Set(eventsArray.slice(-5000));
      }

      res.status(200).send('OK');
    } catch (error) {
      console.error('Webhook processing error:', error);
      
      // Track retry attempts
      const attempts = this.retryAttempts.get(eventId) || 0;
      this.retryAttempts.set(eventId, attempts + 1);

      // Return appropriate status code for retry logic
      if (attempts < 3) {
        res.status(500).send('Temporary error, please retry');
      } else {
        res.status(400).send('Permanent error, stop retrying');
        this.retryAttempts.delete(eventId);
      }
    }
  }

  async processEvent(event) {
    const handlers = {
      'image.generation.completed': this.handleImageCompleted.bind(this),
      'video.generation.completed': this.handleVideoCompleted.bind(this),
      'image.generation.failed': this.handleImageFailed.bind(this),
      'video.generation.failed': this.handleVideoFailed.bind(this)
    };

    const handler = handlers[event.type];
    if (handler) {
      await handler(event.data);
    } else {
      console.log(`No handler for event type: ${event.type}`);
    }
  }

  async handleImageCompleted(data) {
    try {
      // Update database
      await this.updateTaskStatus(data.task_id, 'completed', {
        image_url: data.image_url,
        file_size: data.file_size,
        generation_time: data.generation_time
      });

      // Send notification
      await this.sendNotification(data.user_id, {
        type: 'image_ready',
        task_id: data.task_id,
        image_url: data.image_url,
        title: 'Your image is ready!',
        message: 'Your AI-generated image has been created successfully.'
      });

      // Process any follow-up actions
      await this.processFollowUpActions(data.task_id);
      
    } catch (error) {
      console.error('Error handling image completion:', error);
      throw error; // Propagate for retry logic
    }
  }

  async updateTaskStatus(taskId, status, metadata = {}) {
    // Database update implementation
    console.log(`Updating task ${taskId} to status: ${status}`);
    // await database.tasks.update(taskId, { status, ...metadata });
  }

  async sendNotification(userId, notification) {
    // Notification service implementation
    console.log(`Sending notification to user ${userId}:`, notification);
    // await notificationService.send(userId, notification);
  }

  verifySignature(payload, signature) {
    const secret = process.env.WEBHOOK_SECRET;
    if (!secret) return true; // Skip verification if no secret set

    const expectedSignature = crypto
      .createHmac('sha256', secret)
      .update(JSON.stringify(payload))
      .digest('hex');
    
    return signature === `sha256=${expectedSignature}`;
  }
}

// Usage
const webhookHandler = new WebhookHandler();

app.post('/webhooks/conduit', (req, res) => {
  webhookHandler.handleWebhook(req, res);
});
```

## SignalR Real-Time Updates

### Client-Side SignalR Integration

```javascript
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

class MediaGenerationClient {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.activeTasks = new Map();
    this.setupSignalR();
  }

  setupSignalR() {
    this.connection = new HubConnectionBuilder()
      .withUrl('https://api.conduit.yourdomain.com/hubs/media-generation', {
        accessTokenFactory: () => this.apiKey,
        skipNegotiation: true,
        transport: 1 // WebSockets
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 2s, 4s, 8s, 16s, 30s max
          return Math.min(30000, Math.pow(2, retryContext.previousRetryCount) * 1000);
        }
      })
      .configureLogging(LogLevel.Information)
      .build();

    // Connection events
    this.connection.onreconnecting((error) => {
      console.log('SignalR reconnecting...', error);
      this.showConnectionStatus('reconnecting');
    });

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId);
      this.showConnectionStatus('connected');
    });

    this.connection.onclose((error) => {
      console.log('SignalR connection closed:', error);
      this.showConnectionStatus('disconnected');
    });

    // Media generation events
    this.connection.on('ImageGenerationStarted', (data) => {
      this.handleImageGenerationStarted(data);
    });

    this.connection.on('ImageGenerationProgress', (data) => {
      this.handleImageGenerationProgress(data);
    });

    this.connection.on('ImageGenerationCompleted', (data) => {
      this.handleImageGenerationCompleted(data);
    });

    this.connection.on('ImageGenerationFailed', (data) => {
      this.handleImageGenerationFailed(data);
    });

    this.connection.on('VideoGenerationStarted', (data) => {
      this.handleVideoGenerationStarted(data);
    });

    this.connection.on('VideoGenerationProgress', (data) => {
      this.handleVideoGenerationProgress(data);
    });

    this.connection.on('VideoGenerationCompleted', (data) => {
      this.handleVideoGenerationCompleted(data);
    });

    this.connection.on('VideoGenerationFailed', (data) => {
      this.handleVideoGenerationFailed(data);
    });
  }

  async start() {
    try {
      await this.connection.start();
      console.log('SignalR connection established');
      this.showConnectionStatus('connected');
    } catch (error) {
      console.error('SignalR connection failed:', error);
      this.showConnectionStatus('error');
    }
  }

  async generateImage(prompt, options = {}) {
    const response = await fetch('https://api.conduit.yourdomain.com/v1/images/generations', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        model: 'dall-e-3',
        prompt: prompt,
        async: true,
        ...options
      })
    });

    const task = await response.json();
    
    // Track the task
    this.activeTasks.set(task.task_id, {
      type: 'image',
      prompt: prompt,
      startTime: Date.now(),
      status: 'queued'
    });

    // Create UI element for this task
    this.createTaskUI(task.task_id, 'image', prompt);

    return task.task_id;
  }

  async generateVideo(prompt, options = {}) {
    const response = await fetch('https://api.conduit.yourdomain.com/v1/video/generations', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        model: 'minimax-video',
        prompt: prompt,
        duration: 5,
        ...options
      })
    });

    const task = await response.json();
    
    this.activeTasks.set(task.task_id, {
      type: 'video',
      prompt: prompt,
      startTime: Date.now(),
      status: 'queued'
    });

    this.createTaskUI(task.task_id, 'video', prompt);

    return task.task_id;
  }

  handleImageGenerationStarted(data) {
    const task = this.activeTasks.get(data.taskId);
    if (task) {
      task.status = 'processing';
      this.updateTaskUI(data.taskId, { status: 'processing', progress: 0 });
    }
  }

  handleImageGenerationProgress(data) {
    const task = this.activeTasks.get(data.taskId);
    if (task) {
      task.progress = data.progress;
      task.stage = data.stage;
      this.updateTaskUI(data.taskId, { 
        progress: data.progress, 
        stage: data.stage,
        estimatedTimeRemaining: data.estimatedTimeRemaining 
      });
    }
  }

  handleImageGenerationCompleted(data) {
    const task = this.activeTasks.get(data.taskId);
    if (task) {
      task.status = 'completed';
      task.completedAt = Date.now();
      task.imageUrl = data.imageUrl;
      task.generationTime = task.completedAt - task.startTime;

      this.updateTaskUI(data.taskId, {
        status: 'completed',
        imageUrl: data.imageUrl,
        generationTime: task.generationTime
      });

      // Show notification
      this.showNotification('Image Ready!', `Your image "${task.prompt.substring(0, 50)}..." is ready.`);
    }
  }

  handleVideoGenerationCompleted(data) {
    const task = this.activeTasks.get(data.taskId);
    if (task) {
      task.status = 'completed';
      task.completedAt = Date.now();
      task.videoUrl = data.videoUrl;
      task.thumbnailUrl = data.thumbnailUrl;
      task.generationTime = task.completedAt - task.startTime;

      this.updateTaskUI(data.taskId, {
        status: 'completed',
        videoUrl: data.videoUrl,
        thumbnailUrl: data.thumbnailUrl,
        generationTime: task.generationTime
      });

      this.showNotification('Video Ready!', `Your video "${task.prompt.substring(0, 50)}..." is ready.`);
    }
  }

  createTaskUI(taskId, type, prompt) {
    const container = document.getElementById('tasks-container');
    
    const taskElement = document.createElement('div');
    taskElement.id = `task-${taskId}`;
    taskElement.className = 'task-item';
    taskElement.innerHTML = `
      <div class="task-header">
        <span class="task-type">${type.toUpperCase()}</span>
        <span class="task-prompt">${prompt.substring(0, 100)}...</span>
      </div>
      <div class="task-status">
        <div class="status-text">Queued...</div>
        <div class="progress-container">
          <div class="progress-bar" style="width: 0%">0%</div>
        </div>
      </div>
      <div class="task-result" style="display: none;"></div>
    `;
    
    container.appendChild(taskElement);
  }

  updateTaskUI(taskId, data) {
    const taskElement = document.getElementById(`task-${taskId}`);
    if (!taskElement) return;

    const statusText = taskElement.querySelector('.status-text');
    const progressBar = taskElement.querySelector('.progress-bar');
    const resultContainer = taskElement.querySelector('.task-result');

    if (data.status === 'processing') {
      statusText.textContent = `Processing... (${data.progress || 0}%)`;
      if (data.stage) {
        statusText.textContent += ` - ${data.stage}`;
      }
    }

    if (data.progress !== undefined) {
      progressBar.style.width = `${data.progress}%`;
      progressBar.textContent = `${data.progress}%`;
    }

    if (data.status === 'completed') {
      statusText.textContent = `Completed in ${(data.generationTime / 1000).toFixed(1)}s`;
      progressBar.style.width = '100%';
      progressBar.textContent = 'Complete';
      
      if (data.imageUrl) {
        resultContainer.innerHTML = `
          <img src="${data.imageUrl}" alt="Generated image" style="max-width: 100%; height: auto;">
          <div class="result-actions">
            <a href="${data.imageUrl}" download>Download</a>
            <button onclick="shareImage('${data.imageUrl}')">Share</button>
          </div>
        `;
        resultContainer.style.display = 'block';
      }

      if (data.videoUrl) {
        resultContainer.innerHTML = `
          <video controls poster="${data.thumbnailUrl}" style="max-width: 100%; height: auto;">
            <source src="${data.videoUrl}" type="video/mp4">
            Your browser does not support the video tag.
          </video>
          <div class="result-actions">
            <a href="${data.videoUrl}" download>Download Video</a>
            <button onclick="shareVideo('${data.videoUrl}')">Share</button>
          </div>
        `;
        resultContainer.style.display = 'block';
      }
    }

    if (data.status === 'failed') {
      statusText.textContent = `Failed: ${data.error}`;
      taskElement.classList.add('task-failed');
    }
  }

  showConnectionStatus(status) {
    const statusElement = document.getElementById('connection-status');
    if (statusElement) {
      statusElement.textContent = status;
      statusElement.className = `connection-status ${status}`;
    }
  }

  showNotification(title, message) {
    if ('Notification' in window && Notification.permission === 'granted') {
      new Notification(title, { body: message });
    } else {
      // Fallback to in-app notification
      const notification = document.createElement('div');
      notification.className = 'app-notification';
      notification.innerHTML = `
        <strong>${title}</strong>
        <p>${message}</p>
      `;
      document.body.appendChild(notification);
      
      setTimeout(() => {
        notification.remove();
      }, 5000);
    }
  }

  getTaskStatistics() {
    const stats = {
      total: this.activeTasks.size,
      queued: 0,
      processing: 0,
      completed: 0,
      failed: 0
    };

    this.activeTasks.forEach(task => {
      stats[task.status]++;
    });

    return stats;
  }
}

// Usage
const mediaClient = new MediaGenerationClient('condt_your_virtual_key');

// Request notification permission
if ('Notification' in window && Notification.permission === 'default') {
  Notification.requestPermission();
}

// Start SignalR connection
await mediaClient.start();

// Generate media with real-time updates
const imageTaskId = await mediaClient.generateImage(
  'A beautiful sunset over mountains with a crystal-clear lake reflection'
);

const videoTaskId = await mediaClient.generateVideo(
  'A time-lapse of clouds moving over a cityscape at golden hour'
);

// Monitor statistics
setInterval(() => {
  const stats = mediaClient.getTaskStatistics();
  console.log('Media Generation Stats:', stats);
}, 10000);
```

## Advanced Task Management

### Task Queue Management

```javascript
class MediaTaskQueue {
  constructor(apiKey, options = {}) {
    this.apiKey = apiKey;
    this.baseUrl = 'https://api.conduit.yourdomain.com/v1';
    this.maxConcurrentTasks = options.maxConcurrentTasks || 5;
    this.retryAttempts = options.retryAttempts || 3;
    this.retryDelay = options.retryDelay || 5000;
    
    this.queue = [];
    this.activeTasks = new Map();
    this.completedTasks = new Map();
    this.failedTasks = new Map();
    this.processing = false;
  }

  async addTask(type, prompt, options = {}) {
    const task = {
      id: crypto.randomUUID(),
      type: type, // 'image' or 'video'
      prompt: prompt,
      options: options,
      status: 'queued',
      attempts: 0,
      createdAt: Date.now(),
      priority: options.priority || 5 // 1-10, higher = more important
    };

    this.queue.push(task);
    this.sortQueue();
    
    console.log(`Task ${task.id} added to queue (${this.queue.length} queued)`);
    
    if (!this.processing) {
      this.processQueue();
    }

    return task.id;
  }

  sortQueue() {
    // Sort by priority (higher first), then by creation time (older first)
    this.queue.sort((a, b) => {
      if (a.priority !== b.priority) {
        return b.priority - a.priority;
      }
      return a.createdAt - b.createdAt;
    });
  }

  async processQueue() {
    if (this.processing) return;
    this.processing = true;

    console.log('Starting queue processing...');

    while (this.queue.length > 0 || this.activeTasks.size > 0) {
      // Start new tasks if we have capacity
      while (this.queue.length > 0 && this.activeTasks.size < this.maxConcurrentTasks) {
        const task = this.queue.shift();
        this.startTask(task);
      }

      // Wait a bit before checking again
      await new Promise(resolve => setTimeout(resolve, 1000));
    }

    this.processing = false;
    console.log('Queue processing completed');
  }

  async startTask(task) {
    console.log(`Starting task ${task.id}: ${task.type} - ${task.prompt.substring(0, 50)}...`);
    
    task.status = 'starting';
    task.startedAt = Date.now();
    this.activeTasks.set(task.id, task);

    try {
      const endpoint = task.type === 'image' ? '/images/generations' : '/video/generations';
      
      const response = await fetch(`${this.baseUrl}${endpoint}`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${this.apiKey}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          model: task.type === 'image' ? 'dall-e-3' : 'minimax-video',
          prompt: task.prompt,
          async: true,
          ...task.options
        })
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const result = await response.json();
      task.conduitTaskId = result.task_id;
      task.status = 'processing';
      
      console.log(`Task ${task.id} started with Conduit task ID: ${result.task_id}`);
      
      // Monitor the task
      this.monitorTask(task);
      
    } catch (error) {
      console.error(`Failed to start task ${task.id}:`, error.message);
      await this.handleTaskFailure(task, error);
    }
  }

  async monitorTask(task) {
    const pollInterval = 5000; // 5 seconds
    
    const poll = async () => {
      try {
        const response = await fetch(`${this.baseUrl}/tasks/${task.conduitTaskId}`, {
          headers: {
            'Authorization': `Bearer ${this.apiKey}`
          }
        });

        const conduitTask = await response.json();
        
        task.progress = conduitTask.progress || 0;
        task.stage = conduitTask.stage;

        switch (conduitTask.status) {
          case 'completed':
            await this.handleTaskCompletion(task, conduitTask.result);
            return;
            
          case 'failed':
            await this.handleTaskFailure(task, new Error(conduitTask.error));
            return;
            
          default:
            // Continue monitoring
            setTimeout(poll, pollInterval);
        }
      } catch (error) {
        console.error(`Error monitoring task ${task.id}:`, error);
        setTimeout(poll, pollInterval * 2); // Retry with longer delay
      }
    };

    poll();
  }

  async handleTaskCompletion(task, result) {
    console.log(`Task ${task.id} completed successfully`);
    
    task.status = 'completed';
    task.completedAt = Date.now();
    task.result = result;
    task.duration = task.completedAt - task.startedAt;

    this.activeTasks.delete(task.id);
    this.completedTasks.set(task.id, task);

    // Call completion callback if provided
    if (task.onComplete) {
      try {
        await task.onComplete(task);
      } catch (error) {
        console.error('Error in completion callback:', error);
      }
    }

    this.emitEvent('taskCompleted', task);
  }

  async handleTaskFailure(task, error) {
    console.error(`Task ${task.id} failed:`, error.message);
    
    task.attempts++;
    task.lastError = error.message;

    if (task.attempts < this.retryAttempts) {
      console.log(`Retrying task ${task.id} (attempt ${task.attempts + 1}/${this.retryAttempts})`);
      
      // Add back to queue with delay
      setTimeout(() => {
        task.status = 'queued';
        this.queue.unshift(task); // Add to front for retry priority
        this.sortQueue();
      }, this.retryDelay * task.attempts); // Exponential backoff
      
    } else {
      console.log(`Task ${task.id} failed permanently after ${task.attempts} attempts`);
      
      task.status = 'failed';
      task.failedAt = Date.now();
      
      this.failedTasks.set(task.id, task);
      
      // Call failure callback if provided
      if (task.onFailure) {
        try {
          await task.onFailure(task, error);
        } catch (callbackError) {
          console.error('Error in failure callback:', callbackError);
        }
      }

      this.emitEvent('taskFailed', task);
    }

    this.activeTasks.delete(task.id);
  }

  async cancelTask(taskId) {
    // Cancel queued task
    const queueIndex = this.queue.findIndex(task => task.id === taskId);
    if (queueIndex !== -1) {
      const task = this.queue.splice(queueIndex, 1)[0];
      task.status = 'cancelled';
      console.log(`Cancelled queued task ${taskId}`);
      return true;
    }

    // Cancel active task
    const activeTask = this.activeTasks.get(taskId);
    if (activeTask && activeTask.conduitTaskId) {
      try {
        await fetch(`${this.baseUrl}/tasks/${activeTask.conduitTaskId}/cancel`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${this.apiKey}`
          }
        });
        
        activeTask.status = 'cancelled';
        this.activeTasks.delete(taskId);
        console.log(`Cancelled active task ${taskId}`);
        return true;
      } catch (error) {
        console.error(`Failed to cancel task ${taskId}:`, error);
        return false;
      }
    }

    return false;
  }

  getQueueStatus() {
    return {
      queued: this.queue.length,
      active: this.activeTasks.size,
      completed: this.completedTasks.size,
      failed: this.failedTasks.size,
      processing: this.processing,
      nextTask: this.queue.length > 0 ? {
        id: this.queue[0].id,
        type: this.queue[0].type,
        priority: this.queue[0].priority
      } : null
    };
  }

  emitEvent(eventName, data) {
    // Emit events for external listeners
    if (this.eventListeners && this.eventListeners[eventName]) {
      this.eventListeners[eventName].forEach(listener => {
        try {
          listener(data);
        } catch (error) {
          console.error('Error in event listener:', error);
        }
      });
    }
  }

  addEventListener(eventName, listener) {
    if (!this.eventListeners) {
      this.eventListeners = {};
    }
    
    if (!this.eventListeners[eventName]) {
      this.eventListeners[eventName] = [];
    }
    
    this.eventListeners[eventName].push(listener);
  }
}

// Usage
const taskQueue = new MediaTaskQueue('condt_your_virtual_key', {
  maxConcurrentTasks: 3,
  retryAttempts: 2,
  retryDelay: 3000
});

// Add event listeners
taskQueue.addEventListener('taskCompleted', (task) => {
  console.log(`✅ Task completed: ${task.id}`);
  console.log(`Result: ${task.result.url || task.result.video_url}`);
});

taskQueue.addEventListener('taskFailed', (task) => {
  console.log(`❌ Task failed: ${task.id} - ${task.lastError}`);
});

// Add tasks to queue
const task1 = await taskQueue.addTask('image', 'A serene mountain landscape', {
  priority: 8,
  quality: 'hd'
});

const task2 = await taskQueue.addTask('video', 'A cat playing with a ball', {
  priority: 5,
  duration: 4
});

const task3 = await taskQueue.addTask('image', 'A futuristic cityscape', {
  priority: 9,
  style: 'vivid'
});

// Monitor queue status
setInterval(() => {
  const status = taskQueue.getQueueStatus();
  console.log('Queue Status:', status);
}, 5000);

// Cancel a task if needed
// await taskQueue.cancelTask(task2);
```

## Error Handling and Recovery

### Comprehensive Error Management

```javascript
class AsyncMediaProcessor {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.baseUrl = 'https://api.conduit.yourdomain.com/v1';
    this.errorHandlers = new Map();
    this.setupDefaultErrorHandlers();
  }

  setupDefaultErrorHandlers() {
    // Content policy violation
    this.errorHandlers.set('content_policy_violation', async (task, error) => {
      console.log('Content policy violation, attempting to sanitize prompt...');
      
      const sanitizedPrompt = this.sanitizePrompt(task.prompt);
      if (sanitizedPrompt !== task.prompt) {
        console.log(`Original: ${task.prompt}`);
        console.log(`Sanitized: ${sanitizedPrompt}`);
        
        // Retry with sanitized prompt
        return await this.retryWithNewPrompt(task, sanitizedPrompt);
      }
      
      throw new Error('Unable to sanitize prompt for content policy compliance');
    });

    // Rate limit exceeded
    this.errorHandlers.set('rate_limit_exceeded', async (task, error) => {
      const waitTime = this.extractWaitTime(error.message) || 60000; // Default 1 minute
      console.log(`Rate limited, waiting ${waitTime}ms before retry...`);
      
      await new Promise(resolve => setTimeout(resolve, waitTime));
      return await this.retryTask(task);
    });

    // Model overloaded
    this.errorHandlers.set('model_overloaded', async (task, error) => {
      console.log('Model overloaded, trying alternative model...');
      
      const alternativeModel = this.getAlternativeModel(task.model);
      if (alternativeModel) {
        console.log(`Switching from ${task.model} to ${alternativeModel}`);
        return await this.retryWithNewModel(task, alternativeModel);
      }
      
      // Wait and retry with same model
      await new Promise(resolve => setTimeout(resolve, 30000));
      return await this.retryTask(task);
    });

    // Insufficient quota
    this.errorHandlers.set('insufficient_quota', async (task, error) => {
      console.log('Insufficient quota, trying cheaper alternative...');
      
      const cheaperModel = this.getCheaperModel(task.model);
      if (cheaperModel) {
        console.log(`Switching to cheaper model: ${cheaperModel}`);
        return await this.retryWithNewModel(task, cheaperModel);
      }
      
      throw new Error('No quota available and no cheaper alternatives');
    });

    // Network errors
    this.errorHandlers.set('network_error', async (task, error) => {
      console.log('Network error, retrying with exponential backoff...');
      
      const retryCount = task.retryCount || 0;
      const backoffTime = Math.min(Math.pow(2, retryCount) * 1000, 30000);
      
      await new Promise(resolve => setTimeout(resolve, backoffTime));
      
      task.retryCount = retryCount + 1;
      return await this.retryTask(task);
    });
  }

  async processWithErrorHandling(task) {
    let lastError;
    
    for (let attempt = 1; attempt <= 3; attempt++) {
      try {
        console.log(`Processing task ${task.id}, attempt ${attempt}/3`);
        
        const result = await this.processTask(task);
        return result;
        
      } catch (error) {
        lastError = error;
        console.error(`Attempt ${attempt} failed:`, error.message);
        
        // Try to handle the error
        const errorCode = this.extractErrorCode(error);
        const handler = this.errorHandlers.get(errorCode);
        
        if (handler && attempt < 3) {
          try {
            console.log(`Applying error handler for: ${errorCode}`);
            const handlerResult = await handler(task, error);
            
            if (handlerResult) {
              return handlerResult; // Handler succeeded
            }
          } catch (handlerError) {
            console.error('Error handler failed:', handlerError.message);
            // Continue to next attempt
          }
        }
        
        // Wait before next attempt (if not the last one)
        if (attempt < 3) {
          const waitTime = attempt * 2000; // 2s, 4s
          await new Promise(resolve => setTimeout(resolve, waitTime));
        }
      }
    }
    
    // All attempts failed
    throw new Error(`Task failed after 3 attempts. Last error: ${lastError.message}`);
  }

  async processTask(task) {
    const endpoint = task.type === 'image' ? '/images/generations' : '/video/generations';
    
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        model: task.model,
        prompt: task.prompt,
        async: true,
        ...task.options
      })
    });

    if (!response.ok) {
      const errorData = await response.json();
      const error = new Error(errorData.error?.message || 'Unknown error');
      error.code = errorData.error?.code;
      error.type = errorData.error?.type;
      throw error;
    }

    const taskResult = await response.json();
    
    // Monitor for completion
    return await this.monitorTaskCompletion(taskResult.task_id);
  }

  async monitorTaskCompletion(conduitTaskId) {
    const pollInterval = 3000;
    const maxWaitTime = 600000; // 10 minutes
    const startTime = Date.now();
    
    while (Date.now() - startTime < maxWaitTime) {
      try {
        const response = await fetch(`${this.baseUrl}/tasks/${conduitTaskId}`, {
          headers: {
            'Authorization': `Bearer ${this.apiKey}`
          }
        });

        const task = await response.json();
        
        switch (task.status) {
          case 'completed':
            return task.result;
            
          case 'failed':
            const error = new Error(task.error);
            error.code = task.error_code;
            throw error;
            
          default:
            // Still processing, continue polling
            await new Promise(resolve => setTimeout(resolve, pollInterval));
        }
      } catch (error) {
        if (error.code) {
          throw error; // Task-specific error
        }
        
        // Network error, retry polling
        console.error('Polling error:', error.message);
        await new Promise(resolve => setTimeout(resolve, pollInterval));
      }
    }
    
    throw new Error('Task monitoring timed out after 10 minutes');
  }

  sanitizePrompt(prompt) {
    // Remove potentially problematic content
    const problematicPatterns = [
      /\b(violence|violent|gore|blood|weapon|gun|knife)\b/gi,
      /\b(nude|naked|explicit|sexual|nsfw)\b/gi,
      /\b(drug|cocaine|heroin|marijuana)\b/gi,
      /\b(hitler|nazi|terrorist|bomb)\b/gi
    ];

    let sanitized = prompt;
    problematicPatterns.forEach(pattern => {
      sanitized = sanitized.replace(pattern, '');
    });

    // Clean up extra spaces and add safe qualifiers
    sanitized = sanitized.replace(/\s+/g, ' ').trim();
    
    if (sanitized.length < prompt.length * 0.5) {
      // Too much was removed, add safe context
      sanitized = `family-friendly, safe content: ${sanitized}`;
    }

    return sanitized;
  }

  getAlternativeModel(model) {
    const alternatives = {
      'dall-e-3': 'dall-e-2',
      'runway-gen3': 'minimax-video',
      'pika-1': 'minimax-video'
    };
    
    return alternatives[model] || null;
  }

  getCheaperModel(model) {
    const cheaper = {
      'dall-e-3': 'dall-e-2',
      'dall-e-2': 'minimax-image',
      'runway-gen3': 'runway-gen2',
      'runway-gen2': 'minimax-video'
    };
    
    return cheaper[model] || null;
  }

  extractErrorCode(error) {
    return error.code || error.type || 'unknown_error';
  }

  extractWaitTime(errorMessage) {
    const match = errorMessage.match(/retry after (\d+) seconds?/i);
    return match ? parseInt(match[1]) * 1000 : null;
  }

  async retryTask(task) {
    return await this.processTask(task);
  }

  async retryWithNewPrompt(task, newPrompt) {
    const modifiedTask = { ...task, prompt: newPrompt };
    return await this.processTask(modifiedTask);
  }

  async retryWithNewModel(task, newModel) {
    const modifiedTask = { ...task, model: newModel };
    return await this.processTask(modifiedTask);
  }
}

// Usage
const processor = new AsyncMediaProcessor('condt_your_virtual_key');

// Process a task with comprehensive error handling
const task = {
  id: 'task-123',
  type: 'image',
  model: 'dall-e-3',
  prompt: 'A beautiful landscape with mountains and lakes',
  options: {
    size: '1024x1024',
    quality: 'hd'
  }
};

try {
  const result = await processor.processWithErrorHandling(task);
  console.log('Task completed successfully:', result.url);
} catch (error) {
  console.error('Task failed permanently:', error.message);
}
```

## Next Steps

- **Real-Time Updates**: Integrate [SignalR and webhooks](../realtime/overview)
- **Storage Configuration**: Set up [media storage](storage-configuration)
- **Image Generation**: Master [synchronous image generation](image-generation)
- **Video Generation**: Create [dynamic video content](video-generation)
- **Integration Examples**: See complete [client patterns](../clients/overview)