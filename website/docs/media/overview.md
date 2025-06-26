---
sidebar_position: 1
title: Media Generation Overview
description: Comprehensive guide to image and video generation with async processing and real-time updates
---

# Media Generation Overview

Conduit's Media Generation platform provides comprehensive image and video generation capabilities through a unified API, supporting multiple providers with async processing, real-time progress updates, and scalable storage solutions.

## Media Generation Capabilities

### Image Generation
- **Text-to-Image**: Generate images from descriptive text prompts
- **Image Editing**: Modify existing images with AI-powered tools
- **Style Transfer**: Apply artistic styles to existing images
- **Image Upscaling**: Enhance image resolution and quality
- **Multiple Formats**: Support for PNG, JPEG, WebP output formats

### Video Generation
- **Text-to-Video**: Create videos from text descriptions
- **Image-to-Video**: Animate static images into video sequences
- **Video Enhancement**: Improve video quality and resolution
- **Custom Durations**: Generate videos from 3 seconds to 6 seconds
- **Multiple Resolutions**: Support for 720p, 1080p, and custom aspect ratios

## Supported Providers

### Image Generation Providers

| Provider | Models | Strengths | Cost |
|----------|--------|-----------|------|
| **OpenAI** | DALL-E 2, DALL-E 3 | High quality, reliable | $0.02-0.08 per image |
| **MiniMax** | Image-01 | Fast generation, good value | $0.01-0.04 per image |
| **Replicate** | Various models | Model variety, customization | $0.001-0.10 per image |
| **Stability AI** | Stable Diffusion variants | Open source, customizable | $0.002-0.05 per image |
| **Midjourney** | v6, v5 | Artistic quality | $0.05-0.15 per image |

### Video Generation Providers

| Provider | Models | Capabilities | Cost |
|----------|--------|-------------|------|
| **MiniMax** | Video-01 | Text-to-video, 6s max | $0.20-0.50 per video |
| **Replicate** | Various models | Multiple video models | $0.10-1.00 per video |
| **Runway** | Gen-2, Gen-3 | High quality video | $0.50-2.00 per video |
| **Pika Labs** | Pika-1 | Creative video generation | $0.30-1.50 per video |

## API Endpoints

### Image Generation
```
Sync Generation:    POST /v1/images/generations
Async Generation:   POST /v1/images/generations (with async=true)
Task Status:        GET /v1/tasks/{task_id}
Generated Images:   GET /v1/images/{image_id}
```

### Video Generation
```
Async Generation:   POST /v1/video/generations
Task Status:        GET /v1/tasks/{task_id}
Generated Videos:   GET /v1/videos/{video_id}
Progress Updates:   WebSocket /hubs/video-generation
```

## Quick Start Examples

### Synchronous Image Generation

```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'https://api.conduit.yourdomain.com/v1'
});

// Synchronous generation (blocks until complete)
const response = await openai.images.generate({
  model: 'dall-e-3',
  prompt: 'A futuristic city skyline at sunset with flying cars',
  size: '1024x1024',
  quality: 'hd',
  n: 1
});

console.log('Generated image:', response.data[0].url);
```

### Asynchronous Image Generation

```javascript
// Start async generation
const response = await fetch('https://api.conduit.yourdomain.com/v1/images/generations', {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    model: 'dall-e-3',
    prompt: 'A magical forest with glowing mushrooms and fairy lights',
    size: '1024x1024',
    quality: 'hd',
    async: true,
    webhook_url: 'https://yourapp.com/webhooks/conduit'
  })
});

const task = await response.json();
console.log('Task started:', task.task_id);

// Poll for completion or wait for webhook
const checkStatus = async () => {
  const statusResponse = await fetch(`https://api.conduit.yourdomain.com/v1/tasks/${task.task_id}`, {
    headers: {
      'Authorization': 'Bearer condt_your_virtual_key'
    }
  });
  
  const status = await statusResponse.json();
  
  if (status.status === 'completed') {
    console.log('Image generated:', status.result.url);
  } else if (status.status === 'failed') {
    console.log('Generation failed:', status.error);
  } else {
    setTimeout(checkStatus, 2000); // Check again in 2 seconds
  }
};

checkStatus();
```

### Video Generation

```javascript
// Video generation is always async
const response = await fetch('https://api.conduit.yourdomain.com/v1/video/generations', {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    model: 'minimax-video',
    prompt: 'A cat playing piano in a cozy living room, warm lighting',
    duration: 5,
    resolution: '1280x720',
    webhook_url: 'https://yourapp.com/webhooks/conduit'
  })
});

