---
sidebar_position: 3
title: Video Generation
description: Create videos from text prompts using MiniMax's video-01 model
---

# Video Generation

Conduit's video generation capabilities enable you to create videos from text descriptions using MiniMax's video-01 model. All video generation is asynchronous with real-time progress updates and comprehensive task management.

## Quick Start

### Basic Text-to-Video Generation

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
    prompt: 'A cat playing piano in a cozy living room with warm lighting',
    duration: 5,
    resolution: '1280x720'
  })
});

const task = await response.json();
console.log('Video generation started:', task.task_id);

// Poll for completion
const checkStatus = async () => {
  const statusResponse = await fetch(`https://api.conduit.yourdomain.com/v1/tasks/${task.task_id}`, {
    headers: {
      'Authorization': 'Bearer condt_your_virtual_key'
    }
  });
  
  const status = await statusResponse.json();
  
  if (status.status === 'completed') {
    console.log('Video generated:', status.result.video_url);
    console.log('Thumbnail:', status.result.thumbnail_url);
  } else if (status.status === 'failed') {
    console.log('Generation failed:', status.error);
  } else {
    console.log(`Progress: ${status.progress || 0}%`);
    setTimeout(checkStatus, 5000); // Check again in 5 seconds
  }
};

checkStatus();
```

## Supported Model

### MiniMax Video-01

**Model: `minimax-video` (alias for `video-01`)**

MiniMax is currently the only provider that supports video generation in Conduit.

**Capabilities:**
- Text-to-video generation
- Multiple resolutions and aspect ratios
- Configurable duration (1-60 seconds, default 6)
- Async processing with progress tracking

```javascript
const videoRequest = await fetch('/v1/video/generations', {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    model: 'minimax-video',
    prompt: 'A majestic dragon flying over medieval castles at sunset',
    duration: 6,                    // 1-60 seconds
    resolution: '1920x1080',        // See supported resolutions below
    // Optional parameters
    aspect_ratio: '16:9'
  })
});

const task = await videoRequest.json();
console.log('Task started:', task.task_id);
```

**Supported Resolutions:**
- `720x480` (SD 4:3)
- `1280x720` (HD 16:9) 
- `1920x1080` (Full HD 16:9)
- `720x1280` (Portrait 9:16)
- `1080x1920` (Portrait 9:16)

**Duration:** 1-60 seconds (default 6 seconds)
**Generation Time:** 2-5 minutes depending on length and complexity
**Processing:** Fully asynchronous with real-time progress updates

## Task Management

### Checking Task Status

```javascript
async function checkVideoStatus(taskId) {
  const response = await fetch(`https://api.conduit.yourdomain.com/v1/tasks/${taskId}`, {
    headers: {
      'Authorization': 'Bearer condt_your_virtual_key'
    }
  });

  const task = await response.json();
  
  console.log('Status:', task.status);
  console.log('Progress:', task.progress || 0);
  
  return task;
}

// Usage
const task = await checkVideoStatus('task-12345');
if (task.status === 'completed') {
  console.log('Video URL:', task.result.video_url);
  console.log('Thumbnail URL:', task.result.thumbnail_url);
}
```

### Task States

| State | Description | Next Action |
|-------|-------------|-------------|
| `queued` | Task waiting for processing | Wait for processing to start |
| `processing` | Video generation in progress | Poll for updates |
| `completed` | Video successfully generated | Download video |
| `failed` | Generation failed | Check error, retry if needed |
| `cancelled` | Task cancelled by user | No further action |

### Cancelling Tasks

```javascript
// Cancel a running task
const response = await fetch(`https://api.conduit.yourdomain.com/v1/video/generations/${taskId}`, {
  method: 'DELETE',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key'
  }
});

if (response.ok) {
  console.log('Task cancelled successfully');
}
```

### Retrying Failed Tasks

```javascript
// Retry a failed task
const response = await fetch(`https://api.conduit.yourdomain.com/v1/video/generations/${taskId}/retry`, {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key'
  }
});

const newTask = await response.json();
console.log('Retry task started:', newTask.task_id);
```

## Real-Time Progress Updates

### Using SignalR for Real-Time Updates

```javascript
import { HubConnectionBuilder } from '@microsoft/signalr';

class VideoGenerationMonitor {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.connection = new HubConnectionBuilder()
      .withUrl('https://api.conduit.yourdomain.com/hubs/video-generation', {
        accessTokenFactory: () => apiKey
      })
      .build();
    
