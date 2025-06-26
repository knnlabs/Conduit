---
sidebar_position: 3
title: Provider Configuration
description: Comprehensive guide to configuring and managing LLM providers through the Admin API
---

# Provider Configuration

Provider configuration in Conduit manages credentials, health monitoring, model capabilities, and routing configurations for all supported LLM providers. This guide covers complete provider lifecycle management through the Admin API.

## Provider Architecture

### Supported Providers

**Text & Chat Providers:**
- OpenAI (GPT-3.5, GPT-4, GPT-4o)
- Anthropic (Claude 3 family)
- Google (Gemini Pro, Gemini Ultra)
- Azure OpenAI
- AWS Bedrock
- Cohere
- Mistral AI
- Groq
- Together AI
- Perplexity

**Audio Providers:**
- OpenAI (Whisper, TTS, Realtime API)
- ElevenLabs (TTS, Conversational AI)
- Azure Speech Services
- Google Cloud Speech
- Deepgram
- Ultravox

**Image/Video Providers:**
- OpenAI (DALL-E 2, DALL-E 3)
- MiniMax (Image & Video generation)
- Replicate
- Stability AI
- Midjourney (via API)

### Provider States

```
Provider Lifecycle:
Unconfigured → Configured → Healthy → Active
     ↓             ↓         ↓        ↓
   Error ←――――― Error ←――― Unhealthy → Disabled
```

## Provider Credential Management

### Adding Provider Credentials

**OpenAI Configuration:**
```bash
curl -X POST http://localhost:5002/api/admin/providers/openai/credentials \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "providerName": "openai",
    "displayName": "OpenAI",
    "apiKey": "sk-your-openai-api-key",
    "organizationId": "org-your-organization-id",
    "projectId": "proj-your-project-id",
    "isEnabled": true,
    "priority": 1,
    "configuration": {
      "baseUrl": "https://api.openai.com/v1",
      "timeout": 30000,
      "maxRetries": 3,
      "retryDelay": 1000
    },
    "rateLimits": {
      "requestsPerMinute": 3500,
      "tokensPerMinute": 200000,
      "requestsPerDay": 10000
    },
    "modelMappings": {
      "gpt-4": "gpt-4-0613",
      "gpt-3.5-turbo": "gpt-3.5-turbo-0125"
    }
  }'
```

**Anthropic Configuration:**
```bash
curl -X POST http://localhost:5002/api/admin/providers/anthropic/credentials \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "providerName": "anthropic",
    "displayName": "Anthropic",
    "apiKey": "sk-ant-your-anthropic-key",
    "isEnabled": true,
    "priority": 2,
    "configuration": {
      "baseUrl": "https://api.anthropic.com",
      "anthropicVersion": "2023-06-01",
      "maxTokens": 100000,
      "timeout": 60000
    },
    "rateLimits": {
      "requestsPerMinute": 1000,
      "tokensPerMinute": 80000
    },
    "modelMappings": {
      "claude-3-haiku": "claude-3-haiku-20240307",
      "claude-3-sonnet": "claude-3-sonnet-20240229",
      "claude-3-opus": "claude-3-opus-20240229"
    }
  }'
```

**Azure OpenAI Configuration:**
```bash
curl -X POST http://localhost:5002/api/admin/providers/azure-openai/credentials \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "providerName": "azure-openai",
    "displayName": "Azure OpenAI",
    "apiKey": "your-azure-api-key",
    "isEnabled": true,
    "priority": 3,
    "configuration": {
      "endpoint": "https://your-resource.openai.azure.com",
      "apiVersion": "2024-02-15-preview",
      "deployment": "gpt-4-deployment",
      "resourceGroup": "your-resource-group",
      "subscriptionId": "your-subscription-id"
    },
    "modelMappings": {
      "gpt-4": "gpt-4-deployment",
      "gpt-3.5-turbo": "gpt-35-turbo-deployment"
    }
  }'
```

