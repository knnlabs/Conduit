# Conduit WebAdmin API Reference

This reference documents all API endpoints exposed by the Conduit WebAdmin. The WebAdmin is a Next.js administrative project that provides API endpoints serving as abstractions for the Node SDK functions, enabling seamless integration between the frontend and the Conduit backend services.

## Documentation Structure

The WebAdmin API reference has been organized into focused guides for easier navigation and maintenance:

### 🔧 Core Documentation
- **[WebAdmin Authentication](./webadmin-authentication.md)** - Authentication methods and session management
- **[WebAdmin Gateway API](./webadmin-core-api.md)** - Chat, image, video, and audio endpoints
- **[WebAdmin Admin API](./webadmin-admin-api.md)** - Administrative and management endpoints
- **[WebAdmin Real-Time Features](./webadmin-realtime.md)** - SignalR integration and real-time updates

### 📊 Advanced Topics
- **[WebAdmin Error Handling](./webadmin-error-handling.md)** - Error handling patterns and responses
- **[WebAdmin Types & Interfaces](./webadmin-types.md)** - TypeScript type definitions
- **[WebAdmin Rate Limiting](./webadmin-rate-limiting.md)** - Rate limiting and quota management

## Architecture Overview

The WebAdmin API serves as a thin abstraction layer over the Node SDK, providing:

- **Centralized SDK Management**: Singleton pattern for Admin and Core clients
- **Automatic Authentication**: WebAdmin virtual key auto-creation and management
- **Enhanced Error Handling**: Standardized error responses via `handleSDKError`
- **Session Integration**: NextAuth session management
- **Real-time Updates**: SignalR integration for live data

```typescript
// SDK Client Architecture
WebAdmin API → Node SDK → Backend Services
    ↓           ↓            ↓
  Next.js   Admin/Core   Admin/Gateway APIs
```

## Quick Start Guide

### Chat Completions
```typescript
const response = await fetch('/api/core/chat/completions', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    virtual_key: 'vk_abc123...',
    model: 'gpt-4',
    messages: [{ role: 'user', content: 'Hello!' }],
    stream: true,
  }),
});
```

### Image Generation
```typescript
const response = await fetch('/api/core/images/generations', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    virtual_key: 'vk_abc123...',
    prompt: 'A futuristic city at sunset',
    model: 'dall-e-3',
    size: '1024x1024',
  }),
});
```

### Virtual Key Management
```typescript
const response = await fetch('/api/admin/virtual-keys', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    keyName: 'Development Key',
    virtualKeyGroupId: 1,
    maxBudget: 100.00,
  }),
});
```

## API Categories

### Gateway API Endpoints
Direct access to AI capabilities via the Core SDK:
- **Chat Completions** - GPT, Claude, and other text models
- **Image Generation** - DALL-E, Stable Diffusion, MiniMax
- **Video Generation** - Async video generation with progress tracking
- **Audio Transcription** - Whisper and other speech-to-text models
- **Health Checks** - Service status and diagnostics

### Admin API Endpoints
System management capabilities via the Admin SDK:
- **Virtual Keys** - Create, manage, and monitor API keys
- **Providers** - Configure AI service providers
- **Virtual Key Groups** - Group management and balance tracking
- **Usage Analytics** - Request logs and cost monitoring
- **Configuration** - System settings and feature flags

### Enhanced Features
- **Real-time Streaming** - Enhanced SSE with metrics events
- **Progress Tracking** - SignalR integration for long-running tasks
- **Webhook Support** - Completion notifications for async operations
- **Multi-format Responses** - JSON, text, and binary format support

## Authentication Methods

### Session Authentication (Default)
```typescript
// Automatic session validation for WebAdmin requests
export async function GET(request: NextRequest) {
  const adminClient = getServerAdminClient();
  // Session automatically validated
}
```

### Virtual Key Authentication
```typescript
// Multiple virtual key authentication methods supported
headers: {
  "X-Virtual-Key": "vk_abc123...",           // Header method
  "Authorization": "Bearer vk_abc123..."      // Bearer token
}

// Or in request body
{
  "virtual_key": "vk_abc123...",
  "model": "gpt-4"
}
```

### Master Key Authentication
```typescript
// Automatic backend authentication
const adminClient = getServerAdminClient(); // Uses CONDUIT_API_TO_API_BACKEND_AUTH_KEY
```

## Error Handling

All WebAdmin API endpoints use standardized error handling:

```typescript
interface WebAdminAPIError {
  error: {
    type: string;
    message: string;
    code?: string;
    details?: any;
  };
  meta: {
    timestamp: string;
    endpoint: string;
    virtualKeyUsed?: string;
  };
}
```

Common error types include authentication errors, rate limiting, validation errors, and provider failures.

## Features & Capabilities

### ✅ Core Capabilities
- **OpenAI-compatible API** - Drop-in replacement for OpenAI API calls
- **Multi-provider support** - OpenAI, Anthropic, Google, Azure, and custom providers
- **Streaming responses** - Real-time content generation with enhanced metrics
- **Function calling** - Tool integration and function execution
- **Multimodal support** - Text, image, audio, and video processing

### ✅ Administrative Features
- **Virtual key management** - Create, monitor, and control API access
- **Usage tracking** - Detailed analytics and cost monitoring
- **Provider management** - Configure and health-check AI providers
- **Rate limiting** - Configurable quotas and request limits
- **Group management** - Organize keys into groups with shared balances

### ✅ Production Features
- **Auto-retry logic** - Intelligent failure recovery
- **Circuit breakers** - Provider failure protection
- **Real-time metrics** - Performance monitoring and alerts
- **Webhook integration** - Event-driven notifications
- **Session management** - Secure authentication and authorization

## Response Format

All WebAdmin API responses follow a consistent format:

```typescript
interface WebAdminAPIResponse<T> {
  data: T;
  meta: {
    timestamp: string;
    endpoint?: string;
    virtualKeyUsed?: string;
    [key: string]: any;
  };
}
```

This provides consistent metadata across all endpoints while maintaining flexibility for endpoint-specific data.

## Best Practices

### Authentication
- Use session authentication for WebAdmin frontend requests
- Include virtual keys via headers for external API calls
- Implement proper error handling for authentication failures

### Streaming
- Process enhanced streaming events (content, metrics, errors)
- Handle connection interruptions gracefully
- Implement proper cleanup for abandoned streams

### Error Handling
- Use standardized error types for consistent handling
- Implement retry logic for transient failures
- Provide meaningful error messages to end users

### Performance
- Use appropriate streaming for real-time applications
- Implement client-side caching where appropriate
- Monitor and track API usage and costs

## Support

For questions or issues with WebAdmin API:
- Check the specific endpoint guide for detailed documentation
- Review error handling patterns for troubleshooting
- See real-time features guide for SignalR integration
- Refer to authentication guide for setup and configuration