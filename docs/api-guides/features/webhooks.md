# Webhook API Documentation

## Overview

Conduit supports webhook notifications for asynchronous video generation, image generation, and batch operation tasks. Instead of polling for task status, clients can provide a webhook URL to receive real-time updates when tasks complete, fail, or progress.

The webhook system uses a **distributed architecture** with Redis-based circuit breaking, metrics collection, and delivery tracking to ensure reliable webhook delivery across multiple application instances.

### System Features

- **Distributed Circuit Breaker**: Automatic failure detection and recovery across all instances
- **Retry Logic**: Up to 3 automatic retries with exponential backoff  
- **Deduplication**: Prevents duplicate webhook deliveries
- **Real-time Monitoring**: Live delivery status via SignalR
- **Comprehensive Metrics**: Success rates, response times, and failure tracking

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
- **Timeout**: 10 seconds (configurable)
- **User-Agent**: Conduit-LLM/1.0
- **Custom Headers**: Any headers specified in `webhook_headers` will be included
- **Retries**: Up to 3 automatic retries with exponential backoff
- **Circuit Breaker**: Automatic failure detection blocks requests to consistently failing endpoints

## Reliability and Delivery Guarantees

### Distributed Circuit Breaker
The webhook system includes automatic failure detection and recovery:

- **Failure Threshold**: 5 consecutive failures trigger circuit breaker (configurable)
- **Recovery Time**: 5-minute cooldown before retry attempts (configurable)
- **Cross-Instance**: Circuit breaker state is shared across all application instances via Redis
- **Graceful Degradation**: System falls back to per-instance protection if Redis unavailable

### Delivery Guarantees
- **At-Most-Once**: Deduplication prevents duplicate deliveries for the same event
- **Best-Effort**: System will retry failed deliveries up to 3 times
- **Circuit Protection**: Consistently failing endpoints are temporarily excluded to prevent resource waste
- **Monitoring**: Real-time delivery status available via SignalR hub

### Response Expectations
For optimal webhook reliability, your endpoint should:

- **Respond Quickly**: Target <2 seconds, timeout at 10 seconds
- **Return 2xx Status**: Any 2xx status code indicates success
- **Handle Retries**: Be idempotent as webhooks may be retried
- **Implement Timeouts**: Set appropriate server-side timeouts (recommended: 30 seconds)

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

## Monitoring and Troubleshooting

### Real-time Monitoring
Connect to the SignalR webhook delivery hub for real-time updates:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/webhookdeliveryhub")
    .build();

// Listen for delivery attempts
connection.on("DeliveryAttempt", (data) => {
    console.log(`Webhook attempt: ${data.url} - Attempt ${data.attemptNumber}`);
});

// Listen for successes
connection.on("DeliverySuccess", (data) => {
    console.log(`Webhook success: ${data.url} - ${data.responseTimeMs}ms`);
});

// Listen for failures
connection.on("DeliveryFailed", (data) => {
    console.log(`Webhook failed: ${data.url} - ${data.errorMessage}`);
});

// Listen for circuit breaker state changes
connection.on("CircuitBreakerStateChanged", (data) => {
    console.log(`Circuit breaker ${data.currentState}: ${data.url}`);
});
```

### Common Issues and Solutions

#### Webhook Not Receiving Calls
1. **Check Circuit Breaker Status**: Your endpoint may be temporarily blocked due to failures
2. **Verify URL Accessibility**: Ensure your webhook URL is publicly accessible
3. **Check Logs**: Review application logs for delivery attempts and errors
4. **Test Endpoint**: Manually test your webhook endpoint with curl

#### High Failure Rates  
1. **Response Time**: Ensure your endpoint responds within 10 seconds
2. **Status Codes**: Return proper 2xx status codes for successful processing
3. **Error Handling**: Implement proper error handling to avoid 5xx responses
4. **Capacity**: Ensure your endpoint can handle the expected webhook volume

#### Circuit Breaker Activated
When a webhook endpoint consistently fails, the circuit breaker will temporarily stop delivery attempts:

- **Activation**: After 5 consecutive failures
- **Duration**: 5-minute cooldown period  
- **Recovery**: Automatic retry after cooldown
- **Notification**: Circuit state changes are logged and broadcast via SignalR

To recover:
1. Fix the underlying issue with your webhook endpoint
2. Wait for automatic recovery (5 minutes)
3. Contact support for manual circuit reset if needed

## Limitations and Constraints

- **URL Accessibility**: Webhook URLs must be publicly accessible over HTTPS
- **Payload Size**: Maximum webhook payload size: 1MB
- **Timeout**: Request timeout: 10 seconds (configurable)
- **Delivery Order**: No guarantee of delivery order for multiple webhooks to same URL
- **Authentication**: Client is responsible for webhook authentication validation
- **Retry Limit**: Maximum 3 retry attempts per webhook
- **Rate Limiting**: Subject to system-wide webhook delivery rate limits

## Architecture Documentation

For detailed technical information about the webhook system:

- **Architecture**: See [`docs/architecture/webhook-delivery-system.md`](./architecture/webhook-delivery-system.md)
- **Operations**: See [`docs/operations/webhook-monitoring.md`](./operations/webhook-monitoring.md)

## Future Enhancements

- Webhook retry configuration
- Webhook signature verification (HMAC)
- Webhook event filtering
- Batch webhook delivery
- WebSocket support for real-time streaming