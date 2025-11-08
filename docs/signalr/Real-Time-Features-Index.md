# Conduit Real-Time Features Documentation Index

## Overview

Conduit provides comprehensive real-time capabilities for asynchronous image and video generation tasks. This index helps you navigate our real-time feature documentation based on your needs.

## Documentation Structure

### 🚀 Getting Started
- **[Real-Time API Guide](./Real-Time-API-Guide.md)** - Complete guide covering all real-time communication methods (polling, webhooks, SignalR)
- **Quick Decision Guide** - Choose the right method for your use case in under 30 seconds

### 💻 Implementation Guides

#### SignalR (WebSockets)
- **[SignalR Virtual Key Authentication](./SignalR-Virtual-Key-Authentication.md)** - Detailed authentication guide for SignalR connections
- **[Real-Time Client Examples](./Real-Time-Client-Examples.md#javascripttypescript-client)** - Production-ready SignalR client implementations
- **Supported Hubs**:
  - Image Generation: `wss://api.conduit.im/hubs/image-generation`
  - Video Generation: `wss://api.conduit.im/hubs/video-generation`

#### Webhooks
- **[Webhook API Documentation](./WebhookAPI.md)** - Complete webhook implementation guide
- **[Real-Time Client Examples](./Real-Time-Client-Examples.md#webhook-handler-examples)** - Webhook handler implementations
- **[HTTP Connection Pooling Guide](./HTTP-Connection-Pooling-Guide.md)** - Optimized webhook delivery at scale

#### Polling
- **[Real-Time API Guide](./Real-Time-API-Guide.md#method-1-polling-default)** - Simple polling implementation
- **When to Use**: Development, testing, or low-volume applications

### 📚 Client Libraries

Complete, production-ready client implementations with error handling and reconnection logic:

- **[JavaScript/TypeScript](./Real-Time-Client-Examples.md#javascripttypescript-client)**
  - Vanilla JavaScript/Node.js client
  - React hooks implementation
  - Mock client for testing
  
- **[Python](./Real-Time-Client-Examples.md#python-client)**
  - Async Python client
  - Django Channels integration
  - Connection pool management
  
- **[C#/.NET](./Real-Time-Client-Examples.md#cnet-client)**
  - .NET client with Polly resilience
  - ASP.NET Core integration
  - Hosted service pattern

### 🏗️ Architecture & Infrastructure

- **[Realtime Architecture](./Realtime-Architecture.md)** - Technical architecture details
- **[Event-Driven Architecture](./Architecture-Overview.md#event-driven-architecture)** - How events flow through the system
- **[SignalR Redis Backplane](./testing-signalr-redis-backplane.md)** - Horizontal scaling with Redis

### 📊 Production Considerations

#### Performance & Scaling
- **Connection Limits**:
  - SignalR: 100 concurrent connections per virtual key
  - Webhooks: 1,000 deliveries per minute per virtual key
  - Global: 10,000 SignalR connections per instance
  
- **[RabbitMQ Scaling Guide](./RabbitMQ-Scaling-Guide.md)** - Scaling webhook delivery with message queuing
- **[HTTP Connection Pooling](./HTTP-Connection-Pooling-Guide.md)** - Optimized for 1,000+ webhooks/minute

#### Monitoring & Operations
- **[Production Readiness Checklist](./production-readiness-checklist.md)** - Deployment best practices
- **Health Check Endpoints**:
  - SignalR: `GET /health/signalr`
  - Webhooks: `GET /health/webhooks`
  - Connection Pool: `GET /health/ready` (check `http_connection_pool`)

#### Security
- **[Security Guidelines](./Security-Guidelines.md)** - General security best practices
- **Virtual Key Authentication** - All real-time features require valid virtual keys
- **Webhook Security** - HTTPS enforcement, custom headers, idempotency

### 🔄 Migration Guides

- **[Polling to Webhooks](./Real-Time-API-Guide.md#from-polling-to-webhooks)** - Step-by-step migration
- **[Polling to SignalR](./Real-Time-API-Guide.md#from-polling-to-signalr)** - WebSocket migration guide

### 📖 API References

- **[Core API Reference](./Core-API-Detailed-Reference.md)** - Complete API documentation
- **[Async Image Generation](./Async-Image-Generation.md)** - Image generation endpoints
- **[Video Generation](./VIDEO_ARCHITECTURE.md)** - Video generation details

## Quick Links by Use Case

### "I want to add real-time updates to my web app"
1. Start with [Real-Time API Guide](./Real-Time-API-Guide.md)
2. Review [JavaScript Client Example](./Real-Time-Client-Examples.md#javascripttypescript-client)
3. Check [SignalR Authentication](./SignalR-Virtual-Key-Authentication.md)

### "I need server-to-server notifications"
1. Read [Webhook API Documentation](./WebhookAPI.md)
2. Implement using [Webhook Handler Examples](./Real-Time-Client-Examples.md#webhook-handler-examples)
3. Review [HTTP Connection Pooling](./HTTP-Connection-Pooling-Guide.md) for scale

### "I'm building a high-volume production system"
1. Study [Real-Time API Guide - Production Considerations](./Real-Time-API-Guide.md#production-considerations)
2. Implement [Webhook delivery with RabbitMQ](./RabbitMQ-Scaling-Guide.md)
3. Review [Production Readiness Checklist](./production-readiness-checklist.md)

### "I need to migrate from polling"
1. Follow [Migration Guide](./Real-Time-API-Guide.md#migration-guide)
2. Choose between webhooks (recommended) or SignalR
3. Test with both methods in parallel before switching

## Feature Comparison Matrix

| Feature | Polling | Webhooks | SignalR |
|---------|---------|----------|---------|
| **Complexity** | Low | Medium | High |
| **Latency** | High (seconds) | Low (milliseconds) | Lowest (real-time) |
| **Scalability** | Poor | Excellent | Good (with Redis) |
| **Resource Usage** | High | Low | Medium |
| **Firewall Friendly** | Yes | No (needs public endpoint) | Sometimes |
| **Progress Updates** | No | Yes | Yes |
| **Bidirectional** | No | No | Yes |
| **Best For** | Development/Testing | Production/High-volume | Real-time UI |

## Common Integration Patterns

### 1. **Hybrid Approach** (Recommended for Production)
- Use webhooks as primary notification method
- Implement SignalR for real-time UI updates
- Keep polling as fallback mechanism

### 2. **Progressive Enhancement**
- Start with polling for MVP
- Add webhooks for production scale
- Enhance with SignalR for premium features

### 3. **Microservices Pattern**
- Webhook receiver service handles notifications
- SignalR hub service manages WebSocket connections
- Message queue (RabbitMQ) connects services

## Support Resources

- **GitHub Issues**: [github.com/knnlabs/Conduit/issues](https://github.com/knnlabs/Conduit/issues)
- **Status Page**: [status.conduit.im](https://status.conduit.im)
- **API Support**: support@conduit.im

## Next Steps

1. **Evaluate**: Use the [Quick Decision Guide](./Real-Time-API-Guide.md#quick-decision-guide) to choose your method
2. **Implement**: Follow the appropriate implementation guide and client examples
3. **Test**: Use provided mock clients and testing strategies
4. **Deploy**: Follow production best practices and monitoring guidelines
5. **Monitor**: Set up health checks and alerts for your chosen method