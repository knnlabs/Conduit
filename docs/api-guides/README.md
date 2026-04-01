# Conduit API Guides

This directory contains comprehensive **feature guides** and **tutorials** for using the Conduit APIs. For quick endpoint reference, see [API Reference](../api-reference/).

## 📖 What's in This Directory

These guides provide **in-depth explanations**, **best practices**, and **practical examples** for Conduit API features. They're designed to help you understand concepts and implement features correctly.

## 🎯 Quick Navigation

### Gateway API (User-Facing LLM API)

The Gateway API provides an OpenAI-compatible interface for LLM interactions.

- **[Gateway API Overview](./gateway/README.md)** - Introduction and capabilities
- **[Getting Started](./gateway/getting-started.md)** - Authentication, quick start, basic usage
- **[API Reference](./gateway/api-reference.md)** - Complete endpoint documentation

**Key Features:** Chat completions, streaming, function calling, multimodal vision, embeddings, image generation

### Admin API (Management & Configuration)

The Admin API provides administrative endpoints for platform configuration and monitoring.

- **[Admin API Overview](./admin/README.md)** - Introduction and architecture
- **[Getting Started](./admin/getting-started.md)** - Authentication, setup, first steps
- **[TypeScript SDK](./admin/typescript-sdk.md)** - Complete SDK guide with examples
- **[API Reference](./admin/api-reference.md)** - Complete endpoint documentation

**Key Features:** Virtual key management, provider configuration, model mappings, usage monitoring, cost tracking

### Feature Guides

In-depth guides for specific Conduit features.

- **[Function Calling](./features/function-calling.md)** - Enable LLMs to use tools and functions
- **[LLM Routing](./features/llm-routing.md)** - Routing strategies, failover, load balancing
- **[Multimodal Vision](./features/multimodal-vision.md)** - Work with images and vision models
- **[Webhooks](./features/webhooks.md)** - Real-time notifications for async operations

### SDK Guides

Complete integration guides for the official SDKs.

- **[SDK Overview](./sdk/README.md)** - SDK introduction and capabilities
- **[Next.js Integration](./sdk/nextjs-integration.md)** - Next.js specific patterns
- **[Best Practices](./sdk/best-practices.md)** - Security, performance, code quality
- **[Quick Reference](./sdk/quick-reference.md)** - Common SDK patterns
- **[Troubleshooting](./sdk/troubleshooting.md)** - Common issues and solutions
- **[Connection Management](./sdk/connection-management.md)** - SignalR real-time connections
- **[Health Checks](./sdk/health-checks.md)** - Lightweight health check functionality

### SignalR & Real-Time Features

Real-time updates and WebSocket connections.

- **[SignalR Overview](./signalr/README.md)** - Real-time features introduction
- **[Getting Started](./signalr/getting-started.md)** - Setup and basic usage
- **[Hub Reference](./signalr/hub-reference.md)** - Available hubs and events
- **[Client Examples](./signalr/client-examples.md)** - Integration examples
- **[Authentication](./signalr/authentication.md)** - SignalR authentication

## 📚 API Reference vs API Guides

**Confused about which to use?**

| Use **API Guides** (this directory) when... | Use **[API Reference](../api-reference/)** when... |
|----------------------------------------------|---------------------------------------------------|
| ✅ Learning how to use a feature | ✅ Looking up endpoint parameters |
| ✅ Understanding best practices | ✅ Finding request/response schemas |
| ✅ Following tutorials | ✅ Quick endpoint reference |
| ✅ Implementing complex features | ✅ Checking available endpoints |

**Example:**
- **Want to learn about function calling?** → Read [Function Calling Guide](./features/function-calling.md)
- **Need function calling endpoint details?** → Check [Gateway API Reference](./gateway/api-reference.md#function-calling)

## 🚀 Getting Started Paths

### For New Users

1. Start with [Gateway API Getting Started](./gateway/getting-started.md) - understand the basics
2. Review [Gateway API Reference](./gateway/api-reference.md) - see full capabilities
3. Explore [Feature Guides](./features/) as needed

### For Administrators

1. Start with [Admin API Getting Started](./admin/getting-started.md) - authentication and setup
2. Review [TypeScript SDK Guide](./admin/typescript-sdk.md) - learn the SDK
3. Check [Admin API Reference](./admin/api-reference.md) - endpoint documentation

### For SDK Integration

1. Start with [SDK Overview](./sdk/README.md) - understand SDK capabilities
2. Follow [Next.js Integration](./sdk/nextjs-integration.md) for Next.js apps
3. Check [Best Practices](./sdk/best-practices.md) for security and performance

### For Specific Features

- **Using tools/functions?** → [Function Calling](./features/function-calling.md)
- **Multi-provider setup?** → [LLM Routing](./features/llm-routing.md)
- **Image analysis?** → [Multimodal Vision](./features/multimodal-vision.md)
- **Event notifications?** → [Webhooks](./features/webhooks.md)
- **Real-time updates?** → [SignalR Guides](./signalr/)

## 📋 Related Documentation

- **[API Reference](../api-reference/)** - Endpoint specifications and parameters
- **[Development](../development/)** - Contributing to Conduit codebase
- **[Architecture](../architecture/)** - System design and patterns
- **[Operations](../operations/)** - Deployment and operations guides

## 💡 Tips for Using These Guides

- **Code examples** are production-ready - copy and adapt them
- **Best practices** sections highlight important patterns
- **Common pitfalls** help you avoid mistakes
- **Links to API Reference** for detailed parameter information

## 📁 Directory Structure

```
api-guides/
├── README.md (this file)
├── gateway/
│   ├── README.md
│   ├── getting-started.md
│   └── api-reference.md
├── admin/
│   ├── README.md
│   ├── getting-started.md
│   ├── typescript-sdk.md
│   └── api-reference.md
├── features/
│   ├── README.md
│   ├── function-calling.md
│   ├── llm-routing.md
│   ├── multimodal-vision.md
│   └── webhooks.md
├── sdk/
│   ├── README.md
│   ├── nextjs-integration.md
│   ├── best-practices.md
│   ├── quick-reference.md
│   ├── troubleshooting.md
│   ├── connection-management.md
│   └── health-checks.md
└── signalr/
    ├── README.md
    ├── getting-started.md
    ├── hub-reference.md
    ├── client-examples.md
    └── authentication.md
```

---

*For endpoint specifications and quick reference, see [API Reference](../api-reference/)*
