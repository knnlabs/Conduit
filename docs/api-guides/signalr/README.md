# SignalR Real-Time Communication

Conduit uses SignalR for real-time bidirectional communication between clients and servers. This enables media generation progress tracking, system notifications, and multi-instance synchronization via a Redis backplane.

## Quick Start

```typescript
import { HubConnectionBuilder } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
  .withUrl('https://your-conduit-host/hubs/video-generation', {
    accessTokenFactory: () => virtualKey
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
```

## Available Hubs

| Hub | Endpoint | Purpose |
|-----|----------|---------|
| VideoGeneration | `/hubs/video-generation` | Video creation progress |
| ImageGeneration | `/hubs/image-generation` | Image creation progress |
| SystemNotifications | `/hubs/notifications` | System alerts |
| Tasks | `/hubs/tasks` | General async task tracking |
| Metrics | `/hubs/metrics` | Admin metrics dashboard |
| HealthMonitoring | `/hubs/health-monitoring` | System health monitoring |

## Documentation

- **[Getting Started](./getting-started.md)** — First SignalR integration
- **[Hub Reference](./hub-reference.md)** — Complete hub API documentation
- **[Client Examples](./client-examples.md)** — Production-ready client implementations
- **[Authentication](./authentication.md)** — Security and authorization patterns
- **[MessagePack Protocol](./messagepack-protocol.md)** — Binary protocol for performance

## Authentication

All hubs support multiple authentication methods:

1. **Virtual Key Authentication** (Recommended)
   ```typescript
   accessTokenFactory: () => virtualKey
   ```

2. **Admin Authentication** (Admin hubs only)
   ```typescript
   accessTokenFactory: () => adminAuthToken
   ```

3. **Anonymous Access** (Health hub only)

## Connection Lifecycle

```typescript
connection.onreconnecting(() => {
  console.log('Connection lost, attempting to reconnect...');
});

connection.onreconnected(() => {
  console.log('Successfully reconnected');
});

connection.onclose(() => {
  console.log('Connection closed');
});
```

## Performance Considerations

- **Message Size**: Keep messages under 32KB for optimal performance
- **Frequency**: Batch updates when possible (max 10 updates/second per client)
- **Connections**: Use connection pooling for multiple hubs
- **Scaling**: Enable Redis backplane for 100+ concurrent connections

## Troubleshooting

### Connection fails immediately
- Check CORS configuration
- Verify authentication token
- Ensure WebSocket support is enabled

### Messages not received
- Verify hub name and method names (case-sensitive)
- Check group membership for targeted messages
- Enable client-side logging: `.configureLogging(LogLevel.Debug)`

### Poor performance
- Enable MessagePack protocol — see [MessagePack Protocol](./messagepack-protocol.md)
- Configure Redis backplane — see [SignalR Configuration](../../operations/signalr/configuration.md)
- Review message frequency

## Related Documentation

- [Streaming & WebSockets Architecture](../../architecture/real-time/streaming-and-websockets.md)
- [SignalR Configuration](../../operations/signalr/configuration.md)
- [Redis Backplane Testing](../../operations/signalr/redis-backplane-testing.md)
- [Redis Resilience](../../operations/infrastructure/redis-resilience.md)