### Audio Provider Configuration

**ElevenLabs Configuration:**
```bash
curl -X POST http://localhost:5002/api/admin/providers/elevenlabs/credentials \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "providerName": "elevenlabs",
    "displayName": "ElevenLabs",
    "apiKey": "your-elevenlabs-api-key",
    "isEnabled": true,
    "priority": 1,
    "capabilities": ["text-to-speech", "voice-cloning", "real-time-audio"],
    "configuration": {
      "baseUrl": "https://api.elevenlabs.io/v1",
      "defaultVoice": "21m00Tcm4TlvDq8ikWAM",
      "stability": 0.5,
      "similarityBoost": 0.5,
      "style": 0.0,
      "useSpeakerBoost": true
    },
    "rateLimits": {
      "requestsPerMinute": 120,
      "charactersPerMonth": 10000000
    }
  }'
```

**Deepgram Configuration:**
```bash
curl -X POST http://localhost:5002/api/admin/providers/deepgram/credentials \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "providerName": "deepgram",
    "displayName": "Deepgram",
    "apiKey": "your-deepgram-api-key",
    "isEnabled": true,
    "priority": 1,
    "capabilities": ["speech-to-text", "real-time-transcription"],
    "configuration": {
      "baseUrl": "https://api.deepgram.com/v1",
      "model": "nova-2",
      "language": "en-US",
      "punctuate": true,
      "diarize": true,
      "smartFormat": true
    },
    "rateLimits": {
      "requestsPerMinute": 1000,
      "concurrentConnections": 100
    }
  }'
```

## Provider Health Monitoring

### Health Check Configuration

```bash
curl -X POST http://localhost:5002/api/admin/providers/health-monitoring \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "checkInterval": 300,        // 5 minutes
    "timeout": 30000,           // 30 seconds
    "retryAttempts": 3,
    "retryDelay": 5000,
    "healthCheckMethods": [
      "api-ping",
      "model-test",
      "rate-limit-check"
    ],
    "alerting": {
      "enabled": true,
      "webhookUrl": "https://your-app.com/webhooks/provider-health",
      "alertOnStatusChange": true,
      "alertOnDegraded": true,
      "minimumFailures": 3
    }
  }'
```

### Real-Time Health Status

**Get All Provider Health:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/providers/health
```

Response:
```json
{
  "success": true,
  "data": {
    "overallHealth": "Healthy",
    "totalProviders": 8,
    "healthyProviders": 7,
    "unhealthyProviders": 1,
    "lastCheckTime": "2024-01-15T10:30:00Z",
    "providers": {
      "openai": {
        "status": "Healthy",
        "lastCheck": "2024-01-15T10:30:00Z",
        "responseTime": 245,
        "availableModels": 12,
        "rateLimitStatus": "Normal",
        "errorRate": 0.01,
        "uptime": 0.9995
      },
      "anthropic": {
        "status": "Healthy",
        "lastCheck": "2024-01-15T10:30:00Z",
        "responseTime": 189,
        "availableModels": 6,
        "rateLimitStatus": "Normal",
        "errorRate": 0.005,
        "uptime": 0.9998
      },
      "google": {
        "status": "Unhealthy",
        "lastCheck": "2024-01-15T10:29:00Z",
        "error": "Connection timeout",
        "lastHealthyTime": "2024-01-15T09:45:00Z",
        "consecutiveFailures": 4
      }
    }
  }
}
```

**Get Specific Provider Health:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/providers/openai/health
```

### Health History and Analytics

