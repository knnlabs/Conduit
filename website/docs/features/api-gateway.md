---
sidebar_position: 1
title: API Gateway
description: Learn about Conduit's unified API Gateway for multiple LLM providers
---

# API Gateway

The Conduit API Gateway serves as a unified interface for multiple LLM providers, allowing you to interact with various AI services through a consistent API.

## OpenAI-Compatible API

Conduit's API is designed to be compatible with the OpenAI API format, making it easy to integrate with existing applications that already use OpenAI. The primary endpoints include:

- `/v1/chat/completions` - For chat-based interactions
- `/v1/completions` - For text completion tasks
- `/v1/embeddings` - For generating vector embeddings
- `/v1/models` - For retrieving available models

## Authentication

All API requests require authentication using a virtual key. These keys can be created and managed through the Conduit Web UI.

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "my-gpt4",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
```

## Request Transformation

The API Gateway performs several important functions:

1. **Authentication** - Validates virtual keys and enforces permissions
2. **Routing** - Determines which provider and model to use for each request
3. **Transformation** - Converts the OpenAI-compatible request format to provider-specific formats
4. **Rate Limiting** - Enforces rate limits defined on virtual keys
5. **Logging** - Records usage for monitoring and cost tracking

## Provider Independence

Your applications can seamlessly switch between different providers without changing any code. Simply update the model mapping in the Conduit configuration, and requests will be routed to the new provider automatically.

## Error Handling

The API Gateway provides consistent error responses regardless of the underlying provider. This makes error handling in your applications simpler and more predictable.

## Next Steps

- Learn about [Virtual Keys](virtual-keys) for API authentication
- Explore [Model Routing](model-routing) to understand how requests are directed to providers
- See the [API Reference](../api-reference/overview) for detailed endpoint documentation