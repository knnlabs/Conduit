---
sidebar_position: 4
title: Provider Integration
description: Learn about Conduit's support for multiple LLM providers
---

# Provider Integration

Conduit supports a wide range of LLM providers, allowing you to switch between services without changing your application code.

## Supported Providers

Conduit integrates with many popular LLM providers:

| Provider | Supported Models | Key Features |
|----------|-----------------|--------------|
| OpenAI | GPT-3.5, GPT-4, etc. | Chat, Completions, Embeddings, Images |
| Anthropic | Claude 3 (Opus, Sonnet, Haiku) | Chat with long context windows |
| Azure OpenAI | Same as OpenAI | Azure-specific deployments |
| Google Gemini | Gemini Pro, Ultra | Chat, Embeddings, Vision |
| Cohere | Command, Embed | Chat, Completions, Embeddings |
| Mistral | Mistral Small, Medium, Large | Efficient chat models |
| AWS Bedrock | Claude, Llama 2, etc. | AWS-integrated models |
| Groq | Llama 2, Mixtral, etc. | High-speed inference |
| Replicate | Various open models | Wide range of specialized models |
| HuggingFace | Thousands of models | Open-source model variety |
| Ollama | Local open models | Self-hosted models |
| VertexAI | Google's enterprise AI | Enterprise-grade AI solutions |
| SageMaker | Custom models | AWS ML deployments |

## Adding Provider Credentials

To add a new provider:

1. Navigate to **Configuration > Provider Credentials**
2. Click **Add Provider Credential**
3. Select the provider type
4. Enter your API key and other required credentials
5. Save the configuration

Different providers may require various credential types:
- API Keys (most providers)
- Organization IDs (some providers)
- Project IDs (Google)
- Deployment IDs (Azure)
- Region settings (AWS)

## Model Mappings

After adding provider credentials, you need to create model mappings:

1. Navigate to **Configuration > Model Mappings**
2. Click **Add Model Mapping**
3. Define:
   - **Virtual Model Name**: The name clients will use
   - **Provider Model**: The actual provider model
   - **Provider**: The provider to use
   - **Priority**: Used for routing decisions

## Provider-Specific Features

Conduit normalizes provider features where possible, but also exposes provider-specific capabilities:

- **Vision Models**: Available through multimodal input support
- **Function Calling**: Supported for providers that offer it
- **Streaming**: Enabled for all providers that support it
- **Long Context**: Available for models that support extended contexts

## Custom Provider Integration

For on-premise or custom LLM deployments:

1. Navigate to **Configuration > Provider Credentials**
2. Select "Custom Provider" type
3. Configure the endpoint URL and authentication
4. Create model mappings as needed

## Monitoring Provider Health

Conduit includes provider health monitoring:

1. Navigate to **Provider Health**
2. View the status of each configured provider
3. Configure health check settings
4. Set up notifications for provider issues

## Next Steps

- Learn about [Model Routing](model-routing) to understand how requests are directed to providers
- Explore [Multimodal Support](multimodal-support) for handling images and other media
- See the [API Reference](../api-reference/overview) for details on provider-specific parameters