**Provider Health History:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/providers/openai/health/history?period=7d&granularity=hour"
```

Response:
```json
{
  "success": true,
  "data": {
    "provider": "openai",
    "period": "7d",
    "granularity": "hour",
    "metrics": {
      "uptime": 0.9995,
      "averageResponseTime": 234,
      "totalRequests": 125680,
      "successfulRequests": 125512,
      "failedRequests": 168,
      "errorRate": 0.0013
    },
    "timeline": [
      {
        "timestamp": "2024-01-15T10:00:00Z",
        "status": "Healthy",
        "responseTime": 245,
        "requests": 1248,
        "errors": 2
      }
    ],
    "incidents": [
      {
        "startTime": "2024-01-14T15:30:00Z",
        "endTime": "2024-01-14T15:45:00Z",
        "duration": 900,
        "type": "Degraded",
        "cause": "High latency"
      }
    ]
  }
}
```

## Model Capability Discovery

### Automatic Discovery

Conduit automatically discovers provider capabilities:

```bash
curl -X POST http://localhost:5002/api/admin/providers/openai/discover-capabilities \
  -H "Authorization: Bearer your-master-key"
```

Response:
```json
{
  "success": true,
  "data": {
    "provider": "openai",
    "discoveredAt": "2024-01-15T10:30:00Z",
    "capabilities": {
      "chat": {
        "models": [
          {
            "id": "gpt-4-0613",
            "displayName": "GPT-4",
            "contextLength": 8192,
            "maxOutputTokens": 4096,
            "inputCostPer1kTokens": 0.03,
            "outputCostPer1kTokens": 0.06,
            "supports": {
              "functionCalling": true,
              "streaming": true,
              "systemPrompts": true,
              "images": true,
              "jsonMode": true
            }
          },
          {
            "id": "gpt-3.5-turbo-0125",
            "displayName": "GPT-3.5 Turbo",
            "contextLength": 16385,
            "maxOutputTokens": 4096,
            "inputCostPer1kTokens": 0.0005,
            "outputCostPer1kTokens": 0.0015,
            "supports": {
              "functionCalling": true,
              "streaming": true,
              "systemPrompts": true,
              "jsonMode": true
            }
          }
        ]
      },
      "embeddings": {
        "models": [
          {
            "id": "text-embedding-3-large",
            "displayName": "Text Embedding 3 Large",
            "dimensions": 3072,
            "maxInputTokens": 8191,
            "costPer1kTokens": 0.00013
          }
        ]
      },
      "audio": {
        "transcription": {
          "models": ["whisper-1"],
          "supportedFormats": ["mp3", "mp4", "mpeg", "mpga", "m4a", "wav", "webm"],
          "maxFileSize": 26214400
        },
        "textToSpeech": {
          "models": ["tts-1", "tts-1-hd"],
          "voices": ["alloy", "echo", "fable", "onyx", "nova", "shimmer"],
          "outputFormats": ["mp3", "opus", "aac", "flac"]
        }
      },
      "images": {
        "generation": {
          "models": ["dall-e-2", "dall-e-3"],
          "sizes": ["256x256", "512x512", "1024x1024", "1792x1024", "1024x1792"],
          "qualities": ["standard", "hd"]
        }
      }
    }
  }
}
```

### Manual Capability Updates

```bash
curl -X PUT http://localhost:5002/api/admin/providers/openai/capabilities \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "chat": {
      "models": [
        {
          "id": "gpt-4-turbo-preview",
          "displayName": "GPT-4 Turbo Preview",
          "contextLength": 128000,
          "maxOutputTokens": 4096,
          "inputCostPer1kTokens": 0.01,
          "outputCostPer1kTokens": 0.03,
          "supports": {
            "functionCalling": true,
            "streaming": true,
            "systemPrompts": true,
            "images": true,
            "jsonMode": true
          }
        }
      ]
    }
  }'