const task = await response.json();
console.log('Video generation started:', task.task_id);
```

## Async Processing Architecture

### Task Lifecycle

```
Request → Validation → Queue → Processing → Storage → Notification
    ↓         ↓         ↓         ↓          ↓          ↓
 Task ID   Parameters  RabbitMQ  Provider    S3      Webhook/SignalR
```

### Task States

| State | Description | Actions Available |
|-------|-------------|-------------------|
| `queued` | Task waiting for processing | Cancel |
| `processing` | Generation in progress | Cancel (may not stop immediately) |
| `completed` | Generation successful | Download, view |
| `failed` | Generation failed | Retry, view error |
| `cancelled` | Task cancelled by user | None |

### Real-Time Updates

Conduit provides multiple ways to track generation progress:

1. **Webhooks**: HTTP callbacks when tasks complete
2. **SignalR**: Real-time WebSocket updates
3. **Polling**: Regular status checks via REST API

## Storage Configuration

### Development Storage (In-Memory)

Default configuration for development:

```json
{
  "storage": {
    "provider": "InMemory",
    "maxSize": "100MB",
    "retention": "1h"
  }
}
```

### Production Storage (S3-Compatible)

Recommended for production deployments:

```bash
# S3 Configuration
export CONDUITLLM__STORAGE__PROVIDER=S3
export CONDUITLLM__STORAGE__S3__SERVICEURL=https://s3.amazonaws.com
export CONDUITLLM__STORAGE__S3__ACCESSKEY=your-access-key
export CONDUITLLM__STORAGE__S3__SECRETKEY=your-secret-key
export CONDUITLLM__STORAGE__S3__BUCKETNAME=conduit-media
export CONDUITLLM__STORAGE__S3__REGION=us-east-1
export CONDUITLLM__STORAGE__S3__PUBLICBASEURL=https://cdn.yourdomain.com
```

### CDN Integration

For optimal performance, configure a CDN:

```bash
# Cloudflare R2 with CDN
export CONDUITLLM__STORAGE__S3__SERVICEURL=https://account-id.r2.cloudflarestorage.com
export CONDUITLLM__STORAGE__S3__PUBLICBASEURL=https://media.yourdomain.com
```

## Media Lifecycle Management

### Automatic Cleanup

Configure automatic media cleanup:

```json
{
  "mediaLifecycle": {
    "autoCleanup": true,
    "retentionDays": 30,
    "cleanupSchedule": "0 2 * * *",
    "orphanedMediaCleanup": true
  }
}
```

### Manual Media Management

```javascript
// Delete specific media
const deleteResponse = await fetch(`https://api.conduit.yourdomain.com/v1/images/${imageId}`, {
  method: 'DELETE',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key'
  }
});

// List media for virtual key
const mediaList = await fetch('https://api.conduit.yourdomain.com/v1/media', {
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key'
  }
});

const media = await mediaList.json();
console.log('Your generated media:', media.data);
```

## Cost Management

### Usage Tracking

Media generation costs are tracked per virtual key:

```json
{
  "mediaUsage": {
    "imagesGenerated": 145,
    "videosGenerated": 12,
    "totalCost": 47.85,
    "breakdown": {
      "dalle3": { "count": 75, "cost": 22.50 },
      "minimax_image": { "count": 70, "cost": 14.20 },
      "minimax_video": { "count": 12, "cost": 11.15 }
    }
  }
}
```

### Cost Optimization

```javascript
// Choose cost-effective models for bulk generation
const bulkGeneration = async (prompts) => {
  const tasks = [];
  
  for (const prompt of prompts) {
    const response = await fetch('/v1/images/generations', {
      method: 'POST',
      headers: {
        'Authorization': 'Bearer condt_your_virtual_key',
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        model: 'minimax-image', // Lower cost option
        prompt,
        size: '1024x1024',
        async: true
      })
    });
    
    const task = await response.json();
    tasks.push(task.task_id);
  }
  
  return tasks;
};
```

## Quality and Resolution Options

### Image Quality Settings

```javascript
// High quality settings
const highQualityImage = await openai.images.generate({
  model: 'dall-e-3',
  prompt: 'Professional headshot of a businesswoman',
  size: '1024x1024',
  quality: 'hd',        // HD quality
  style: 'natural'      // Natural vs vivid
});

// Standard quality for faster/cheaper generation
const standardImage = await openai.images.generate({
  model: 'dall-e-2',
  prompt: 'Cartoon character illustration',
  size: '512x512',
  quality: 'standard'
});
```

### Video Quality Settings

```javascript
const videoGeneration = await fetch('/v1/video/generations', {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    model: 'minimax-video',
    prompt: 'A peaceful lake at sunrise with mountains in background',
    resolution: '1920x1080',  // HD quality
    duration: 6,              // Maximum duration
    framerate: 24,            // Smooth motion
    quality: 'high'           // High quality setting
  })
});
```

## Advanced Features

### Batch Processing

```javascript
class MediaBatchProcessor {
  constructor(apiKey, concurrency = 5) {
    this.apiKey = apiKey;
    this.concurrency = concurrency;
    this.queue = [];
    this.processing = new Set();
  }

