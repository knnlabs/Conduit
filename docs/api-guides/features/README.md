# Feature Guides

In-depth guides for specific Conduit features and capabilities.

## Available Guides

### Core Features

- **[Function Calling](./function-calling.md)** - Enable LLMs to request tool execution
- **[Multimodal Vision](./multimodal-vision.md)** - Work with images and vision-capable models
- **[LLM Routing](./llm-routing.md)** - Intelligent request routing and load balancing
- **[Webhooks](./webhooks.md)** - Real-time notifications for async operations

## Feature Overview

### Function Calling

Allows LLMs to request the execution of specific functions during a conversation. The LLM doesn't execute functions directly - it only requests them, and your application executes them.

**Key Capabilities:**
- Define custom functions with parameters
- Structured JSON responses
- Multi-step function calling
- OpenAI-compatible format

**Use Cases:**
- API integrations
- Database queries
- External tool usage
- Structured data extraction

### Multimodal Vision

Support for vision-capable models that can analyze images alongside text. Compatible with OpenAI's vision API format.

**Key Capabilities:**
- Image URL inputs
- Base64-encoded images
- Multiple images per request
- High/low detail modes

**Use Cases:**
- Image analysis and description
- OCR and text extraction
- Visual question answering
- Multi-image comparison

### LLM Routing

Intelligent distribution of requests across different LLM providers and models for failover, load balancing, and cost optimization.

**Key Capabilities:**
- Multiple routing strategies (simple, random, round-robin)
- Health tracking and monitoring
- Automatic failover
- Cost optimization

**Use Cases:**
- High availability deployments
- Load distribution
- Provider redundancy
- Cost management

### Webhooks

Real-time notifications for asynchronous operations like video generation, image generation, and batch tasks.

**Key Capabilities:**
- Real-time delivery notifications
- Automatic retries with exponential backoff
- Circuit breaker for failing endpoints
- Deduplication and monitoring

**Use Cases:**
- Async video generation
- Async image generation
- Long-running batch operations
- Event-driven workflows

## Getting Started

1. **Choose a feature** based on your use case
2. **Read the guide** for detailed implementation instructions
3. **Review examples** in each guide
4. **Test in development** before deploying to production

## Related Documentation

- **[Gateway API](../core/)** - Main API documentation
- **[Admin API](../admin/)** - Configuration and management
- **[SDK Documentation](../sdk/)** - SDK integration guides
- **[SignalR](../signalr/)** - Real-time updates and connections

## Support

For questions or issues:
- **GitHub Issues**: https://github.com/knnlabs/Conduit/issues
- **Documentation**: https://docs.conduit.ai