    this.setupEventHandlers();
  }

  setupEventHandlers() {
    this.connection.on('VideoGenerationStarted', (data) => {
      console.log(`🎬 Video generation started: ${data.taskId}`);
      this.showProgress(data.taskId);
    });

    this.connection.on('VideoGenerationProgress', (data) => {
      console.log(`⏳ Progress: ${data.progress}% - ${data.taskId}`);
      this.updateProgress(data.taskId, data.progress);
    });

    this.connection.on('VideoGenerationCompleted', (data) => {
      console.log(`✅ Video completed: ${data.videoUrl}`);
      this.showVideo(data.taskId, data.videoUrl, data.thumbnailUrl);
    });

    this.connection.on('VideoGenerationFailed', (data) => {
      console.log(`❌ Video generation failed: ${data.error}`);
      this.showError(data.taskId, data.error);
    });
  }

  async start() {
    await this.connection.start();
    console.log('🔄 Connected to video generation updates');
  }

  showProgress(taskId) {
    const progressElement = document.getElementById(`progress-${taskId}`);
    if (progressElement) {
      progressElement.style.display = 'block';
    }
  }

  updateProgress(taskId, progress) {
    const progressBar = document.getElementById(`progress-bar-${taskId}`);
    if (progressBar) {
      progressBar.style.width = `${progress}%`;
      progressBar.textContent = `${progress}%`;
    }
  }

  showVideo(taskId, videoUrl, thumbnailUrl) {
    const container = document.getElementById(`video-container-${taskId}`);
    if (container) {
      container.innerHTML = `
        <video controls poster="${thumbnailUrl}" style="max-width: 100%;">
          <source src="${videoUrl}" type="video/mp4">
          Your browser does not support the video tag.
        </video>
        <div class="video-actions">
          <a href="${videoUrl}" download>Download Video</a>
        </div>
      `;
    }
  }

  showError(taskId, error) {
    const container = document.getElementById(`video-container-${taskId}`);
    if (container) {
      container.innerHTML = `<div class="error">Generation failed: ${error}</div>`;
    }
  }
}

// Usage
const monitor = new VideoGenerationMonitor('condt_your_virtual_key');
await monitor.start();
```

### Polling Implementation

```javascript
class VideoTaskPoller {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.activeTasks = new Map();
  }

  async startVideoGeneration(prompt, options = {}) {
    const response = await fetch('https://api.conduit.yourdomain.com/v1/video/generations', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        model: 'minimax-video',
        prompt: prompt,
        duration: 6,
        resolution: '1280x720',
        ...options
      })
    });

    const task = await response.json();
    this.activeTasks.set(task.task_id, {
      prompt: prompt,
      startTime: Date.now()
    });

    this.pollTask(task.task_id);
    return task.task_id;
  }

  async pollTask(taskId) {
    const pollInterval = 5000; // 5 seconds
    
    const poll = async () => {
      try {
        const response = await fetch(`https://api.conduit.yourdomain.com/v1/tasks/${taskId}`, {
          headers: {
            'Authorization': `Bearer ${this.apiKey}`
          }
        });

        const task = await response.json();
        
        switch (task.status) {
          case 'completed':
            this.onTaskCompleted(taskId, task.result);
            this.activeTasks.delete(taskId);
            return;
            
          case 'failed':
            this.onTaskFailed(taskId, task.error);
            this.activeTasks.delete(taskId);
            return;
            
          case 'processing':
            this.onTaskProgress(taskId, task.progress || 0);
            setTimeout(poll, pollInterval);
            break;
            
          default:
            setTimeout(poll, pollInterval);
        }
      } catch (error) {
        console.error('Polling error:', error);
        setTimeout(poll, pollInterval * 2); // Backoff on error
      }
    };

    poll();
  }

  onTaskCompleted(taskId, result) {
    const taskData = this.activeTasks.get(taskId);
    const duration = Date.now() - taskData.startTime;
    
    console.log(`Task ${taskId} completed in ${(duration / 1000).toFixed(1)}s`);
    console.log('Video URL:', result.video_url);
    console.log('Thumbnail URL:', result.thumbnail_url);
  }

  onTaskFailed(taskId, error) {
    console.error(`Task ${taskId} failed:`, error);
  }

  onTaskProgress(taskId, progress) {
    console.log(`Task ${taskId}: ${progress}% complete`);
  }
}

// Usage
const poller = new VideoTaskPoller('condt_your_virtual_key');
const taskId = await poller.startVideoGeneration(
  'A time-lapse of clouds moving over a cityscape at golden hour'
);
```

## Batch Video Generation

### Processing Multiple Videos

```javascript
class BatchVideoGenerator {
  constructor(apiKey, concurrency = 2) {
    this.apiKey = apiKey;
    this.concurrency = concurrency;
    this.queue = [];
    this.activeTasks = new Set();
  }

  async addToQueue(prompt, options = {}) {
    this.queue.push({ prompt, options });
    this.processQueue();
  }

  async processQueue() {
    while (this.queue.length > 0 && this.activeTasks.size < this.concurrency) {
      const { prompt, options } = this.queue.shift();
      const taskId = await this.startVideoGeneration(prompt, options);
      this.activeTasks.add(taskId);
    }
  }

  async startVideoGeneration(prompt, options) {
    try {
      const response = await fetch('https://api.conduit.yourdomain.com/v1/video/generations', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${this.apiKey}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          model: 'minimax-video',
          prompt: prompt,
          duration: 6,
          resolution: '1280x720',
          ...options
        })
      });

      const task = await response.json();
      console.log(`Started video generation: ${task.task_id}`);
      
      // Monitor completion
      this.monitorTask(task.task_id);
      
      return task.task_id;
    } catch (error) {
      console.error('Failed to start video generation:', error);
      throw error;
    }
  }

  async monitorTask(taskId) {
    // Use polling or SignalR to monitor completion
    // When complete, remove from activeTasks and process queue
    // Implementation similar to previous examples
  }
}