  async addImageGeneration(prompt, options = {}) {
    const task = {
      type: 'image',
      prompt,
      options,
      id: crypto.randomUUID()
    };
    
    this.queue.push(task);
    this.processQueue();
    
    return task.id;
  }

  async processQueue() {
    while (this.queue.length > 0 && this.processing.size < this.concurrency) {
      const task = this.queue.shift();
      this.processing.add(task.id);
      
      this.processTask(task).finally(() => {
        this.processing.delete(task.id);
        this.processQueue(); // Continue processing
      });
    }
  }

  async processTask(task) {
    try {
      const response = await fetch('/v1/images/generations', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${this.apiKey}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          model: 'dall-e-3',
          prompt: task.prompt,
          async: true,
          ...task.options
        })
      });

      const result = await response.json();
      this.onTaskStarted(task.id, result.task_id);
    } catch (error) {
      this.onTaskFailed(task.id, error);
    }
  }

  onTaskStarted(taskId, conduitTaskId) {
    console.log(`Batch task ${taskId} started as ${conduitTaskId}`);
  }

  onTaskFailed(taskId, error) {
    console.log(`Batch task ${taskId} failed:`, error.message);
  }
}

// Usage
const processor = new MediaBatchProcessor('condt_your_virtual_key', 3);

const prompts = [
  'A red car on a mountain road',
  'A blue ocean with sailing boats',
  'A green forest with tall trees'
];

for (const prompt of prompts) {
  await processor.addImageGeneration(prompt);
}
```

### Image Editing and Variations

```javascript
// Generate variations of an existing image
const variations = await openai.images.createVariation({
  image: fs.createReadStream('original-image.png'),
  n: 3,
  size: '1024x1024'
});

// Edit an image with a mask
const edit = await openai.images.edit({
  image: fs.createReadStream('original.png'),
  mask: fs.createReadStream('mask.png'),
  prompt: 'A beautiful garden with flowers',
  n: 1,
  size: '1024x1024'
});
```

## Error Handling

### Common Media Generation Errors

```javascript
try {
  const response = await openai.images.generate({
    model: 'dall-e-3',
    prompt: 'A beautiful landscape',
    size: '1024x1024'
  });
} catch (error) {
  switch (error.code) {
    case 'content_policy_violation':
      console.log('Prompt violates content policy');
      break;
    case 'rate_limit_exceeded':
      console.log('Too many requests, please wait');
      break;
    case 'insufficient_quota':
      console.log('Quota exceeded for this model');
      break;
    case 'model_overloaded':
      console.log('Model temporarily overloaded, try again');
      break;
    case 'invalid_image_size':
      console.log('Requested size not supported by model');
      break;
    default:
      console.log('Generation error:', error.message);
  }
}
```

### Retry Strategies

```javascript
class MediaGenerationClient {
  constructor(apiKey, maxRetries = 3) {
    this.apiKey = apiKey;
    this.maxRetries = maxRetries;
  }

  async generateWithRetry(params, attempt = 1) {
    try {
      return await this.generate(params);
    } catch (error) {
      if (attempt >= this.maxRetries) {
        throw error;
      }

      // Retry for certain error types
      if (this.shouldRetry(error)) {
        const delay = Math.pow(2, attempt) * 1000; // Exponential backoff
        console.log(`Retrying in ${delay}ms (attempt ${attempt + 1}/${this.maxRetries})`);
        
        await new Promise(resolve => setTimeout(resolve, delay));
        return this.generateWithRetry(params, attempt + 1);
      }

      throw error;
    }
  }

  shouldRetry(error) {
    const retryableCodes = [
      'model_overloaded',
      'rate_limit_exceeded',
      'network_error',
      'timeout'
    ];
    
    return retryableCodes.includes(error.code);
  }

  async generate(params) {
    const response = await fetch('/v1/images/generations', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(params)
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error.message);
    }

