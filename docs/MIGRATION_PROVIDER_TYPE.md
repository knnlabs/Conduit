# Migration Guide: String-based Provider Names to ProviderType Enum

## Overview
As part of Phase 3f (#628), we've migrated from string-based provider names to the strongly-typed `ProviderType` enum. This is a breaking change that improves type safety and reduces runtime errors.

## Removed APIs

### 1. ILLMClientFactory.GetClientByProvider(string providerName)
**Old:**
```csharp
var client = _clientFactory.GetClientByProvider("openai");
```

**New:**
```csharp
var client = _clientFactory.GetClientByProviderId(1); // OpenAI = 1
```

### 2. IProviderCredentialService.GetCredentialByProviderNameAsync(string providerName)
**Old:**
```csharp
var credentials = await _credentialService.GetCredentialByProviderNameAsync("anthropic");
```

**New:**
```csharp
var credentials = await _credentialService.GetCredentialByIdAsync(2); // Anthropic = 2
```

### 3. ProviderCredentials.ProviderName property
The `ProviderName` property has been removed from the `ProviderCredentials` class. Use the provider ID instead.

## ProviderType Enum Values
```csharp
public enum ProviderType
{
    OpenAI = 1,
    Anthropic = 2,
    AzureOpenAI = 3,
    Gemini = 4,
    VertexAI = 5,
    Cohere = 6,
    Mistral = 7,
    Groq = 8,
    Ollama = 9,
    Replicate = 10,
    Fireworks = 11,
    Bedrock = 12,
    HuggingFace = 13,
    SageMaker = 14,
    OpenRouter = 15,
    OpenAICompatible = 16,
    MiniMax = 17,
    Ultravox = 18,
    ElevenLabs = 19,
    GoogleCloud = 20,
    Cerebras = 21
}
```

## Migration Steps

1. **Update client factory calls:**
   - Replace `GetClientByProvider(providerName)` with `GetClientByProviderId(providerId)`
   - Use the enum value cast to int: `(int)ProviderType.OpenAI`

2. **Update credential service calls:**
   - Replace `GetCredentialByProviderNameAsync(providerName)` with `GetCredentialByIdAsync(providerId)`

3. **Remove ProviderName usage:**
   - If you were accessing `credentials.ProviderName`, use the provider ID instead
   - To get the provider name as a string, use: `((ProviderType)providerId).ToString()`

4. **Update string comparisons:**
   - Replace string comparisons like `provider == "openai"` with enum comparisons
   - Use: `providerType == ProviderType.OpenAI`

## Helper Method
If you need to convert provider names to IDs during migration:
```csharp
private int? GetProviderIdFromName(string providerName)
{
    return providerName?.ToLowerInvariant() switch
    {
        "openai" => 1,
        "anthropic" => 2,
        "azure" or "azureopenai" => 3,
        "gemini" => 4,
        "vertexai" => 5,
        "cohere" => 6,
        "mistral" => 7,
        "groq" => 8,
        "ollama" => 9,
        "replicate" => 10,
        "fireworks" => 11,
        "bedrock" => 12,
        "huggingface" => 13,
        "sagemaker" => 14,
        "openrouter" => 15,
        "openaicompatible" => 16,
        "minimax" => 17,
        "ultravox" => 18,
        "elevenlabs" => 19,
        "google" or "googlecloud" => 20,
        "cerebras" => 21,
        _ => null
    };
}
```

## Database Migration
This change requires a database migration. The provider type is now stored as an integer in the database instead of a string. Make sure to run the latest migrations before deploying.