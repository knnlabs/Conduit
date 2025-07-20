# SignalR Real-Time Communication

*Last Updated: 2025-01-20*

Comprehensive documentation for SignalR/WebSocket real-time features in Conduit.

## Table of Contents
- [Overview](#overview)
- [Quick Start](#quick-start)
- [Documentation Structure](#documentation-structure)
- [Common Tasks](#common-tasks)

## Overview

Conduit uses SignalR for real-time bidirectional communication between clients and servers. This enables:

- **Real-time navigation state updates** - Track user journey through conversations
- **Live streaming responses** - Stream AI responses as they're generated
- **Media generation progress** - Monitor image/video generation status
- **System notifications** - Receive alerts and updates
- **Multi-instance synchronization** - Redis backplane for horizontal scaling

## Quick Start

### Client Connection

```typescript
import { HubConnectionBuilder } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
  .withUrl('https://api.conduit.ai/hubs/navigation-state', {
    accessTokenFactory: () => virtualKey
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
```

### Available Hubs

| Hub | Endpoint | Purpose |
|-----|----------|---------|
| NavigationState | `/hubs/navigation-state` | User journey tracking |
| VideoGeneration | `/hubs/video-generation` | Video creation progress |
| ImageGeneration | `/hubs/image-generation` | Image creation progress |
| Notifications | `/hubs/notifications` | System alerts |
| Admin | `/hubs/admin` | Admin real-time updates |
| Health | `/hubs/health` | System health monitoring |
| ContentGeneration | `/hubs/content-generation` | Unified media updates |

## Documentation Structure

### Core Documentation
- **[Configuration](./configuration.md)** - Server setup, Redis backplane, connection settings
- **[Hub Reference](./hub-reference.md)** - Complete hub API documentation
- **[Architecture](./architecture.md)** - System design and data flow
- **[Authentication](./authentication.md)** - Security and authorization

### Developer Guides
- **[Getting Started](./guides/getting-started.md)** - First SignalR integration
- **[Implementation Patterns](./guides/implementation-patterns.md)** - Best practices and patterns
- **[Connection Management](./guides/connection-management.md)** - Handling connections at scale
- **[Migration Guide](./guides/migration-guide.md)** - Upgrading SignalR implementations

### Advanced Topics
- **[Performance Optimization](./advanced/performance-optimization.md)** - Tuning for high throughput
- **[Monitoring & Diagnostics](./advanced/monitoring-and-diagnostics.md)** - Health checks and metrics
- **[Scaling with Redis](./advanced/scaling-and-redis.md)** - Multi-instance deployment
- **[Admin Features](./advanced/admin-features.md)** - Administrative capabilities

## Common Tasks

### Track Navigation State
```typescript
// Subscribe to navigation updates
connection.on('NavigationUpdated', (state) => {
  console.log('User navigated to:', state.currentView);
});

// Update navigation
await connection.invoke('UpdateNavigation', {
  view: 'conversation-detail',
  conversationId: '123'
});
```

### Monitor Media Generation
```typescript
// Image generation progress
connection.on('ImageProgress', (progress) => {
  console.log(`Generation ${progress.percentage}% complete`);
});

// Video generation updates
connection.on('VideoStatus', (status) => {
  if (status.completed) {
    console.log('Video ready:', status.url);
  }
});
```

### Handle Connection Lifecycle
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

## Authentication

All hubs support multiple authentication methods:

1. **Virtual Key Authentication** (Recommended)
   ```typescript
   accessTokenFactory: () => virtualKey
   ```

2. **Admin Authentication** (Admin hub only)
   ```typescript
   accessTokenFactory: () => adminAuthToken
   ```

3. **Anonymous Access** (Health hub only)
   - No authentication required for health checks

## Performance Considerations

- **Message Size**: Keep messages under 32KB for optimal performance
- **Frequency**: Batch updates when possible (max 10 updates/second per client)
- **Connections**: Use connection pooling for multiple hubs
- **Scaling**: Enable Redis backplane for 100+ concurrent connections

## Troubleshooting

### Common Issues

1. **Connection fails immediately**
   - Check CORS configuration
   - Verify authentication token
   - Ensure WebSocket support

2. **Messages not received**
   - Verify hub name and method names (case-sensitive)
   - Check group membership for targeted messages
   - Enable client-side logging

3. **Poor performance**
   - Enable message pack protocol
   - Configure Redis backplane
   - Review message frequency

### Debug Logging

```typescript
const connection = new HubConnectionBuilder()
  .withUrl(hubUrl)
  .configureLogging(LogLevel.Debug)
  .build();
```

## Related Documentation

- [Real-Time API Guide](../real-time-api-guide.md)
- [WebSocket Protocol](../api-reference/README.md#websocket)
- [Performance Metrics](../performance-metrics.md)
- [Redis Configuration](../cache-configuration.md)

## Migration from Older Versions

If upgrading from previous SignalR implementations:
1. Review [Migration Guide](./guides/migration-guide.md)
2. Update client libraries to latest versions
3. Test connection resilience
4. Verify authentication flow

---

*For framework-specific examples, see the [SDK Documentation](../../Clients/Node/docs/README.md).*