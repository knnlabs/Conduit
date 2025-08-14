# Provider Multi-Instance Architecture

## Overview

ConduitLLM supports multiple instances of the same provider type, enabling advanced deployment scenarios such as:
- Multiple OpenAI accounts with different API keys
- Separate development and production configurations
- Geographic distribution (e.g., Azure OpenAI in different regions)
- Account-based rate limit management

## Core Entities

### Provider Entity

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

### ProviderKeyCredential Entity

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

## Key Concepts

### Provider ID vs ProviderType

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

### ProviderAccountGroup

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

### Primary Key Selection

Each provider can have one primary key:
- The primary key is used by default
- If no primary is set, the first enabled key is used
- Non-primary keys are used for failover

## Model Mapping

The `ModelProviderMapping` entity connects model aliases to specific provider instances:

```csharp
public class ModelProviderMapping
{
    public int Id { get; set; }
    public string ModelAlias { get; set; }        // Client-facing name
    public string ProviderModelId { get; set; }   // Provider's model name
    public int ProviderId { get; set; }           // FK to Provider (not ProviderType!)
    public Provider Provider { get; set; }
    
    // Capabilities
    public bool SupportsVision { get; set; }
    public bool SupportsChat { get; set; }
    // ... other capabilities
}
```

## Usage Examples

### Scenario 1: Multiple OpenAI Configurations

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

// Model mappings route to different providers
{
  "modelAlias": "gpt-4-prod",
  "providerId": 1,
  "providerModelId": "gpt-4"
}
{
  "modelAlias": "gpt-4-dev",
  "providerId": 2,
  "providerModelId": "gpt-4"
}
```

### Scenario 2: Geographic Distribution

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

## Factory Pattern

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

## Best Practices

1. **Use Provider ID for References**: Always use the Provider.Id when creating relationships, not ProviderType
2. **Name Providers Clearly**: Use descriptive names like "Production OpenAI" vs "OpenAI"
3. **Group Keys Properly**: Use ProviderAccountGroup to represent external account boundaries
4. **Set Primary Keys**: Always designate one key as primary for predictable behavior
5. **Enable/Disable vs Delete**: Use the IsEnabled flag rather than deleting providers or keys

## Migration Considerations

When migrating from single-provider-per-type to multi-instance:

1. Existing code using ProviderType as identifier needs updating
2. Model mappings must reference Provider.Id instead of provider type
3. API endpoints should accept provider IDs, not provider type strings
4. Cost tracking should be associated with specific provider instances