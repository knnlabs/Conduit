# Conduit API Reference

This directory contains the complete API documentation for Conduit.

## API Documentation Structure

### Core APIs (OpenAI-Compatible)
- **[Core API Reference](./core-api.md)** - Complete OpenAI-compatible API documentation
  - Chat completions, embeddings, models, images
  - Streaming, function calling, multimodal support
  - Client libraries and examples

### Admin APIs (Management)
- **[Admin API Reference](./admin-api.md)** - System administration and management
  - Virtual key management
  - Provider configuration
  - Model mapping and costs
  - System monitoring

### WebUI APIs (Frontend)
- **[WebUI API Reference](./webui-api.md)** - WebUI-specific endpoints
  - TypeScript interfaces
  - React integration patterns
  - Real-time features

## Quick Links

### By Feature
- **Authentication**: [API Keys](./core-api.md#authentication) | [Admin Auth](./admin-api.md#authentication)
- **Streaming**: [SSE Streaming](./core-api.md#streaming) | [WebSocket](./webui-api.md#real-time)
- **Error Handling**: [Error Codes](./core-api.md#error-handling)

### By User Type
- **Application Developers**: Start with [Core API Reference](./core-api.md)
- **System Administrators**: See [Admin API Reference](./admin-api.md)
- **Frontend Developers**: Check [WebUI API Reference](./webui-api.md)

## API Endpoints Overview

### Core API (OpenAI-Compatible)
- `POST /v1/chat/completions` - Chat completions
- `POST /v1/embeddings` - Generate embeddings
- `GET /v1/models` - List available models
- `POST /v1/images/generations` - Generate images

### Admin API
- `GET/POST/PUT/DELETE /api/virtualkeys` - Virtual key management
- `GET/POST/PUT/DELETE /api/modelprovider` - Provider configuration
- `GET/POST /api/modelmapping` - Model routing rules
- `GET /api/system/info` - System information

### WebUI API
- `POST /api/core/*` - Proxied core API calls
- `GET/POST /api/admin/*` - Proxied admin API calls
- `WS /hubs/*` - Real-time WebSocket connections

## Additional Resources

- [SDK Documentation](../../sdk-quick-reference.md)
- [Getting Started Guide](../../Getting-Started.md)
- [Provider Integration](../../Provider-Integration.md)