    return await response.json();
  }
}
```

## Integration Patterns

### Web Application Integration

```javascript
class MediaGenerationUI {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.activeTasks = new Map();
    this.setupSignalR();
  }

  setupSignalR() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/image-generation', {
        accessTokenFactory: () => this.apiKey
      })
      .build();

    this.connection.on('ImageGenerationStarted', (data) => {
      this.showProgress(data.taskId);
    });

    this.connection.on('ImageGenerationCompleted', (data) => {
      this.hideProgress(data.taskId);
      this.displayImage(data.imageUrl);
    });

    this.connection.on('ImageGenerationFailed', (data) => {
      this.hideProgress(data.taskId);
      this.showError(data.error);
    });

    this.connection.start();
  }

  async generateImage(prompt) {
    const response = await fetch('/v1/images/generations', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        model: 'dall-e-3',
        prompt,
        size: '1024x1024',
        async: true
      })
    });

    const task = await response.json();
    this.activeTasks.set(task.task_id, prompt);
    
    return task.task_id;
  }

  showProgress(taskId) {
    const progressDiv = document.createElement('div');
    progressDiv.id = `progress-${taskId}`;
    progressDiv.innerHTML = `
      <div class="progress-bar">
        <div class="progress-fill"></div>
      </div>
      <p>Generating image...</p>
    `;
    
    document.getElementById('results').appendChild(progressDiv);
  }

  hideProgress(taskId) {
    const progressDiv = document.getElementById(`progress-${taskId}`);
    if (progressDiv) {
      progressDiv.remove();
    }
  }

  displayImage(imageUrl) {
    const img = document.createElement('img');
    img.src = imageUrl;
    img.className = 'generated-image';
    
    document.getElementById('results').appendChild(img);
  }

  showError(error) {
    const errorDiv = document.createElement('div');
    errorDiv.className = 'error';
    errorDiv.textContent = `Generation failed: ${error}`;
    
    document.getElementById('results').appendChild(errorDiv);
  }
}
```

### Mobile Application Pattern

```javascript
// Mobile app integration with push notifications
class MobileMediaGenerator {
  constructor(apiKey, pushToken) {
    this.apiKey = apiKey;
    this.pushToken = pushToken;
  }

  async generateImage(prompt) {
    const response = await fetch('/v1/images/generations', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        model: 'dall-e-3',
        prompt,
        size: '1024x1024',
        async: true,
        webhook_url: `https://yourapi.com/webhooks/mobile?token=${this.pushToken}`
      })
    });

    const task = await response.json();
    
    // Store task locally for reference
    await this.storeTask(task.task_id, prompt);
    
    return task.task_id;
  }

  async storeTask(taskId, prompt) {
    // Store in local database/storage
    const task = {
      id: taskId,
      prompt,
      status: 'processing',
      createdAt: Date.now()
    };
    
    localStorage.setItem(`task_${taskId}`, JSON.stringify(task));
  }

  async handlePushNotification(notification) {
    if (notification.type === 'image_completed') {
      const taskId = notification.taskId;
      const imageUrl = notification.imageUrl;
      
      // Update local task status
      const task = JSON.parse(localStorage.getItem(`task_${taskId}`));
      task.status = 'completed';
      task.imageUrl = imageUrl;
      task.completedAt = Date.now();
      
      localStorage.setItem(`task_${taskId}`, JSON.stringify(task));
      
      // Update UI
      this.updateTaskInUI(task);
    }
  }
}
```

## Performance Optimization

### Caching Strategies

```javascript
class MediaCache {
  constructor() {
    this.cache = new Map();
    this.maxAge = 24 * 60 * 60 * 1000; // 24 hours
  }

  getCacheKey(prompt, options) {
    return crypto.createHash('md5')
      .update(JSON.stringify({ prompt, ...options }))
      .digest('hex');
  }

  async getCachedImage(prompt, options) {
    const key = this.getCacheKey(prompt, options);
    const cached = this.cache.get(key);
    
    if (cached && Date.now() - cached.timestamp < this.maxAge) {
      return cached.imageUrl;
    }
    
    return null;
  }

  setCachedImage(prompt, options, imageUrl) {
    const key = this.getCacheKey(prompt, options);
    this.cache.set(key, {
      imageUrl,
      timestamp: Date.now()
    });
  }

  async generateWithCache(prompt, options = {}) {
    // Check cache first
    const cached = await this.getCachedImage(prompt, options);
    if (cached) {
      console.log('Using cached image');
      return { url: cached, cached: true };
    }

    // Generate new image
    const response = await this.generate(prompt, options);
    
    // Cache the result
    this.setCachedImage(prompt, options, response.data[0].url);
    
    return { ...response.data[0], cached: false };
  }
}
```

## Next Steps

- **Image Generation**: Deep dive into [image generation capabilities](image-generation)
- **Video Generation**: Explore [video generation features](video-generation)
- **Async Processing**: Learn about [async task management](async-processing)
- **Storage Configuration**: Set up [media storage](storage-configuration)
- **Real-Time Updates**: Integrate [real-time notifications](../realtime/overview)