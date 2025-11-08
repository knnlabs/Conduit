# Provider System Architecture

This document describes the comprehensive provider architecture in ConduitLLM, including multi-instance support, provider registry pattern, and model mapping.

## Table of Contents

1. [Overview](#overview)
2. [Core Architecture](#core-architecture)
3. [Multi-Instance Support](#multi-instance-support)
4. [Provider Registry Pattern](#provider-registry-pattern)
5. [Provider Configuration](#provider-configuration)
6. [Provider Health Monitoring](#provider-health-monitoring)
7. [Integration with Router](#integration-with-router)
8. [Best Practices](#best-practices)
9. [Adding New Providers](#adding-new-providers)

---

## Overview

ConduitLLM supports integration with multiple LLM providers through a unified interface. The provider system enables:

- **Multiple instances of the same provider type** (e.g., multiple OpenAI configurations)
- **Centralized provider metadata** through the Provider Registry Pattern
- **Automatic discovery** of provider capabilities
- **Provider health monitoring** with automatic key management
- **Unified API abstraction** for provider-agnostic applications

**Important Architecture Note**: ConduitLLM supports multiple instances of the same provider type (e.g., multiple OpenAI configurations with different API keys or endpoints). Each provider instance is identified by a unique Provider ID, not by its ProviderType.

---

## Core Architecture

### Provider Entities

The provider system uses two core entities:

#### 1. Provider Entity

The `Provider` entity represents a configured instance of an LLM provider:

```csharp
public class Provider
{
    public int Id { get; set; }                    // Unique identifier
    public ProviderType ProviderType { get; set; } // Provider category (OpenAI, Anthropic, etc.)
    public string ProviderName { get; set; }       // User-friendly instance name
    public string? BaseUrl { get; set; }           // Optional custom endpoint
    public bool IsEnabled { get; set; }            // Active/inactive status
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public ICollection<ProviderKeyCredential> ProviderKeyCredentials { get; set; }
}
```

#### 2. ProviderKeyCredential Entity

Each provider can have multiple API keys for load balancing and failover:

```csharp
public class ProviderKeyCredential
{
    public int Id { get; set; }
    public int ProviderId { get; set; }              // FK to Provider
    public short ProviderAccountGroup { get; set; }  // External account grouping (0-32)
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }             // Override provider's base URL
    public string? Organization { get; set; }        // Organization/Project ID
    public string? KeyName { get; set; }             // Human-readable identifier
    public bool IsPrimary { get; set; }              // Primary key flag
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public Provider Provider { get; set; }
}
```

### Supported Providers

ConduitLLM currently supports the following LLM providers:

| Provider   | Description                         | Vision Support | Documentation | API Key URL |
|------------|-------------------------------------|---------------|---------------|-------------|
| OpenAI     | Provider of GPT models              | Yes           | [OpenAI API Docs](https://platform.openai.com/docs/api-reference) | [Get API Key](https://platform.openai.com/api-keys) |
| Anthropic  | Provider of Claude models           | Planned       | [Anthropic API Docs](https://docs.anthropic.com/claude/reference) | [Get API Key](https://console.anthropic.com/keys) |
| Cohere     | Provider of Command models          | No            | [Cohere API Docs](https://docs.cohere.com/reference/about) | [Get API Key](https://dashboard.cohere.com/api-keys) |
| Gemini     | Google's generative AI              | Partial       | [Gemini API Docs](https://ai.google.dev/docs) | [Get API Key](https://makersuite.google.com/app/apikey) |
| Fireworks  | Various fine-tuned models           | No            | [Fireworks API Docs](https://docs.fireworks.ai/api) | [Get API Key](https://app.fireworks.ai/users/settings/api-keys) |
| OpenRouter | Meta-provider (routes to many)      | Dependent     | [OpenRouter API Docs](https://openrouter.ai/docs) | [Get API Key](https://openrouter.ai/keys) |

- Only providers with "Yes" or "Partial" vision support will process image content. Others will ignore images and process only text parts of a message.
- If a vision request is routed to a provider without vision support, ConduitLLM will fallback to text-only handling or return an error, depending on your router configuration.
- For OpenRouter, vision/model support depends on the selected backend provider.

---

## Multi-Instance Support

### Key Concepts

#### Provider ID vs ProviderType

- **Provider ID**: The unique identifier for a provider instance. This is the canonical way to reference providers.
- **ProviderType**: An enum that categorizes providers (OpenAI, Anthropic, etc.). Multiple providers can share the same type.

```csharp
// Example: Two OpenAI providers
Provider prod = new Provider {
    Id = 1,
    ProviderType = ProviderType.OpenAI,
    ProviderName = "Production OpenAI"
};

Provider dev = new Provider {
    Id = 2,
    ProviderType = ProviderType.OpenAI,
    ProviderName = "Development OpenAI"
};
```

#### ProviderAccountGroup

The `ProviderAccountGroup` field (0-32) represents which external provider account a key belongs to:

- Keys with the same `ProviderAccountGroup` share rate limits and quotas
- Used for intelligent failover - if one account hits limits, switch to a different group
- Value 0 is the default group
- Not related to internal Conduit user accounts

Example scenario:
```csharp
// Company has two OpenAI accounts with different billing
var key1 = new ProviderKeyCredential {
    ProviderAccountGroup = 1,  // Account A
    ApiKey = "sk-accountA-key1"
};
var key2 = new ProviderKeyCredential {
    ProviderAccountGroup = 1,  // Account A (shares limits with key1)
    ApiKey = "sk-accountA-key2"
};
var key3 = new ProviderKeyCredential {
    ProviderAccountGroup = 2,  // Account B (independent limits)
    ApiKey = "sk-accountB-key1"
};
```

#### Primary Key Selection

Each provider can have one primary key:
- The primary key is used by default
- If no primary is set, the first enabled key is used
- Non-primary keys are used for failover

### Multi-Key Support

Providers can have multiple API keys for:
- **Load Balancing**: Distribute requests across multiple keys
- **Failover**: Automatically switch to backup keys on failure
- **Account Separation**: Keys with different ProviderAccountGroup values represent different external accounts with separate rate limits

### Common Deployment Scenarios

#### Scenario 1: Multiple OpenAI Configurations

```json
// Provider 1: Production with high quota
{
  "id": 1,
  "providerType": "OpenAI",
  "providerName": "OpenAI Production",
  "keys": [
    { "apiKey": "sk-prod-1", "providerAccountGroup": 1, "isPrimary": true },
    { "apiKey": "sk-prod-2", "providerAccountGroup": 1 }
  ]
}

// Provider 2: Development with lower quota
{
  "id": 2,
  "providerType": "OpenAI",
  "providerName": "OpenAI Development",
  "keys": [
    { "apiKey": "sk-dev-1", "providerAccountGroup": 2 }
  ]
}
```

#### Scenario 2: Geographic Distribution

```json
// Azure OpenAI in different regions
{
  "id": 3,
  "providerType": "AzureOpenAI",
  "providerName": "Azure OpenAI - East US",
  "baseUrl": "https://eastus.openai.azure.com"
}
{
  "id": 4,
  "providerType": "AzureOpenAI",
  "providerName": "Azure OpenAI - West Europe",
  "baseUrl": "https://westeurope.openai.azure.com"
}
```

---

## Provider Registry Pattern

The Provider Registry pattern provides a centralized, single source of truth for all provider metadata in the ConduitLLM system.

### Architecture Components

#### 1. IProviderMetadata Interface
   - Defines the contract for provider-specific metadata
   - Includes capabilities, authentication requirements, and configuration hints
   - Located in `ConduitLLM.Core/Interfaces/IProviderMetadata.cs`

#### 2. BaseProviderMetadata Abstract Class
   - Provides common implementation for provider metadata
   - Includes validation logic and helper methods
   - Located in `ConduitLLM.Core/Providers/BaseProviderMetadata.cs`

#### 3. IProviderMetadataRegistry Interface
   - Defines the registry contract for managing provider metadata
   - Provides methods for querying and discovering providers
   - Located in `ConduitLLM.Core/Interfaces/IProviderMetadataRegistry.cs`

#### 4. ProviderMetadataRegistry Implementation
   - Automatically discovers and registers all provider metadata implementations
   - Provides caching and efficient lookup
   - Located in `ConduitLLM.Core/Services/ProviderMetadataRegistry.cs`

#### 5. Provider-Specific Metadata Classes
   - One class per provider type (e.g., `OpenAIProviderMetadata`)
   - Located in `ConduitLLM.Core/Providers/Metadata/`

### Key Features

#### Automatic Discovery
The registry automatically discovers all `IProviderMetadata` implementations at startup using reflection:
```csharp
var providerMetadataTypes = AppDomain.CurrentDomain.GetAssemblies()
    .SelectMany(a => a.GetTypes())
    .Where(t => t.IsClass && !t.IsAbstract &&
                typeof(IProviderMetadata).IsAssignableFrom(t))
    .ToList();
```

#### Type-Safe Provider Lookup
Uses the strongly-typed `ProviderType` enum for lookups:
```csharp
var metadata = registry.GetMetadata(ProviderType.OpenAI);
```

#### Feature-Based Discovery
Find providers by their capabilities:
```csharp
var imageProviders = registry.GetProvidersByFeature(f => f.ImageGeneration);
```

#### Configuration Validation
Each provider can validate its configuration:
```csharp
var result = metadata.ValidateConfiguration(configDict);
if (!result.IsValid)
{
    // Handle validation errors
}
```

### Benefits

1. **Single Source of Truth**: All provider metadata is defined in one place
2. **Type Safety**: Uses strongly-typed enums instead of magic strings
3. **Discoverability**: Providers can be discovered by their capabilities
4. **Extensibility**: New providers can be added without modifying existing code
5. **Testability**: Clean separation of concerns enables easy testing
6. **Performance**: Metadata is cached at startup for fast lookups

---

## Provider Configuration

### Adding a Provider

Providers are managed through the Admin API. Each provider can have multiple API keys:

#### Creating a Provider

First, create the provider instance:

```
POST /api/admin/providers
```

```json
{
  "providerType": "OpenAI",
  "providerName": "Production OpenAI",
  "baseUrl": "https://api.openai.com/v1",
  "isEnabled": true
}
```

#### Adding API Keys to a Provider

After creating a provider, add one or more API keys:

```
POST /api/admin/providers/{providerId}/keys
```

```json
{
  "apiKey": "sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "keyName": "Primary Key",
  "isPrimary": true,
  "providerAccountGroup": 0,
  "isEnabled": true
}
```

### Provider-Specific Configuration

Each provider may have unique configuration requirements:

#### OpenAI

- API Key format: `sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://api.openai.com/v1`
- Optional organization ID for enterprise accounts

#### Anthropic

- API Key format: `sk-ant-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://api.anthropic.com`
- Version header is automatically handled

#### Cohere

- API Key format: `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://api.cohere.ai`

#### Gemini

- API Key format: `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://generativelanguage.googleapis.com`

#### Fireworks

- API Key format: `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://api.fireworks.ai/inference`

#### OpenRouter

- API Key format: `sk-or-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://openrouter.ai/api`
- Optional referer and site information

---

## Provider Health Monitoring

ConduitLLM includes a provider health monitoring system that actively checks the status of each configured provider to ensure they are available and responding correctly.

### Status Types

Providers can have one of three status types:

- **Online** (green): The provider is responding correctly and API keys are validated
- **Offline** (red): The provider is not responding or is returning errors
- **Unknown** (gray): The provider's status cannot be determined with certainty

### Provider-Specific Behaviors

Different providers have different health check behaviors:

| Provider   | Status Check Method | API Key Validation | Notes |
|------------|---------------------|-------------------|-------|
| OpenAI     | `/v1/models` endpoint | Yes | Full validation |
| Anthropic  | `/v1/models` endpoint | Yes | Full validation |
| Cohere     | `/v1/models` endpoint | Yes | Full validation |
| Gemini     | Models endpoint | Yes | Full validation |
| Fireworks  | Models endpoint | Yes | Full validation |
| OpenRouter | `/api/v1/models` endpoint | No | Shows as "Unknown" status because API keys aren't validated at this endpoint |

> **Note for OpenRouter**: OpenRouter does not validate API keys at the `/api/v1/models` endpoint used for health checks, therefore its status will appear as "Unknown" even when it's functioning correctly. This is expected behavior and doesn't indicate a problem with your OpenRouter configuration.

### Health Check Process

The health monitoring system:

1. Periodically checks each provider's status (default: every 5 minutes)
2. Tests connectivity to the provider's API endpoint
3. Validates API key correctness when possible
4. Records response times and error details
5. Updates the status in the database and UI

### Viewing Provider Health Status

Provider health status is displayed in multiple locations:

1. **Dashboard**: The home page shows status indicators for all providers
2. **Provider Configuration**: The provider list shows detailed status
3. **Provider Health Page**: A dedicated page shows comprehensive health metrics

### Troubleshooting Provider Health

If a provider shows as **Offline**, check:

1. API key validity and expiration
2. Network connectivity to the provider's endpoints
3. Provider service status (check the provider's status page)
4. Rate limiting or quota issues

If a provider shows as **Unknown**:

1. This is normal for providers like OpenRouter that don't validate API keys in their health check endpoints
2. Test the provider using the Chat interface to verify it's working correctly
3. Check provider documentation for endpoint-specific behaviors

---

## Integration with Router

Provider integrations work seamlessly with the router:

1. Configure multiple provider instances
2. Create model mappings for each provider
3. Set up model deployments in the router
4. Configure fallback paths

### Factory Pattern

The `DatabaseAwareLLMClientFactory` resolves providers by ID:

```csharp
public ILLMClient GetClient(string modelName)
{
    // 1. Look up model mapping by alias
    var mapping = await _mappingService.GetMappingByModelAliasAsync(modelName);

    // 2. Get provider by ID (not by type!)
    var provider = await _providerService.GetProviderByIdAsync(mapping.ProviderId);

    // 3. Get key credentials
    var keys = await _providerService.GetKeyCredentialsByProviderIdAsync(provider.Id);

    // 4. Select primary or first enabled key
    var key = keys.FirstOrDefault(k => k.IsPrimary && k.IsEnabled)
              ?? keys.FirstOrDefault(k => k.IsEnabled);

    // 5. Create appropriate client based on provider.ProviderType
    return CreateClientForProviderType(provider.ProviderType, provider, key);
}
```

### Example Multi-Provider Routing

```json
{
  "providers": [
    {
      "id": 1,
      "providerType": "OpenAI",
      "providerName": "Production OpenAI",
      "baseUrl": "https://api.openai.com/v1"
    },
    {
      "id": 2,
      "providerType": "OpenAI",
      "providerName": "Development OpenAI",
      "baseUrl": "https://api.openai.com/v1"
    },
    {
      "id": 3,
      "providerType": "AzureOpenAI",
      "providerName": "Azure OpenAI East",
      "baseUrl": "https://east.openai.azure.com"
    }
  ],
  "modelMappings": [
    {
      "modelAlias": "gpt-4",
      "providerId": 1,
      "providerModelId": "gpt-4"
    },
    {
      "modelAlias": "gpt-4",
      "providerId": 3,
      "providerModelId": "gpt-4-deployment"
    }
  ]
}
```

---

## Best Practices

1. **Use Provider ID for References**: Always use the Provider.Id when creating relationships, not ProviderType
2. **Name Providers Clearly**: Use descriptive names like "Production OpenAI" vs "OpenAI"
3. **Group Keys Properly**: Use ProviderAccountGroup to represent external account boundaries
4. **Set Primary Keys**: Always designate one key as primary for predictable behavior
5. **Enable/Disable vs Delete**: Use the IsEnabled flag rather than deleting providers or keys
6. **Use Generic Models**: Work with generic model names to maintain flexibility
7. **Configure Multiple Providers**: Set up alternatives for critical capabilities
8. **Monitor Usage**: Watch for costs and rate limits across providers
9. **Review Mappings**: Periodically review model mappings for new options
10. **Test Regularly**: Verify all providers are functioning correctly

---

## Adding New Providers

To add a new provider to the system:

### 1. Add the provider to the `ProviderType` enum

```csharp
public enum ProviderType
{
    // ... existing providers ...
    NewProvider = 22
}
```

### 2. Create a metadata class

```csharp
public class NewProviderMetadata : BaseProviderMetadata
{
    public override ProviderType ProviderType => ProviderType.NewProvider;
    public override string DisplayName => "New Provider";
    public override string DefaultBaseUrl => "https://api.newprovider.com/v1";

    public NewProviderMetadata()
    {
        // Configure capabilities
        Capabilities.Features.Streaming = true;
        // ... other capabilities ...

        // Configure authentication
        AuthRequirements.RequiresApiKey = true;
        // ... other auth requirements ...
    }
}
```

### 3. Build the solution

The registry will automatically discover the new provider.

### 4. Implement the provider client

Create a client class that implements `ILLMClient` for the provider-specific API.

### 5. Update the factory

Add the provider to the factory's client creation logic.

---

## Troubleshooting

### Common Issues

#### Rate Limiting

Each provider has different rate limits. If you encounter rate limiting:

1. Implement retry logic with exponential backoff
2. Distribute requests across multiple providers
3. Contact the provider to request limit increases

#### Authentication Errors

If you experience authentication errors:

1. Verify the API key is correct and active
2. Check that the endpoint URL is correct
3. Ensure the provider account is in good standing
4. Check for required headers or authentication methods

#### Model Availability

If a model is unavailable:

1. Verify the model exists with the provider
2. Check that your account has access to the model
3. Configure appropriate fallbacks in the router

---

## Related Documentation

- [Model and Cost Mapping](./model-and-cost-mapping.md) - Model mapping and cost tracking
- [Error Tracking](./error-tracking.md) - Provider error registry and automatic key management
- [Repository Pattern](../patterns/repository-and-data-access.md) - Data access patterns
- [DTO Guidelines](../data-transfer/dto-guidelines.md) - Data transfer object patterns
