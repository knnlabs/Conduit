# Conduit Real-Time Client Examples

Production-ready client implementations for Conduit's real-time APIs with complete error handling, reconnection logic, and best practices across multiple programming languages.

## Documentation Structure

The real-time client examples have been organized by programming language and common patterns:

### 🌐 Client Implementations
- **[JavaScript/TypeScript Client](./real-time/javascript-client.md)** - Complete SignalR client with React hooks
- **[Python Client](./real-time/python-client.md)** - Python client with Django integration
- **[C#/.NET Client](./real-time/csharp-client.md)** - Native .NET SignalR client implementation

### 🔧 Advanced Topics
- **[Common Patterns](./real-time/common-patterns.md)** - Shared patterns, connection pooling, and error recovery
- **[Testing & Debugging](./real-time/testing-debugging.md)** - Testing strategies and debugging techniques

## Quick Start Guide

### JavaScript/TypeScript

```bash
npm install @microsoft/signalr
```

```typescript
import { ConduitRealtimeClient } from './conduit-realtime-client';

const client = new ConduitRealtimeClient({
    virtualKey: 'condt_your_virtual_key'
});

await client.connect('image');
const taskId = await client.generateImage({
    model: 'dall-e-3',
    prompt: 'A beautiful sunset over mountains'
});
```

### Python

```bash
pip install signalrcore requests
```

```python
from conduit_realtime_client import ConduitRealtimeClient

client = ConduitRealtimeClient('condt_your_virtual_key')
await client.connect('image')
task_id = await client.generate_image({
    'model': 'dall-e-3',
    'prompt': 'A beautiful sunset over mountains'
})
```

### C#/.NET

```bash
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

```csharp
var client = new ConduitRealtimeClient("condt_your_virtual_key");
await client.ConnectAsync("image");
var taskId = await client.GenerateImageAsync(new {
    model = "dall-e-3",
    prompt = "A beautiful sunset over mountains"
});
```

## Features Covered

### ✅ Core Functionality
- **SignalR WebSocket connections** - Real-time bidirectional communication
- **Image generation** - DALL-E, Midjourney, Stable Diffusion support
- **Video generation** - Video model support with progress tracking
- **Task subscription** - Subscribe/unsubscribe from specific tasks
- **Progress updates** - Real-time progress notifications

### ✅ Production Features
- **Automatic reconnection** - Exponential backoff with jitter
- **Error handling** - Comprehensive error recovery strategies
- **Connection pooling** - Efficient resource management
- **Type safety** - Full TypeScript support
- **Event-driven architecture** - Clean separation of concerns

### ✅ Framework Integration
- **React hooks** - `useConduitRealtime` hook for React applications
- **Django integration** - Django channels and routing examples
- **ASP.NET Core** - Native SignalR integration patterns
- **Connection management** - Lifecycle management and cleanup

## Architecture Overview

```
┌─────────────────┐    WebSocket    ┌─────────────────┐
│   Your Client   │ ←────────────→  │   Conduit API   │
│                 │                 │                 │
│ - Connect       │                 │ - Image Hub     │
│ - Subscribe     │                 │ - Video Hub     │
│ - Generate      │                 │ - Progress      │
│ - Listen        │                 │ - Completion    │
└─────────────────┘                 └─────────────────┘
        │                                   │
        └─── HTTP API ──────────────────────┘
             (Task Creation)
```

## Event Flow

1. **Connection** - Establish WebSocket connection to appropriate hub
2. **Authentication** - Authenticate using virtual key
3. **Task Creation** - Create image/video generation task via HTTP API
4. **Subscription** - Subscribe to task updates via WebSocket
5. **Progress Updates** - Receive real-time progress notifications
6. **Completion** - Receive final result or error notification
7. **Cleanup** - Unsubscribe and manage connection lifecycle

## Language-Specific Features

### JavaScript/TypeScript
- Full TypeScript type definitions
- React hooks integration
- Event-driven architecture with EventEmitter
- Automatic task subscription management
- Browser and Node.js support

### Python
- AsyncIO support for concurrent operations
- Django channels integration
- Async context managers for resource cleanup
- Type hints and dataclass support
- Connection pooling and retry logic

### C#/.NET
- Native SignalR client integration
- Async/await patterns
- IAsyncDisposable implementation
- Built-in logging and telemetry
- Configuration via dependency injection

## Best Practices

### Connection Management
- Always clean up connections when done
- Implement automatic reconnection with backoff
- Handle connection state changes gracefully
- Use connection pooling for high-throughput scenarios

### Error Handling
- Implement comprehensive error recovery
- Distinguish between retryable and non-retryable errors
- Provide meaningful error messages to users
- Log errors for debugging and monitoring

### Performance
- Subscribe only to tasks you need to monitor
- Unsubscribe from completed tasks promptly
- Use appropriate hub types (image vs video)
- Implement client-side caching when appropriate

### Security
- Never expose virtual keys in client-side code
- Use secure WebSocket connections (WSS)
- Implement proper authentication flows
- Validate all incoming data from the server

## Troubleshooting

### Common Issues
- **Connection failures** - Check virtual key and network connectivity
- **Subscription errors** - Ensure proper hub type and task ID
- **Missing progress updates** - Verify subscription is active
- **Memory leaks** - Always disconnect and clean up resources

### Debugging Tools
- Enable SignalR logging for detailed connection information
- Monitor WebSocket traffic with browser developer tools
- Use connection state events to track lifecycle
- Implement health checks for connection monitoring

## Support

For questions or issues with real-time clients:
- Check the language-specific guide for your platform
- Review [Common Patterns](./real-time/common-patterns.md) for shared solutions
- See [Testing & Debugging](./real-time/testing-debugging.md) for troubleshooting
- Refer to the [Real-Time API Guide](./real-time-api-guide.md) for API details