```

## Model Routing Configuration

### Provider Priority and Routing

**Set Provider Priorities:**
```bash
curl -X PUT http://localhost:5002/api/admin/providers/routing \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "defaultStrategy": "priority",
    "fallbackEnabled": true,
    "healthCheckEnabled": true,
    "providers": [
      {
        "name": "openai",
        "priority": 1,
        "weight": 0.7,
        "enabled": true,
        "models": ["gpt-4", "gpt-3.5-turbo"]
      },
      {
        "name": "anthropic", 
        "priority": 2,
        "weight": 0.3,
        "enabled": true,
        "models": ["claude-3-sonnet", "claude-3-haiku"]
      }
    ],
    "modelRouting": {
      "gpt-4": {
        "providers": [
          {"name": "openai", "priority": 1},
          {"name": "azure-openai", "priority": 2}
        ]
      },
      "claude-3-sonnet": {
        "providers": [
          {"name": "anthropic", "priority": 1}
        ]
      }
    }
  }'
```

### Load Balancing Strategies

**Round Robin Configuration:**
```bash
curl -X PUT http://localhost:5002/api/admin/providers/load-balancing \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "strategy": "round-robin",
    "providers": [
      {"name": "openai", "weight": 50},
      {"name": "azure-openai", "weight": 30},
      {"name": "anthropic", "weight": 20}
    ],
    "stickySessionEnabled": false,
    "healthCheckEnabled": true,
    "retryOnFailure": true,
    "maxRetries": 3
  }'
```

**Cost-Based Routing:**
```bash
curl -X PUT http://localhost:5002/api/admin/providers/cost-optimization \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "strategy": "lowest-cost",
    "qualityThreshold": 0.8,
    "costSavingsTarget": 0.3,
    "modelPreferences": {
      "gpt-4": {
        "preferredProvider": "azure-openai",
        "fallbackProvider": "openai",
        "costThreshold": 0.02
      }
    }
  }'
```

## Provider Analytics

### Usage Analytics

**Provider Usage Summary:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/providers/analytics/usage?period=30d"
```

Response:
```json
{
  "success": true,
  "data": {
    "period": "30d",
    "totalRequests": 1250000,
    "totalCost": 15234.56,
    "providers": {
      "openai": {
        "requests": 875000,
        "percentage": 0.7,
        "cost": 10678.23,
        "averageLatency": 1.23,
        "errorRate": 0.012,
        "topModels": [
          {"model": "gpt-4", "requests": 450000, "cost": 8234.45},
          {"model": "gpt-3.5-turbo", "requests": 425000, "cost": 2443.78}
        ]
      },
      "anthropic": {
        "requests": 300000,
        "percentage": 0.24,
        "cost": 3678.90,
        "averageLatency": 1.45,
        "errorRate": 0.008,
        "topModels": [
          {"model": "claude-3-sonnet", "requests": 200000, "cost": 2456.78},
          {"model": "claude-3-haiku", "requests": 100000, "cost": 1222.12}
        ]
      },
      "google": {
        "requests": 75000,
        "percentage": 0.06,
        "cost": 877.43,
        "averageLatency": 0.98,
        "errorRate": 0.015
      }
    }
  }
}
```

### Performance Analytics

**Provider Performance Comparison:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/providers/analytics/performance?period=7d&groupBy=provider"
```

### Cost Analytics

**Provider Cost Analysis:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/providers/analytics/costs?period=30d&breakdown=model"
```

## Provider Configuration Management

### Configuration Export/Import

**Export Provider Configurations:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/providers/export?includeCredentials=false" > providers-config.json
```

**Import Provider Configurations:**
```bash
curl -X POST http://localhost:5002/api/admin/providers/import \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d @providers-config.json
```

### Bulk Operations

**Bulk Provider Updates:**
```bash
curl -X PATCH http://localhost:5002/api/admin/providers/bulk \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "providers": ["openai", "anthropic", "google"],
    "updates": {
      "rateLimits": {
        "requestsPerMinute": 2000
      },
      "timeout": 60000
    }
  }'
```

**Bulk Health Check:**
```bash
curl -X POST http://localhost:5002/api/admin/providers/bulk-health-check \
  -H "Authorization: Bearer your-master-key" \
  -d '{
    "providers": ["openai", "anthropic", "google"],
    "checkType": "full",
    "async": true
  }'