// Usage
const batchGen = new BatchVideoGenerator('condt_your_virtual_key', 2);

const prompts = [
  'A peaceful lake at sunrise with mountains in the background',
  'A busy street market with colorful vendors and customers',
  'A rocket launching into space with flames and smoke'
];

for (const prompt of prompts) {
  await batchGen.addToQueue(prompt);
}
```

## Error Handling

### Common Video Generation Errors

```javascript
try {
  const response = await fetch('/v1/video/generations', {
    method: 'POST',
    headers: {
      'Authorization': 'Bearer condt_your_virtual_key',
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      model: 'minimax-video',
      prompt: 'A beautiful landscape',
      duration: 5
    })
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error?.message || 'Video generation failed');
  }

  const task = await response.json();
  console.log('Task started:', task.task_id);
} catch (error) {
  console.error('Error starting video generation:', error.message);
  
  // Handle specific error cases
  if (error.message.includes('rate limit')) {
    console.log('Rate limited, try again later');
  } else if (error.message.includes('quota')) {
    console.log('Quota exceeded');
  } else if (error.message.includes('content policy')) {
    console.log('Prompt violates content policy');
  }
}
```

### Retry Logic

```javascript
class ResilientVideoGenerator {
  constructor(apiKey, maxRetries = 3) {
    this.apiKey = apiKey;
    this.maxRetries = maxRetries;
  }

  async generateWithRetry(prompt, options = {}) {
    for (let attempt = 1; attempt <= this.maxRetries; attempt++) {
      try {
        const response = await fetch('https://api.conduit.yourdomain.com/v1/video/generations', {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${this.apiKey}`,
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({
            model: 'minimax-video',
            prompt: prompt,
            ...options
          })
        });

        if (response.ok) {
          const task = await response.json();
          return task.task_id;
        }

        const error = await response.json();
        
        // Don't retry for certain errors
        if (error.error?.code === 'content_policy_violation' || 
            error.error?.code === 'insufficient_quota') {
          throw new Error(error.error.message);
        }

        if (attempt === this.maxRetries) {
          throw new Error(error.error?.message || 'Video generation failed');
        }

        // Wait before retry
        const delay = Math.pow(2, attempt) * 1000;
        console.log(`Attempt ${attempt} failed, retrying in ${delay}ms...`);
        await new Promise(resolve => setTimeout(resolve, delay));

      } catch (error) {
        if (attempt === this.maxRetries) {
          throw error;
        }
      }
    }
  }
}

// Usage
const resilientGen = new ResilientVideoGenerator('condt_your_virtual_key');
try {
  const taskId = await resilientGen.generateWithRetry(
    'A cat playing with a ball of yarn'
  );
  console.log('Generation started:', taskId);
} catch (error) {
  console.error('Failed after all retries:', error.message);
}
```

## Best Practices

### Prompt Engineering

```javascript
// Good prompts for video generation
const goodPrompts = [
  'A cat walking across a wooden table in slow motion',
  'Waves crashing against rocks at sunset with golden light',
  'A person reading a book by a fireplace with warm lighting',
  'Rain drops falling on a window with city lights in the background'
];

// Tips for better results:
// 1. Be specific about motion and scene
// 2. Include lighting conditions
// 3. Mention camera movement if desired
// 4. Keep prompts focused and clear
// 5. Avoid complex scenes with multiple actions
```

### Performance Optimization

```javascript
// Use shorter durations for faster generation
const fastVideo = {
  duration: 3,  // Faster than 6 seconds
  resolution: '1280x720'  // Balance of quality and speed
};

// For testing, use lower resolution
const testVideo = {
  duration: 3,
  resolution: '720x480'  // Fastest generation
};

// For production, use high quality
const productionVideo = {
  duration: 6,
  resolution: '1920x1080'  // Best quality
};
```

### Cost Management

```javascript
// Estimate costs before generation
function estimateVideoCost(duration, resolution) {
  const baseCost = 0.20;  // Base cost per video
  const durationMultiplier = duration / 6;  // Relative to 6-second default
  const resolutionMultipliers = {
    '720x480': 0.8,
    '1280x720': 1.0,
    '1920x1080': 1.5,
    '720x1280': 1.0,
    '1080x1920': 1.5
  };
  
  const multiplier = resolutionMultipliers[resolution] || 1.0;
  return baseCost * durationMultiplier * multiplier;
}

// Usage
const cost = estimateVideoCost(6, '1920x1080');
console.log(`Estimated cost: $${cost.toFixed(2)}`);
```

## Next Steps

- **Image Generation**: Combine with [image generation](image-generation) for comprehensive media
- **Async Processing**: Learn about [async task management](async-processing)
- **Real-Time Updates**: Integrate [real-time notifications](../realtime/overview)
- **Storage Configuration**: Set up [media storage](storage-configuration)
- **Integration Examples**: See complete [client patterns](../clients/overview)