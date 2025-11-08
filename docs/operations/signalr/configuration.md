# SignalR Configuration

This document describes the SignalR configuration for real-time updates in Conduit.

## Real-Time Navigation State Updates

The WebUI navigation state now updates in real-time using SignalR:

1. **SignalR Hub**: Core API exposes `/hubs/navigation-state` for WebSocket connections
2. **Event-Driven Updates**: Navigation states update instantly when:
   - Model mappings are created/updated/deleted
   - Provider health status changes  
   - Model capabilities are discovered
3. **Automatic Fallback**: If SignalR connection fails, WebUI falls back to 30-second polling
4. **Provider Health Monitoring**: Admin API monitors provider health every 5 minutes (configurable)

## SignalR Redis Backplane for Horizontal Scaling

Conduit supports SignalR Redis backplane for horizontal scaling, enabling real-time updates across multiple Core API instances:

### Configuration

#### Environment Variables
```bash
# Use dedicated Redis instance for SignalR (recommended)
export REDIS_URL_SIGNALR=redis://redis-signalr:6379/2

# Or use connection string format
export ConnectionStrings__RedisSignalR=redis-signalr:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000
```

#### appsettings.json
```json
{
  "ConnectionStrings": {
    "RedisSignalR": "redis-signalr:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000"
  }
}
```

### Features

- **Automatic Detection**: SignalR automatically uses Redis backplane when connection string is configured
- **Channel Prefix**: Uses `conduit_signalr:` prefix to isolate SignalR messages
- **Separate Database**: Uses Redis database 2 to avoid conflicts with cache data
- **Connection Pooling**: Includes connection timeout and sync timeout settings
- **Fallback Mode**: Works in single-instance mode when Redis is not configured

### SignalR Hubs

Conduit provides three SignalR hubs for real-time updates:

1. **NavigationStateHub** (`/hubs/navigation-state`)
   - Real-time navigation state updates
   - Model mapping changes
   - Provider health status updates

2. **VideoGenerationHub** (`/hubs/video-generation`)
   - Real-time video generation progress
   - Completion notifications
   - Error notifications

3. **ImageGenerationHub** (`/hubs/image-generation`)
   - Real-time image generation progress
   - Task completion updates
   - Error handling

### Testing Multi-Instance Setup

1. **Deploy Multiple Instances**:
   ```bash
   docker-compose up -d --scale api=3
   ```

2. **Connect Clients to Different Instances**:
   - Use load balancer to distribute connections
   - Verify clients on different instances receive updates

3. **Monitor Redis Channels**:
   ```bash
   redis-cli -h redis-signalr
   > MONITOR
   # Watch for conduit_signalr: prefixed messages
   ```

### Performance Considerations

- **Message Size**: Limited to 32KB per message
- **Keep-Alive**: 30-second intervals to maintain connections
- **Client Timeout**: 60 seconds before disconnection
- **Stream Buffer**: Limited to 10 concurrent streams per connection

### Troubleshooting

**Check SignalR Backplane Status**:
```bash
# Look for this log message on startup
[Conduit] SignalR configured with Redis backplane for horizontal scaling
# or
[Conduit] SignalR configured without Redis backplane (single-instance mode)
```

**Monitor SignalR Connections**:
- Use Application Insights or OpenTelemetry metrics
- Monitor Redis memory usage for SignalR database
- Check WebSocket connection counts

**Common Issues**:
- **Sticky Sessions**: Not required with Redis backplane
- **Connection Drops**: Check Redis connectivity and timeouts
- **Message Loss**: Verify Redis persistence settings
- **High Latency**: Monitor Redis performance and network latency