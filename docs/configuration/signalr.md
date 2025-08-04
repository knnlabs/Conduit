# SignalR Configuration Guide

## Overview

Conduit uses SignalR for real-time communication between clients and servers. This guide covers configuration options for video generation progress tracking and other real-time features.

## Video Generation Hub Configuration

### Client Connection

Configure the SDK client with SignalR support:

```typescript
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

const client = new ConduitCoreClient({
  apiKey: 'your-virtual-key',
  baseURL: 'https://api.conduit.ai',
  signalR: {
    enabled: true,
    autoConnect: true,
    reconnectAttempts: 3,
    reconnectInterval: 5000,
    transports: ['webSockets', 'serverSentEvents'],
    logLevel: 'Information'
  }
});
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `enabled` | boolean | `true` | Enable/disable SignalR connections |
| `autoConnect` | boolean | `true` | Automatically connect when first needed |
| `reconnectAttempts` | number | `3` | Number of reconnection attempts |
| `reconnectInterval` | number | `5000` | Delay between reconnection attempts (ms) |
| `transports` | string[] | `['webSockets', 'serverSentEvents']` | Allowed transport methods |
| `logLevel` | string | `'Information'` | SignalR client logging level |

### Hub Endpoints

- **Video Generation Hub**: `/hubs/video-generation`
- **Image Generation Hub**: `/hubs/image-generation`  
- **Navigation State Hub**: `/hubs/navigation-state`

## Progress Event Formats

### Video Generation Progress

```typescript
interface VideoGenerationProgress {
  eventType: 'VideoGenerationProgress';
  taskId: string;
  progress: number;        // 0-100
  message?: string;        // Human-readable status
  currentFrame?: number;   // Current frame being processed
  totalFrames?: number;    // Total frames to generate
  timestamp: Date;
}
```

### Video Generation Completed

```typescript
interface VideoGenerationCompleted {
  eventType: 'VideoGenerationCompleted';
  taskId: string;
  videoUrl: string;
  duration: number;        // Video duration in seconds
  metadata: {
    width: number;
    height: number;
    fps: number;
    format: string;
  };
  timestamp: Date;
}
```

### Video Generation Failed

```typescript
interface VideoGenerationFailed {
  eventType: 'VideoGenerationFailed';
  taskId: string;
  error: string;
  isRetryable: boolean;
  errorCode?: string;
  timestamp: Date;
}
```

## Server Configuration

### appsettings.json

```json
{
  "SignalR": {
    "EnableDetailedErrors": false,
    "KeepAliveInterval": "00:00:15",
    "ClientTimeoutInterval": "00:00:30",
    "HandshakeTimeout": "00:00:15",
    "MaximumReceiveMessageSize": 32768,
    "StreamBufferCapacity": 10,
    "EnableMessageTracing": false
  },
  "Redis": {
    "Configuration": "localhost:6379",
    "InstanceName": "conduit-signalr"
  }
}
```

### Redis Backplane (Multi-Instance)

For horizontal scaling, configure Redis backplane:

```csharp
services.AddSignalR()
    .AddStackExchangeRedis(Configuration.GetConnectionString("Redis"), options =>
    {
        options.Configuration.ChannelPrefix = "conduit";
    });
```

## Connection Management

### Authentication

SignalR connections use the same virtual key authentication as API requests:

```typescript
// The SDK automatically includes the authorization header
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/video-generation', {
    accessTokenFactory: () => virtualKey,
  })
  .build();
```

### Connection Lifecycle

1. **Initial Connection**: Established on first progress tracking request
2. **Keep-Alive**: Automatic ping/pong to maintain connection
3. **Reconnection**: Automatic with exponential backoff
4. **Cleanup**: Connection closed when no active subscriptions

### Group Management

Clients are automatically added to task-specific groups:

```csharp
// Server-side group management
await Clients.Group($"video-{taskId}").SendAsync("Progress", progressEvent);
```

## Client Implementation Examples

### React Hook Example

```typescript
function useVideoProgress(taskId: string) {
  const [progress, setProgress] = useState(0);
  const [status, setStatus] = useState<string>('');

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/video-generation')
      .withAutomaticReconnect()
      .build();

    connection.on('Progress', (event: VideoGenerationProgress) => {
      if (event.taskId === taskId) {
        setProgress(event.progress);
        setStatus(event.message || '');
      }
    });

    connection.start()
      .then(() => connection.invoke('SubscribeToTask', taskId))
      .catch(console.error);

    return () => {
      connection.invoke('UnsubscribeFromTask', taskId);
      connection.stop();
    };
  }, [taskId]);

  return { progress, status };
}
```

### Plain JavaScript Example

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/video-generation', {
    accessTokenFactory: () => localStorage.getItem('virtualKey')
  })
  .withAutomaticReconnect()
  .build();

// Handle progress updates
connection.on('Progress', (progress) => {
  console.log(`Task ${progress.taskId}: ${progress.percentage}%`);
});

// Start connection and subscribe
async function trackProgress(taskId) {
  await connection.start();
  await connection.invoke('SubscribeToTask', taskId);
}
```

## Performance Tuning

### Client-Side Optimization

```typescript
{
  signalR: {
    // Reduce reconnection attempts in poor network conditions
    reconnectAttempts: 2,
    
    // Increase interval to reduce server load
    reconnectInterval: 10000,
    
    // Disable verbose logging in production
    logLevel: 'Error',
    
    // Prefer WebSockets for lower latency
    transports: ['webSockets']
  }
}
```

### Server-Side Optimization

```json
{
  "SignalR": {
    // Reduce keep-alive frequency
    "KeepAliveInterval": "00:00:30",
    
    // Increase client timeout for mobile networks
    "ClientTimeoutInterval": "00:01:00",
    
    // Limit message size to prevent abuse
    "MaximumReceiveMessageSize": 16384
  }
}
```

## Troubleshooting

### Common Issues

1. **Connection Failures**
   - Check firewall allows WebSocket connections
   - Verify proxy supports WebSocket upgrade
   - Ensure virtual key has appropriate permissions

2. **Missed Events**
   - Verify task subscription is active
   - Check for connection drops in logs
   - Ensure proper error handling in event callbacks

3. **High Latency**
   - Consider reducing keep-alive interval
   - Check network path to server
   - Monitor Redis latency for multi-instance setups

### Debug Logging

Enable detailed logging for troubleshooting:

```typescript
const client = new ConduitCoreClient({
  apiKey: 'your-key',
  signalR: {
    logLevel: 'Debug'
  },
  debug: true
});
```

### Health Checks

Monitor SignalR health endpoint:

```bash
curl https://api.conduit.ai/health/signalr
```

## Security Considerations

- Always use HTTPS/WSS in production
- Implement rate limiting on hub methods
- Validate all client input on server
- Use task-specific groups to prevent information leakage
- Monitor for abnormal connection patterns