```

## Event-Driven Provider Management

### Provider Events

When providers are updated, events are published for real-time coordination:

```json
{
  "eventType": "ProviderCredentialUpdated",
  "providerId": "openai",
  "timestamp": "2024-01-15T10:30:00Z",
  "changedProperties": ["apiKey", "rateLimits"],
  "data": {
    "isEnabled": true,
    "priority": 1,
    "rateLimits": {
      "requestsPerMinute": 4000
    }
  }
}
```

### Capability Discovery Events

```json
{
  "eventType": "ModelCapabilitiesDiscovered",
  "providerId": "openai",
  "timestamp": "2024-01-15T10:30:00Z",
  "data": {
    "newModels": ["gpt-4-turbo-preview"],
    "updatedModels": ["gpt-4"],
    "removedModels": [],
    "totalModels": 15
  }
}
```

## Security and Compliance

### Credential Security

```json
{
  "credentialSecurity": {
    "encryption": {
      "algorithm": "AES-256-GCM",
      "keyRotation": "monthly"
    },
    "accessControl": {
      "requireMasterKey": true,
      "auditLogging": true,
      "fieldLevelPermissions": true
    },
    "storage": {
      "encrypted": true,
      "backupEncrypted": true,
      "keyVault": "azure-key-vault"
    }
  }
}
```

### Audit Logging

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "level": "Information",
  "message": "Provider credentials updated",
  "properties": {
    "operation": "Provider.UpdateCredentials",
    "provider": "openai",
    "changedFields": ["apiKey", "rateLimits"],
    "performedBy": "admin",
    "ipAddress": "192.168.1.100",
    "correlationId": "req-abc123"
  }
}
```

### Compliance Features

- **PCI DSS**: Encrypted credential storage
- **SOC 2**: Audit logging and access controls
- **GDPR**: Data anonymization and deletion
- **HIPAA**: Healthcare-compliant configurations

## Troubleshooting

### Common Provider Issues

**Provider Authentication Failures:**
```bash
# Test provider credentials
curl -X POST http://localhost:5002/api/admin/providers/openai/test \
  -H "Authorization: Bearer your-master-key"

# Check credential status
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/providers/openai/credentials/status
```

**Rate Limiting Issues:**
```bash
# Check rate limit status
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/providers/openai/rate-limits

# View rate limit history
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/providers/openai/rate-limit-events?period=1h"
```

**Model Availability Issues:**
```bash
# Refresh model capabilities
curl -X POST http://localhost:5002/api/admin/providers/openai/refresh-capabilities \
  -H "Authorization: Bearer your-master-key"

# Check model mappings
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/providers/openai/model-mappings
```

### Debug Logging

```bash
# Enable debug logging for specific provider
curl -X PUT http://localhost:5002/api/admin/providers/openai/logging \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "level": "Debug",
    "includeRequestBodies": false,
    "includeResponseBodies": false,
    "duration": 3600
  }'
```

## Best Practices

### Provider Configuration

1. **Diversify Providers**: Use multiple providers for redundancy
2. **Monitor Health**: Set up automated health monitoring
3. **Cost Optimization**: Configure cost-based routing
4. **Security**: Rotate credentials regularly
5. **Performance**: Monitor latency and error rates

### Scaling Considerations

1. **Rate Limits**: Configure realistic rate limits
2. **Circuit Breakers**: Implement failure protection
3. **Load Balancing**: Distribute load across providers
4. **Caching**: Cache capabilities and configurations
5. **Monitoring**: Track all provider metrics

## Next Steps

- **Usage & Billing**: Monitor [provider costs and usage](usage-billing)
- **Virtual Keys**: Configure [virtual key permissions](virtual-keys)
- **WebUI Guide**: Use the [administrative interface](webui-guide)
- **Core APIs**: Learn about [using providers with Core APIs](../core-apis/overview)