# Model Cost Mapping Architecture

## Overview

ConduitLLM uses a flexible cost tracking system that allows one cost configuration to be applied to multiple models across different providers. This enables efficient management of pricing information, especially for models that share the same cost structure (e.g., Llama models across different providers).

## Entity Relationships

```
ModelCost ←→ ModelCostMapping ←→ ModelProviderMapping → Provider
```

### ModelCost Entity

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

### ModelCostMapping Entity

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

### ModelProviderMapping Entity

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

## Best Practices

1. **Use Descriptive Cost Names**: Include model and date information
2. **Set Appropriate Priorities**: Higher priority for more specific costs
3. **Plan for Price Changes**: Use effective/expiry dates
4. **Group Similar Costs**: Reuse cost configurations where possible
5. **Document Special Cases**: Note any provider-specific pricing rules

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