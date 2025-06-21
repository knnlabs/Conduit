# Async Image Generation

## Overview

Conduit now supports asynchronous image generation, allowing long-running image generation tasks to be processed in the background while providing immediate feedback to clients. This is especially useful for:

- Multiple image generation requests (N > 1)
- High-quality/HD image generation
- Slower providers like DALL-E 3
- Better user experience with progress tracking

## Architecture

The async image generation system uses:

1. **Redis Streams** for distributed task queue
2. **MassTransit** for event-driven processing
3. **Background Service** for task processing
4. **Polling endpoints** for status checking

## API Endpoints

### Create Async Image Generation Task

```bash
POST /v1/images/generations/async
Content-Type: application/json
Authorization: Bearer <your-api-key>

{
  "prompt": "A beautiful sunset over mountains",
  "model": "dall-e-3",
  "n": 2,
  "size": "1024x1024",
  "quality": "hd"
}
```

Response:
```json
{
  "taskId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "queued",
  "statusUrl": "https://api.conduit.com/v1/images/generations/550e8400-e29b-41d4-a716-446655440000/status",
  "created": "2024-01-15T10:30:00Z"
}
```

### Check Task Status

```bash
GET /v1/images/generations/{taskId}/status
Authorization: Bearer <your-api-key>
```

Response (Processing):
```json
{
  "taskId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "running",
  "created": "2024-01-15T10:30:00Z",
  "updated": "2024-01-15T10:30:05Z",
  "progress": 50
}
```

Response (Completed):
```json
{
  "taskId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "completed",
  "created": "2024-01-15T10:30:00Z",
  "updated": "2024-01-15T10:31:00Z",
  "progress": 100,
  "result": {
    "images": [
      {
        "url": "https://api.conduit.com/v1/media/image/abc123.png",
        "b64_json": null,
        "revisedPrompt": null,
        "metadata": {
          "provider": "openai",
          "model": "dall-e-3",
          "index": 0
        }
      }
    ],
    "duration": 60.5,
    "cost": 0.08,
    "provider": "openai",
    "model": "dall-e-3"
  }
}
```

### Cancel Task (Optional)

```bash
DELETE /v1/images/generations/{taskId}
Authorization: Bearer <your-api-key>
```

## Configuration

### Environment Variables

```bash
# Maximum concurrent image generations per instance (default: 3)
CONDUITLLM__IMAGEGENERATION__MAXCONCURRENCY=5

# Redis connection for task queue (required for multi-instance)
CONDUITLLM__REDIS__CONNECTIONSTRING=localhost:6379
```

### Multi-Instance Deployment

For production deployments with multiple Core API instances:

1. **Redis Required**: Redis must be configured for the task queue
2. **Background Service**: Each instance runs its own background service
3. **Load Distribution**: Tasks are automatically distributed across instances
4. **Task Recovery**: Orphaned tasks are recovered if an instance goes down

## Task States

- `pending` - Task created but not started
- `running` - Task is being processed
- `completed` - Task finished successfully
- `failed` - Task failed with error
- `cancelled` - Task was cancelled by user

## Implementation Details

### Domain Events

The system publishes events throughout the image generation lifecycle:

- `ImageGenerationRequested` - Task submitted to queue
- `ImageGenerationProgress` - Progress updates during processing
- `ImageGenerationCompleted` - Task completed with results
- `ImageGenerationFailed` - Task failed with error

### Cost Tracking

Image generation costs are automatically:
- Calculated based on provider and model
- Tracked via `SpendUpdateRequested` events
- Applied to virtual key budgets

### Error Handling

- Automatic retry with backoff for transient errors
- Rate limit handling with appropriate delays
- Task timeout protection (5 minutes default)
- Graceful degradation when event bus unavailable

## Client Integration

### JavaScript/TypeScript Example

```javascript
async function generateImageAsync(prompt, model = 'dall-e-3') {
  // Start async generation
  const response = await fetch('/v1/images/generations/async', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ prompt, model })
  });
  
  const { taskId, statusUrl } = await response.json();
  
  // Poll for status
  let status = 'queued';
  while (status === 'queued' || status === 'running') {
    await new Promise(resolve => setTimeout(resolve, 2000)); // 2s delay
    
    const statusResponse = await fetch(statusUrl, {
      headers: { 'Authorization': `Bearer ${apiKey}` }
    });
    
    const statusData = await statusResponse.json();
    status = statusData.status;
    
    if (status === 'completed') {
      return statusData.result;
    } else if (status === 'failed') {
      throw new Error(statusData.error);
    }
  }
}
```

## Migration from Sync API

The synchronous endpoint (`POST /v1/images/generations`) remains available for:
- Backward compatibility
- Simple single-image generation
- Clients that prefer blocking behavior

To migrate:
1. Change endpoint to `/v1/images/generations/async`
2. Implement polling for task status
3. Handle async response format

## Future Enhancements

- **SignalR Integration**: Real-time updates via WebSocket
- **Batch Operations**: Submit multiple prompts in one request
- **Priority Queues**: Premium tier with faster processing
- **Webhooks**: Push notifications when tasks complete