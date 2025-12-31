# Gateway API Overview

The Conduit Gateway API provides an OpenAI-compatible interface for interacting with multiple LLM providers through a unified gateway. The API is designed to be drop-in compatible with existing OpenAI client libraries and code.

## Quick Navigation

- **[Getting Started](./getting-started.md)** - Authentication, basic usage, and quick start guide
- **[API Reference](./api-reference.md)** - Complete endpoint documentation with examples
- **[Node.js SDK](../../SDKs/Node/Core/README.md)** - Type-safe client for Node.js/TypeScript

## What You Can Do

- **Chat Completions** - Generate text responses with streaming support
- **Multimodal Vision** - Analyze images alongside text
- **Function Calling** - Enable LLMs to request tool execution
- **Embeddings** - Generate text embeddings
- **Image Generation** - Create images from text prompts

## Base URLs

- **Development**: `http://localhost:5002/v1`
- **Production**: Configure based on your deployment

## OpenAI Compatibility

The Gateway API is fully OpenAI-compatible, which means:

- ✅ Use existing OpenAI client libraries (Python, Node.js, C#, Go)
- ✅ Drop-in replacement in your existing code
- ✅ Same request/response formats
- ✅ Support for streaming, function calling, and multimodal inputs

## Related Documentation

- **[Feature Guides](../features/)** - In-depth guides for specific features
- **[Admin API](../admin/)** - Manage configuration and virtual keys
- **[SDK Documentation](../sdk/)** - Complete SDK integration guides
- **[SignalR Real-Time](../signalr/)** - Real-time updates and webhooks
