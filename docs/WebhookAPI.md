# Webhook API Documentation

## Overview

Conduit supports webhook notifications for asynchronous video generation tasks. Instead of polling for task status, clients can provide a webhook URL to receive real-time updates when video generation completes, fails, or progresses.

## Enabling Webhooks

To enable webhook notifications, include the `webhook_url` and optional `webhook_headers` fields in your video generation request:

```json
POST /v1/videos/generations/async
{
  "model": "minimax-video",
  "prompt": "A serene mountain landscape with clouds",
  "duration": 6,
  "size": "1280x720",
  "webhook_url": "https://your-app.com/webhooks/video-complete",
  "webhook_headers": {
    "Authorization": "Bearer your-webhook-secret",
    "X-Custom-Header": "custom-value"
  }
}
```

## Webhook Payload Format

### Video Generation Completed

When a video generation task completes successfully, Conduit sends a POST request to your webhook URL:

```json
{
  "task_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "completed",
  "webhook_type": "video_generation_completed",
  "timestamp": "2024-01-20T15:30:45.123Z",
  "video_url": "https://cdn.example.com/videos/generated-video.mp4",
  "generation_duration_seconds": 45.5,
  "model": "minimax-video",
  "prompt": "A serene mountain landscape with clouds"
}
```

### Video Generation Failed

If video generation fails, you'll receive:

```json
{
  "task_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "failed",
  "webhook_type": "video_generation_completed",
  "timestamp": "2024-01-20T15:30:45.123Z",
  "error": "Insufficient credits for video generation",
  "model": "minimax-video",
  "prompt": "A serene mountain landscape with clouds"
}
```

### Video Generation Cancelled

If the task is cancelled:

```json
{
  "task_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "cancelled",
  "webhook_type": "video_generation_completed",
  "timestamp": "2024-01-20T15:30:45.123Z",
  "error": "Video generation was cancelled by user request",
  "model": "minimax-video",
  "prompt": "A serene mountain landscape with clouds"
}
```

### Progress Updates

During video generation, progress updates are sent periodically:

```json
{
  "task_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "processing",
  "webhook_type": "video_generation_progress",
  "timestamp": "2024-01-20T15:29:30.123Z",
  "progress_percentage": 50,
  "message": "Rendering video content",
  "estimated_seconds_remaining": 30
}
```

## Webhook Request Details

- **Method**: POST
- **Content-Type**: application/json
- **Timeout**: 30 seconds
- **User-Agent**: Conduit-LLM/1.0
- **Custom Headers**: Any headers specified in `webhook_headers` will be included

## Best Practices

1. **Secure Your Webhooks**: Use HTTPS endpoints and include authentication headers
2. **Idempotency**: Webhooks may be retried on failure, ensure your handler is idempotent
3. **Quick Response**: Respond quickly (< 5 seconds) with 2xx status code
4. **Async Processing**: Process webhook data asynchronously if needed
5. **Fallback to Polling**: Keep polling as a fallback mechanism

## Example Webhook Handler

### Node.js/Express
```javascript
app.post('/webhooks/video-complete', (req, res) => {
  const { task_id, status, video_url, error } = req.body;
  
  // Verify webhook authenticity
  if (req.headers.authorization !== `Bearer ${process.env.WEBHOOK_SECRET}`) {
    return res.status(401).send('Unauthorized');
  }
  
  // Process webhook
  if (status === 'completed') {
    console.log(`Video ready: ${video_url}`);
    // Update your database, notify users, etc.
  } else if (status === 'failed') {
    console.error(`Video generation failed: ${error}`);
    // Handle failure
  }
  
  // Respond quickly
  res.status(200).send('OK');
});
```

### Python/Flask
```python
@app.route('/webhooks/video-complete', methods=['POST'])
def handle_video_webhook():
    # Verify webhook authenticity
    if request.headers.get('Authorization') != f"Bearer {os.environ['WEBHOOK_SECRET']}":
        return 'Unauthorized', 401
    
    data = request.json
    task_id = data['task_id']
    status = data['status']
    
    if status == 'completed':
        video_url = data['video_url']
        # Process completed video
    elif status == 'failed':
        error = data['error']
        # Handle failure
    
    return 'OK', 200
```

## Webhook vs Polling Comparison

| Feature | Webhooks | Polling |
|---------|----------|---------|
| Real-time updates | ✅ Instant | ❌ Delayed by interval |
| Resource usage | ✅ Efficient | ❌ Wasteful |
| Implementation complexity | Medium | Low |
| Firewall friendly | ❌ Requires public endpoint | ✅ Works everywhere |
| Reliability | Requires retry logic | Simple retry |

## Limitations

- Webhook URLs must be publicly accessible
- No guarantee of delivery order for multiple webhooks
- Maximum webhook payload size: 1MB
- Webhook timeout: 30 seconds
- No webhook authentication validation (client responsibility)

## Future Enhancements

- Webhook retry configuration
- Webhook signature verification (HMAC)
- Webhook event filtering
- Batch webhook delivery
- WebSocket support for real-time streaming