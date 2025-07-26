# WebSocket Integration Guide

This document outlines the WebSocket integration for real-time chat features in ConduitLLM WebUI.

## Overview

The WebSocket integration will provide:
- Real-time message streaming
- Live typing indicators
- Multi-user collaboration
- Provider status updates
- Connection health monitoring

## Architecture

### SignalR Hubs

The integration will use SignalR (already available in the Core SDK) with the following hubs:

1. **ChatHub** (`/hubs/chat`)
   - Message streaming
   - Typing indicators
   - User presence

2. **StatusHub** (`/hubs/status`)
   - Provider availability
   - System health updates
   - Rate limit notifications

### Connection Management

```typescript
// Example connection setup
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/chat', {
    accessTokenFactory: () => getAuthToken()
  })
  .withAutomaticReconnect({
    nextRetryDelayInMilliseconds: (retryContext) => {
      // Exponential backoff: 0, 2, 10, 30 seconds
      const delays = [0, 2000, 10000, 30000];
      return delays[retryContext.previousRetryCount] || 30000;
    }
  })
  .build();
```

## Events

### Client → Server

- `JoinConversation(conversationId: string)`
- `LeaveConversation(conversationId: string)`
- `SendMessage(conversationId: string, message: ChatMessage)`
- `StartTyping(conversationId: string)`
- `StopTyping(conversationId: string)`
- `RequestProviderStatus()`

### Server → Client

- `ReceiveMessage(message: ChatMessage)`
- `UserJoined(userId: string, userName: string)`
- `UserLeft(userId: string)`
- `UserTyping(userId: string, isTyping: boolean)`
- `ProviderStatusUpdate(provider: string, status: ProviderStatus)`
- `StreamToken(conversationId: string, token: string, metadata: StreamMetadata)`

## Implementation Steps

### Phase 1: Basic Connection
1. Set up SignalR connection in `useWebSocketChat` hook
2. Implement connection state management
3. Add automatic reconnection logic
4. Create connection status UI component

### Phase 2: Message Streaming
1. Replace SSE streaming with WebSocket streaming
2. Implement token-by-token streaming
3. Add streaming performance metrics
4. Support pause/resume via WebSocket

### Phase 3: Collaboration Features
1. Implement typing indicators
2. Add user presence tracking
3. Show active users in conversation
4. Add "user is typing" UI

### Phase 4: Provider Management
1. Real-time provider status updates
2. Automatic failover notifications
3. Rate limit warnings
4. Model availability changes

## Security Considerations

1. **Authentication**: Use JWT tokens for WebSocket authentication
2. **Authorization**: Verify user has access to conversation
3. **Rate Limiting**: Implement per-connection rate limits
4. **Input Validation**: Sanitize all incoming messages
5. **Connection Limits**: Max connections per user

## Error Handling

```typescript
connection.onclose((error) => {
  if (error) {
    console.error('Connection closed with error:', error);
    // Show user-friendly error message
  }
});

connection.onreconnecting((error) => {
  console.log('Attempting to reconnect:', error);
  // Show reconnecting status
});

connection.onreconnected((connectionId) => {
  console.log('Reconnected with ID:', connectionId);
  // Rejoin conversations
  // Request missed messages
});
```

## Performance Optimizations

1. **Message Batching**: Group multiple tokens before sending
2. **Compression**: Enable WebSocket compression
3. **Binary Protocol**: Use MessagePack for smaller payloads
4. **Connection Pooling**: Reuse connections across components
5. **Lazy Loading**: Only connect when chat is active

## Testing Strategy

1. **Unit Tests**: Mock SignalR connections
2. **Integration Tests**: Test with real WebSocket server
3. **Load Tests**: Simulate multiple concurrent users
4. **Network Tests**: Test reconnection scenarios
5. **Security Tests**: Verify authentication/authorization

## Monitoring

Track the following metrics:
- Connection count
- Message throughput
- Reconnection frequency
- Average latency
- Error rates

## Future Enhancements

1. **Voice/Video Chat**: WebRTC integration
2. **File Sharing**: Binary file transfer
3. **Screen Sharing**: For collaborative debugging
4. **Persistent Connections**: Maintain connection across page navigation
5. **Offline Support**: Queue messages when disconnected