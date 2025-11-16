# Model and Cost Mapping Architecture

This document describes the model mapping flow and cost tracking system used across all AI endpoints in ConduitLLM.

## Table of Contents

1. [Overview](#overview)
2. [Model Mapping Flow](#model-mapping-flow)
3. [Cost Mapping Architecture](#cost-mapping-architecture)
4. [Entity Relationships](#entity-relationships)
5. [Cost Configuration Scenarios](#cost-configuration-scenarios)
6. [Cost Resolution Logic](#cost-resolution-logic)
7. [Special Pricing Models](#special-pricing-models)
8. [API Usage](#api-usage)
9. [Best Practices](#best-practices)

---

## Overview

ConduitLLM uses two complementary systems:

1. **Model Mapping**: Creates an abstraction layer between generic model names and provider-specific implementations
2. **Cost Tracking**: Allows one cost configuration to be applied to multiple models across different providers

Together, these systems enable efficient management of models and pricing information, especially for models that share the same cost structure (e.g., Llama models across different providers).

---

## Model Mapping Flow

### Key Components

#### 1. Model Alias
- User-facing identifier (e.g., `gpt-4`, `whisper-large`, `video-gen-v2`)
- Consistent across all API calls
- Mapped to provider-specific model IDs

#### 2. Provider Model ID
- Provider's internal model identifier (e.g., `gpt-4-0613`, `whisper-1`)
- Used in actual API calls to providers
- Hidden from end users

#### 3. Model Provider Mapping
- Database entity linking aliases to provider model IDs
- Includes capability flags (chat, image, audio, video support)
- Associates models with specific provider instances

### Consistent Flow Pattern

All services follow this pattern:

```csharp
// 1. Receive request with model alias
var request = new SomeRequest { Model = "user-friendly-alias" };

// 2. Get model mapping
var mapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
if (mapping == null) {
    // Handle unmapped model
}

// 3. Store original alias for response
var originalAlias = request.Model;

// 4. Update request with provider model ID
request.Model = mapping.ProviderModelId;

// 5. Call provider with translated model ID
var response = await provider.CallApiAsync(request);

// 6. Restore original alias in response
response.Model = originalAlias;
```

### Implementation by Service

#### Audio (Transcription & TTS)
- **Location**: `AudioRouter.cs`
- **Methods**: `GetTranscriptionClientAsync()`, `GetTextToSpeechClientAsync()`
- **Pattern**: Updates request model before returning client

#### Video Generation
- **Location**: `VideoGenerationService.cs`
- **Method**: `GenerateVideoAsync()`
- **Pattern**: Updates request model after mapping lookup

#### Image Generation
- **Location**: `ImagesController.cs` + routing
- **Pattern**: Uses model mapping for capability checking

#### Chat Completion
- **Location**: Chat routing infrastructure
- **Pattern**: Standard model mapping flow

### Creating Model Mappings

Model mappings link a model alias to a specific provider instance:

#### Via Admin API

```
POST /api/admin/model-provider-mappings
```

```json
{
  "modelAlias": "gpt-4-turbo",
  "providerId": 1,
  "providerModelId": "gpt-4-turbo-preview",
  "isEnabled": true,
  "maxContextTokens": 128000,
  "supportsVision": true,
  "supportsChat": true,
  "supportsFunctionCalling": true,
  "supportsStreaming": true
}
```

Note: The `providerId` refers to the Provider entity's ID, not the ProviderType enum.

### Example Mappings

The following table shows example mappings across providers:

| Generic Model | OpenAI | Anthropic | Cohere | Gemini |
|---------------|--------|-----------|--------|--------|
| chat-large | gpt-4 | claude-2 | command-r-plus | gemini-pro |
| chat-medium | gpt-3.5-turbo | claude-instant | command | gemini-flash |
| embedding | text-embedding-3-large | N/A | embed-english-v3.0 | embedding-001 |

### Using Mapped Models

In API requests, use the generic model name instead of provider-specific names:

```json
{
  "model": "chat-large",
  "messages": [
    {"role": "system", "content": "You are a helpful assistant."},
    {"role": "user", "content": "Hello!"}
  ],
  "max_tokens": 1024
}
```

The system will automatically route this request to the appropriate provider-specific model.

### Benefits

1. **Abstraction**: Users don't need to know provider-specific model names
2. **Flexibility**: Easy to switch providers without changing client code
3. **Consistency**: Same pattern across all AI services
4. **Multi-Provider**: Support multiple instances of same provider type

---

## Cost Mapping Architecture

ConduitLLM uses a flexible cost tracking system that allows one cost configuration to be applied to multiple models across different providers.

### Entity Relationships

```
ModelCost ←→ ModelCostMapping ←→ ModelProviderMapping → Provider
```

#### ModelCost Entity

The `ModelCost` entity stores pricing configurations:

```csharp
public class ModelCost
{
    public int Id { get; set; }
    public string CostName { get; set; }              // e.g., "GPT-4 Standard Pricing"
    public decimal InputTokenCost { get; set; }       // Cost per input token
    public decimal OutputTokenCost { get; set; }      // Cost per output token
    public decimal? EmbeddingTokenCost { get; set; }  // For embedding models
    public decimal? ImageCostPerImage { get; set; }   // For image generation
    public string ModelType { get; set; }             // "chat", "embedding", "image"
    public bool IsActive { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Priority { get; set; }                 // For cost resolution order

    // Audio costs
    public decimal? AudioCostPerMinute { get; set; }        // Transcription
    public decimal? AudioCostPerKCharacters { get; set; }   // TTS
    public decimal? AudioInputCostPerMinute { get; set; }   // Realtime input
    public decimal? AudioOutputCostPerMinute { get; set; }  // Realtime output

    // Video costs
    public decimal? VideoCostPerSecond { get; set; }
    public string? VideoResolutionMultipliers { get; set; } // JSON: {"720p": 1.0, "1080p": 1.5}

    // Advanced pricing
    public decimal? BatchProcessingMultiplier { get; set; }  // Discount for batch API
    public decimal? CachedInputTokenCost { get; set; }      // Prompt caching
    public decimal? CostPerSearchUnit { get; set; }         // Reranking models
    public decimal? CostPerInferenceStep { get; set; }      // Step-based image pricing

    // Navigation property
    public ICollection<ModelCostMapping> ModelCostMappings { get; set; }
}
```

#### ModelCostMapping Entity

The junction table that links costs to models:

```csharp
public class ModelCostMapping
{
    public int Id { get; set; }
    public int ModelCostId { get; set; }
    public int ModelProviderMappingId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ModelCost ModelCost { get; set; }
    public ModelProviderMapping ModelProviderMapping { get; set; }
}
```

#### ModelProviderMapping Entity

Links model aliases to providers and includes the cost mappings:

```csharp
public class ModelProviderMapping
{
    public int Id { get; set; }
    public string ModelAlias { get; set; }
    public string ProviderModelId { get; set; }
    public int ProviderId { get; set; }

    // Navigation property to costs
    public ICollection<ModelCostMapping> ModelCostMappings { get; set; }
}
```

---

## Cost Configuration Scenarios

### Scenario 1: Shared Cost Configuration

One cost configuration applied to multiple Llama instances:

```json
// Single cost configuration
{
  "id": 1,
  "costName": "Llama 3 70B Standard",
  "inputTokenCost": 0.00065,
  "outputTokenCost": 0.00079,
  "modelType": "chat"
}

// Applied to multiple providers
{
  "modelCostMappings": [
    {
      "modelCostId": 1,
      "modelProviderMappingId": 10  // Llama on Groq
    },
    {
      "modelCostId": 1,
      "modelProviderMappingId": 11  // Llama on Fireworks
    },
    {
      "modelCostId": 1,
      "modelProviderMappingId": 12  // Llama on Replicate
    }
  ]
}
```

### Scenario 2: Provider-Specific Costs

Different costs for the same model on different providers:

```json
// OpenAI GPT-4 pricing
{
  "id": 2,
  "costName": "GPT-4 OpenAI Direct",
  "inputTokenCost": 0.03,
  "outputTokenCost": 0.06
}

// Azure OpenAI GPT-4 pricing (potentially different)
{
  "id": 3,
  "costName": "GPT-4 Azure",
  "inputTokenCost": 0.03,
  "outputTokenCost": 0.06,
  "batchProcessingMultiplier": 0.5  // Azure-specific batch discount
}
```

### Scenario 3: Time-Based Pricing

Handle price changes over time:

```json
// Current pricing
{
  "id": 4,
  "costName": "Claude 3 Current",
  "inputTokenCost": 0.015,
  "outputTokenCost": 0.075,
  "effectiveDate": "2024-01-01",
  "expiryDate": "2024-12-31"
}

// Future pricing
{
  "id": 5,
  "costName": "Claude 3 2025 Pricing",
  "inputTokenCost": 0.012,
  "outputTokenCost": 0.060,
  "effectiveDate": "2025-01-01",
  "priority": 10  // Higher priority when multiple costs match
}
```

---

## Cost Resolution Logic

When calculating costs for a request:

1. Find all active ModelCostMappings for the ModelProviderMapping
2. Filter by effective date range
3. Sort by priority (highest first)
4. Use the first matching cost configuration

```csharp
public decimal CalculateCost(int modelProviderMappingId, int inputTokens, int outputTokens)
{
    var costMapping = dbContext.ModelCostMappings
        .Include(m => m.ModelCost)
        .Where(m => m.ModelProviderMappingId == modelProviderMappingId)
        .Where(m => m.IsActive && m.ModelCost.IsActive)
        .Where(m => m.ModelCost.EffectiveDate <= DateTime.UtcNow)
        .Where(m => m.ModelCost.ExpiryDate == null || m.ModelCost.ExpiryDate > DateTime.UtcNow)
        .OrderByDescending(m => m.ModelCost.Priority)
        .FirstOrDefault();

    if (costMapping == null) return 0;

    var cost = costMapping.ModelCost;
    return (inputTokens * cost.InputTokenCost) + (outputTokens * cost.OutputTokenCost);
}
```

---

## Special Pricing Models

### Cached Input Tokens (Anthropic, Gemini)

```json
{
  "costName": "Claude 3.5 with Caching",
  "inputTokenCost": 0.003,           // Regular input
  "cachedInputTokenCost": 0.0003,    // 10% cost for cached tokens
  "cachedInputWriteCost": 0.00375,   // 125% cost to write to cache
  "outputTokenCost": 0.015
}
```

### Step-Based Image Pricing (Fireworks)

```json
{
  "costName": "FLUX.1 Schnell",
  "costPerInferenceStep": 0.00035,
  "defaultInferenceSteps": 4,        // 4 steps × $0.00035 = $0.0014 per image
  "modelType": "image"
}
```

### Search Unit Pricing (Cohere Rerank)

```json
{
  "costName": "Cohere Rerank v3",
  "costPerSearchUnit": 0.002,        // Per 1000 search units
  "modelType": "rerank"
}
```

---

## API Usage

### Creating a Cost Configuration

```bash
POST /api/admin/model-costs
{
  "costName": "GPT-4 Turbo 2024",
  "inputTokenCost": 0.01,
  "outputTokenCost": 0.03,
  "modelType": "chat",
  "isActive": true,
  "effectiveDate": "2024-01-01"
}
```

### Applying Cost to Models

```bash
POST /api/admin/model-cost-mappings
{
  "modelCostId": 1,
  "modelProviderMappingId": 5,
  "isActive": true
}
```

### Bulk Cost Assignment

```bash
POST /api/admin/model-costs/{costId}/apply-to-models
{
  "modelProviderMappingIds": [5, 6, 7, 8]
}
```

---

## Best Practices

### Model Mapping

1. **Use Descriptive Aliases**: Choose model aliases that clearly indicate their purpose
2. **Document Capabilities**: Ensure all capability flags are accurately set
3. **Test Mappings**: Verify model aliases work correctly with all endpoints
4. **Version Control**: Consider version suffixes for models that change over time

### Cost Configuration

1. **Use Descriptive Cost Names**: Include model and date information
2. **Set Appropriate Priorities**: Higher priority for more specific costs
3. **Plan for Price Changes**: Use effective/expiry dates
4. **Group Similar Costs**: Reuse cost configurations where possible
5. **Document Special Cases**: Note any provider-specific pricing rules

### Testing

Integration tests should verify:
- Model alias resolution works correctly
- Provider receives correct model ID
- Response contains original alias
- Null handling for unmapped models
- Cost calculations are accurate

See: `/ConduitLLM.Tests/Integration/ModelMappingIntegrationTests.cs`

---

## Configuration

Model mappings and costs are configured in the database:

- **Tables**:
  - `ModelProviderMappings` - Model alias to provider model ID mappings
  - `ModelCosts` - Cost configurations
  - `ModelCostMappings` - Links costs to model mappings
- **Key fields**:
  - Model mapping: `ModelAlias`, `ProviderModelId`, `ProviderId`
  - Cost: `CostName`, `InputTokenCost`, `OutputTokenCost`, `EffectiveDate`, `ExpiryDate`, `Priority`
- **Capability flags**: Enable features per model (chat, image, audio, video support)

---

## Future Considerations

- Model versioning support
- Automatic model discovery from providers
- Model deprecation handling
- Cost tier mapping
- Batch cost assignment UI
- Cost import/export functionality

---

## Related Documentation

- [Provider Architecture](./provider-architecture.md) - Provider system design and multi-instance support
- [Error Tracking](./error-tracking.md) - Provider error registry
- [Repository Pattern](../patterns/repository-and-data-access.md) - Data access patterns
- [DTO Guidelines](../data-transfer/dto-guidelines.md) - Data transfer object patterns
