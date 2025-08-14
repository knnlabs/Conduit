# Provider Integration Guide

*Last Updated: 2025-08-01*

This guide covers how to integrate, configure, and manage LLM and Audio providers in Conduit, including the multi-instance provider architecture.

## Table of Contents
- [Overview](#overview)
- [Supported Providers](#supported-providers)
- [Provider Architecture](#provider-architecture)
- [Configuration](#configuration)
- [Multi-Instance Support](#multi-instance-support)
- [Provider Health Monitoring](#provider-health-monitoring)
- [Cost Management](#cost-management)

## Overview

Conduit supports multiple AI providers through a unified interface, allowing you to:
- **Route requests** across different providers based on model availability
- **Implement fallback chains** for high availability
- **Monitor provider health** and performance
- **Manage costs** with granular tracking
- **Scale horizontally** with multiple instances of the same provider

## Supported Providers

### Text Generation (LLM)

| Provider | Models | Features | Status |
|----------|---------|----------|--------|
| **OpenAI** | GPT-4o, GPT-4, GPT-3.5-turbo, GPT-4o-mini | Chat, Function calling, Vision | ✅ Production |
| **Anthropic** | Claude 3.5 Sonnet, Claude 3 Opus/Haiku | Chat, Tool use, Vision | ✅ Production |
| **Azure OpenAI** | Azure-hosted OpenAI models | Enterprise features | ✅ Production |
| **Google Gemini** | Gemini Pro, Gemini Flash | Chat, Vision, Function calling | ✅ Production |
| **Google Vertex AI** | Gemini, PaLM models | Enterprise features | ✅ Production |
| **Amazon Bedrock** | Claude, Llama, Titan models | AWS integration | ✅ Production |
| **Cohere** | Command, Command R/R+ | Chat, RAG optimization | ✅ Production |
| **Mistral AI** | Mistral Large, Medium, Small | European provider | ✅ Production |
| **Groq** | Llama, Mixtral models | Ultra-fast inference | ✅ Production |
| **Ollama** | Local model hosting | Self-hosted models | ✅ Production |
| **Replicate** | Open source models | Community models | ✅ Production |
| **Fireworks AI** | Optimized open models | Fast inference | ✅ Production |
| **HuggingFace** | Transformers models | Open source hub | ✅ Production |
| **OpenRouter** | Multi-provider access | Provider aggregation | ✅ Production |
| **Cerebras** | Llama models | Ultra-fast inference | ✅ Production |
| **OpenAI Compatible** | Any OpenAI-compatible API | Custom deployments | ✅ Production |

### Audio Processing

| Provider | Capabilities | Features | Status |
|----------|-------------|----------|--------|
| **OpenAI** | Whisper, TTS, Realtime | Multi-language transcription | ✅ Production |
| **Azure OpenAI** | Azure Whisper, TTS | Enterprise compliance | ✅ Production |
| **ElevenLabs** | Premium TTS, Conversational | Voice cloning, streaming | ✅ Production |
| **Ultravox** | Real-time conversation | Ultra-low latency | ✅ Production |
| **Groq** | High-speed Whisper | Fastest transcription | ✅ Production |
| **Deepgram** | Real-time STT | Live transcription | ✅ Production |
| **Google Cloud** | Speech-to-Text, TTS | Advanced language support | 🔄 Coming Soon |
| **AWS** | Transcribe, Polly | AWS ecosystem integration | 🔄 Coming Soon |

### Image Generation

| Provider | Models | Features | Status |
|----------|---------|----------|--------|
| **OpenAI** | DALL-E 3, DALL-E 2 | High-quality generation | ✅ Production |
| **Replicate** | Stable Diffusion, Flux | Open source models | ✅ Production |
| **Stability AI** | Stable Diffusion models | Advanced control | 🔄 Planned |

## Provider Architecture

### Multi-Instance Design

Conduit supports multiple instances of the same provider type:

```
Provider Type: OpenAI
├── Instance 1: "OpenAI Production" (Provider ID: 1)
│   ├── API Key 1: "Primary key"
│   └── API Key 2: "Backup key"  
├── Instance 2: "OpenAI Development" (Provider ID: 2)
│   └── API Key 3: "Dev key"
└── Instance 3: "OpenAI High-Volume" (Provider ID: 3)
    ├── API Key 4: "Volume key 1"
    └── API Key 5: "Volume key 2"
```

### Key Concepts

- **Provider Type**: Category of provider (OpenAI, Anthropic, etc.)
- **Provider Instance**: Specific configuration of a provider type
- **Provider ID**: Unique identifier for each instance
- **Provider Key Credentials**: API keys associated with an instance
- **Provider Account Group**: External account separation for billing

### Entity Relationships

```csharp
public class Provider
{
    public int Id { get; set; }                    // Unique instance identifier
    public ProviderType ProviderType { get; set; } // OpenAI, Anthropic, etc.
    public string Name { get; set; }               // User-friendly name
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    
    // Navigation properties
    public ICollection<ProviderKeyCredential> KeyCredentials { get; set; }
    public ICollection<ModelProviderMapping> ModelMappings { get; set; }
}

public class ProviderKeyCredential
{
    public int Id { get; set; }
    public int ProviderId { get; set; }            // Links to Provider.Id
    public string ApiKey { get; set; }
    public string? ProviderAccountGroup { get; set; } // External billing group
    public bool IsEnabled { get; set; }
    public DateTime? LastUsed { get; set; }
    
    public Provider Provider { get; set; }
}
```

## Configuration

### Adding a New Provider Instance

```bash
# Create OpenAI production instance
curl -X POST "https://admin.conduit.ai/api/providers" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "OpenAI Production",
    "providerType": "OpenAI",
    "isEnabled": true,
    "description": "Primary OpenAI instance for production workloads"
  }'
```

### Adding API Keys

```bash
# Add API key to provider instance
curl -X POST "https://admin.conduit.ai/api/providers/1/keys" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "apiKey": "sk-your-openai-key-here",
    "providerAccountGroup": "production-account",
    "isEnabled": true
  }'
```

### Model Mapping

Map model aliases to specific provider instances:

```bash
# Map GPT-4 to specific OpenAI instance
curl -X POST "https://admin.conduit.ai/api/model-provider-mappings" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "modelAlias": "gpt-4",
    "providerId": 1,
    "actualModelName": "gpt-4-0125-preview",
    "isEnabled": true
  }'
```

### Provider Discovery

Enable automatic model discovery:

```bash
# Enable model discovery for provider
curl -X PUT "https://admin.conduit.ai/api/providers/1" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "enableModelDiscovery": true,
    "discoveryIntervalHours": 24
  }'
```

## Multi-Instance Support

### Load Balancing

Configure multiple instances for load distribution:

```json
{
  "providers": [
    {
      "name": "OpenAI Primary",
      "providerType": "OpenAI",
      "weight": 70,
      "priority": 1
    },
    {
      "name": "OpenAI Secondary", 
      "providerType": "OpenAI",
      "weight": 30,
      "priority": 1
    }
  ]
}
```

### Fallback Chains

Configure provider fallback for high availability:

```json
{
  "fallbackChains": [
    {
      "modelAlias": "gpt-4",
      "providers": [
        {
          "providerId": 1,
          "priority": 1,
          "timeout": "30s"
        },
        {
          "providerId": 2,
          "priority": 2,
          "timeout": "30s"
        }
      ]
    }
  ]
}
```

### Account Separation

Use `ProviderAccountGroup` for billing separation:

```csharp
// Different API keys for different billing accounts
var productionKeys = new[]
{
    new ProviderKeyCredential 
    { 
        ApiKey = "sk-prod-key-1", 
        ProviderAccountGroup = "production" 
    },
    new ProviderKeyCredential 
    { 
        ApiKey = "sk-dev-key-1", 
        ProviderAccountGroup = "development" 
    }
};
```

## Provider Health Monitoring

### Health Check Configuration

```csharp
services.Configure<ProviderHealthOptions>(options =>
{
    options.CheckIntervalMinutes = 5;
    options.TimeoutSeconds = 30;
    options.RetryAttempts = 3;
    options.FailureThreshold = 3; // Mark unhealthy after 3 failures
});
```

### Health Check Endpoints

```bash
# Check health of specific provider
curl -X GET "https://admin.conduit.ai/api/providers/1/health" \
  -H "X-Master-Key: your-master-key"

# Get health status for all providers
curl -X GET "https://admin.conduit.ai/api/provider-health" \
  -H "X-Master-Key: your-master-key"
```

### Health Status Response

```json
{
  "providerId": 1,
  "providerName": "OpenAI Production",
  "providerType": "OpenAI",
  "status": "Healthy",
  "lastCheckTime": "2025-08-01T12:00:00Z",
  "responseTime": 250,
  "availableModels": ["gpt-4", "gpt-4-turbo", "gpt-3.5-turbo"],
  "healthDetails": {
    "apiConnectivity": "OK",
    "authentication": "OK",
    "rateLimit": "OK",
    "modelAvailability": "OK"
  },
  "keyCredentials": [
    {
      "keyId": "key_123",
      "status": "Active",
      "lastUsed": "2025-08-01T11:45:00Z",
      "rateLimitRemaining": 4000
    }
  ]
}
```

### Automated Failover

When a provider becomes unhealthy:

1. **Health Monitor** detects failures
2. **Circuit Breaker** opens to prevent further requests
3. **Load Balancer** routes traffic to healthy instances
4. **Notifications** alert administrators via SignalR
5. **Auto-Recovery** attempts to restore service

```csharp
public class ProviderCircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan HalfOpenTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

## Cost Management

### Provider Cost Configuration

```bash
# Configure cost for specific provider instance
curl -X POST "https://admin.conduit.ai/api/model-costs" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "providerId": 1,
    "modelPattern": "gpt-4*",
    "inputCostPer1kTokens": 0.03,
    "outputCostPer1kTokens": 0.06,
    "costCalculationType": "TokenBased"
  }'
```

### Cost Tracking by Provider

```bash
# Get cost breakdown by provider
curl -X GET "https://admin.conduit.ai/api/usage/costs?groupBy=provider" \
  -H "X-Master-Key: your-master-key" \
  -G \
  -d "startDate=2025-07-01" \
  -d "endDate=2025-07-31"
```

### Provider Account Group Billing

```json
{
  "costBreakdown": [
    {
      "providerAccountGroup": "production",
      "totalCost": 245.67,
      "providers": [
        {
          "providerId": 1,
          "providerName": "OpenAI Production",
          "cost": 180.45
        },
        {
          "providerId": 3,
          "providerName": "Anthropic Production", 
          "cost": 65.22
        }
      ]
    },
    {
      "providerAccountGroup": "development",
      "totalCost": 23.45,
      "providers": [
        {
          "providerId": 2,
          "providerName": "OpenAI Development",
          "cost": 23.45
        }
      ]
    }
  ]
}
```

## Provider Registry Pattern

### Dynamic Provider Loading

```csharp
public interface IProviderRegistry
{
    ILLMClient GetProvider(int providerId);
    ILLMClient GetProviderByType(ProviderType providerType);
    Task<ILLMClient> GetBestAvailableProviderAsync(string modelAlias);
}

public class ProviderRegistry : IProviderRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IProviderService _providerService;
    
    public ILLMClient GetProvider(int providerId)
    {
        var provider = await _providerService.GetProviderAsync(providerId);
        
        return provider.ProviderType switch
        {
            ProviderType.OpenAI => _serviceProvider.GetService<OpenAIClient>(),
            ProviderType.Anthropic => _serviceProvider.GetService<AnthropicClient>(),
            ProviderType.AzureOpenAI => _serviceProvider.GetService<AzureOpenAIClient>(),
            // ... etc
            _ => throw new NotSupportedException($"Provider type {provider.ProviderType} not supported")
        };
    }
}
```

### Source Generator for Provider Registration

```csharp
// Generated code for provider registration
[GeneratedProviderRegistration]
public partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddProviders(this IServiceCollection services)
    {
        // Auto-generated registrations
        services.AddTransient<OpenAIClient>();
        services.AddTransient<AnthropicClient>();
        services.AddTransient<AzureOpenAIClient>();
        // ... all providers
        
        services.AddSingleton<IProviderRegistry, ProviderRegistry>();
        return services;
    }
}
```

## Provider Integration Examples

### Adding a New Provider Type

1. **Add to ProviderType enum**:
```csharp
public enum ProviderType
{
    // ... existing values
    NewProvider = 23
}
```

2. **Implement provider client**:
```csharp
public class NewProviderClient : BaseLLMClient, ILLMClient
{
    public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request, 
        string? apiKey = null, 
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

3. **Register in DI container**:
```csharp
services.AddTransient<NewProviderClient>();
```

4. **Add to provider registry**:
```csharp
ProviderType.NewProvider => _serviceProvider.GetService<NewProviderClient>(),
```

5. **Configure via Admin API**:
```bash
curl -X POST "https://admin.conduit.ai/api/providers" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "New Provider Instance",
    "providerType": "NewProvider",
    "isEnabled": true
  }'
```

### Custom OpenAI-Compatible Provider

```bash
# Add custom endpoint
curl -X POST "https://admin.conduit.ai/api/providers" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Custom LLM Service",
    "providerType": "OpenAICompatible",
    "baseUrl": "https://custom-llm.example.com/v1",
    "isEnabled": true
  }'
```

## Monitoring and Analytics

### Provider Performance Metrics

```bash
# Get provider performance data
curl -X GET "https://admin.conduit.ai/api/providers/analytics" \
  -H "X-Master-Key: your-master-key" \
  -G \
  -d "providerId=1" \
  -d "startDate=2025-07-01" \
  -d "endDate=2025-07-31"
```

Response:
```json
{
  "providerId": 1,
  "providerName": "OpenAI Production",
  "metrics": {
    "totalRequests": 15420,
    "successfulRequests": 15398,
    "failedRequests": 22,
    "averageResponseTime": 850,
    "medianResponseTime": 720,
    "p95ResponseTime": 1200,
    "totalTokensProcessed": 2450000,
    "totalCost": 145.67,
    "requestsPerHour": [
      { "hour": "2025-07-01T00:00:00Z", "count": 45 },
      { "hour": "2025-07-01T01:00:00Z", "count": 38 }
    ]
  }
}
```

### Health History

```bash
# Get provider health history
curl -X GET "https://admin.conduit.ai/api/providers/1/health/history" \
  -H "X-Master-Key: your-master-key" \
  -G \
  -d "hours=24"
```

### Rate Limit Monitoring

```json
{
  "providerId": 1,
  "rateLimits": [
    {
      "keyId": "key_123",
      "requestsPerMinute": {
        "limit": 3500,
        "remaining": 3420,
        "resetsAt": "2025-08-01T12:01:00Z"
      },
      "tokensPerMinute": {
        "limit": 90000,
        "remaining": 85400,
        "resetsAt": "2025-08-01T12:01:00Z"
      }
    }
  ]
}
```

## Security Considerations

### API Key Management

- **Encryption**: All API keys encrypted at rest using AES-256
- **Rotation**: Regular key rotation recommended
- **Auditing**: All key usage logged and tracked
- **Separation**: Use different keys for different environments

### Access Controls

```json
{
  "providerSecurity": {
    "allowedIPs": ["192.168.1.0/24"],
    "requireMTLS": false,
    "keyRotationDays": 90,
    "auditLevel": "Full"
  }
}
```

### Provider-Specific Security

Different providers may have specific security requirements:

- **Azure OpenAI**: Managed identity integration
- **Bedrock**: IAM role-based access
- **Vertex AI**: Service account authentication
- **OpenAI**: Organization and project-level keys

## Best Practices

### Provider Configuration

1. **Use descriptive names** for provider instances
2. **Group related providers** with consistent naming
3. **Configure health checks** for all production providers
4. **Set up monitoring** and alerting
5. **Document provider purposes** in descriptions

### Key Management

1. **Rotate keys regularly** (every 90 days)
2. **Use separate keys** for different environments
3. **Monitor key usage** and rate limits
4. **Implement key fallback** for high availability
5. **Track costs per key** for billing accuracy

### Performance Optimization

1. **Load balance** across multiple instances
2. **Configure circuit breakers** to prevent cascade failures
3. **Monitor response times** and adjust timeouts
4. **Use appropriate retry policies** for different error types
5. **Cache provider health status** to reduce check overhead

## Troubleshooting

### Common Issues

#### Provider Not Found
```json
{
  "error": "Provider with ID 999 not found",
  "code": "PROVIDER_NOT_FOUND"
}
```
**Solution**: Verify provider ID exists in database

#### Invalid API Key
```json
{
  "error": "Invalid API key for provider OpenAI",
  "code": "INVALID_API_KEY"  
}
```
**Solution**: Check key validity and rotation status

#### Provider Unhealthy
```json
{
  "error": "Provider OpenAI Production is currently unhealthy",
  "code": "PROVIDER_UNHEALTHY"
}
```
**Solution**: Check provider health status and configuration

### Diagnostic Commands

```bash
# Test provider connectivity
curl -X POST "https://admin.conduit.ai/api/providers/1/test" \
  -H "X-Master-Key: your-master-key"

# Get provider configuration
curl -X GET "https://admin.conduit.ai/api/providers/1" \
  -H "X-Master-Key: your-master-key"

# List all provider instances
curl -X GET "https://admin.conduit.ai/api/providers" \
  -H "X-Master-Key: your-master-key"
```

## Related Documentation

- [Provider Multi-Instance Architecture](../architecture/provider-multi-instance.md)
- [Model Cost Mapping](../architecture/model-cost-mapping.md) 
- [Provider Health Monitoring](../Health-Monitoring-Guide.md)
- [Audio Provider Integration](./audio/README.md)
- [Security Guidelines](./security.md)

---

*For provider-specific configuration details, see the individual provider documentation in the [Claude documentation directory](../claude/provider-models.md).*