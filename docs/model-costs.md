# Model Cost Configuration

This document provides information about the model cost configuration in Conduit, including default costs for popular frontier models from Anthropic and OpenAI.

## Overview

Model costs in Conduit are used to:
- Calculate usage costs for virtual keys
- Enforce budget limits
- Generate cost reports and dashboards
- Track spending across different models and providers

## Architecture

Conduit uses a modern cost mapping architecture:

1. **ModelCost entities** store cost configurations with user-friendly names (e.g., "GPT-4 Standard Pricing")
2. **ModelProviderMapping entities** represent specific model implementations from providers
3. **ModelCostMapping entities** link cost configurations to specific models (many-to-many relationship)

This allows one cost configuration to be applied to multiple models across different providers.

## Cost Storage Format

**IMPORTANT**: All costs in the database are stored as **cost per token** (not per 1K tokens). The values shown in this documentation are converted to per-1K-token format for readability, but the actual database values are 1000x smaller.

## Default Model Costs

Conduit includes default costs for popular frontier models from Anthropic and OpenAI. These costs are based on the official pricing from each provider as of August 2025.

### Anthropic Models

| Model | Input Cost (per 1K tokens) | Output Cost (per 1K tokens) | Database Value (Input) | Database Value (Output) |
|-------|----------------------------|------------------------------|------------------------|-------------------------|
| `anthropic/claude-3-opus-20240229` | $15.00 | $75.00 | 0.0150000000 | 0.0750000000 |
| `anthropic/claude-3-sonnet-20240229` | $3.00 | $15.00 | 0.0030000000 | 0.0150000000 |
| `anthropic/claude-3-haiku-20240307` | $0.25 | $1.25 | 0.0002500000 | 0.0012500000 |
| `anthropic/claude-2.1` | $8.00 | $24.00 | 0.0080000000 | 0.0240000000 |
| `anthropic/claude-2.0` | $8.00 | $24.00 | 0.0080000000 | 0.0240000000 |
| `anthropic/claude-instant-1.2` | $0.80 | $2.40 | 0.0008000000 | 0.0024000000 |

### OpenAI Models

| Model | Input Cost (per 1K tokens) | Output Cost (per 1K tokens) | Database Value (Input) | Database Value (Output) |
|-------|----------------------------|------------------------------|------------------------|-------------------------|
| `openai/gpt-4o` | $5.00 | $15.00 | 0.0050000000 | 0.0150000000 |
| `openai/gpt-4o-mini` | $0.50 | $1.50 | 0.0005000000 | 0.0015000000 |
| `openai/gpt-4-turbo` | $10.00 | $30.00 | 0.0100000000 | 0.0300000000 |
| `openai/gpt-4-1106-preview` | $10.00 | $30.00 | 0.0100000000 | 0.0300000000 |
| `openai/gpt-4-0125-preview` | $10.00 | $30.00 | 0.0100000000 | 0.0300000000 |
| `openai/gpt-4-vision-preview` | $10.00 | $30.00 | 0.0100000000 | 0.0300000000 |
| `openai/gpt-4-32k` | $60.00 | $120.00 | 0.0600000000 | 0.1200000000 |
| `openai/gpt-4` | $30.00 | $60.00 | 0.0300000000 | 0.0600000000 |
| `openai/gpt-3.5-turbo` | $0.50 | $1.50 | 0.0005000000 | 0.0015000000 |
| `openai/gpt-3.5-turbo-16k` | $1.00 | $2.00 | 0.0010000000 | 0.0020000000 |

### Embedding Models

| Model | Embedding Cost (per 1K tokens) | Database Value |
|-------|-------------------------------|----------------|
| `openai/text-embedding-3-small` | $0.02 | 0.0000200000 |
| `openai/text-embedding-3-large` | $0.13 | 0.0001300000 |
| `openai/text-embedding-ada-002` | $0.10 | 0.0001000000 |

### Image Generation Models

| Model | Cost per Image | Database Value |
|-------|----------------|----------------|
| `openai/dall-e-3` | $0.04 | 0.0400 |
| `openai/dall-e-2` | $0.02 | 0.0200 |

## Model Cost Mapping

The current implementation uses a mapping-based system rather than wildcard patterns:

1. **Create ModelCost entities** with cost configurations
2. **Link to ModelProviderMapping entities** via ModelCostMappings
3. **Cost lookup** happens through the mapping relationships

This provides more precise control and better performance than pattern matching.

## Managing Model Costs

You can manage model costs in the Conduit Admin UI at `/model-costs`:
- View all configured model costs
- Add new model costs
- Edit existing model costs
- Delete model costs

## Reloading Default Costs

**Note**: The shell script referenced in older documentation does not exist. Use the SQL script directly.

To reload default model costs for Anthropic and OpenAI:

```bash
# Execute the SQL script directly
docker compose exec postgres psql -U conduit -d conduitdb -f add-frontier-model-costs.sql
```

**Important**: The current SQL script uses the deprecated `ModelIdPattern` approach. For production use, you should:

1. Create ModelCost entities through the Admin API
2. Link them to ModelProviderMapping entities via ModelCostMappings
3. Use the Admin UI at `/model-costs` for management

## Adding Model Costs via Admin API

The recommended approach is to use the Admin API:

```bash
# Example: Create a new model cost configuration
curl -X POST "http://localhost:5000/api/model-costs" \
  -H "Content-Type: application/json" \
  -d '{
    "costName": "GPT-4 Standard Pricing",
    "modelProviderMappingIds": [1, 2, 3],
    "modelType": "chat",
    "inputTokenCost": 0.0100000000,
    "outputTokenCost": 0.0300000000,
    "description": "Standard GPT-4 pricing"
  }'
```

## Custom Cost Scripts

For custom model costs, create SQL scripts that work with the current architecture:

```sql
-- Example: Add a custom model cost
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputTokenCost", 
    "OutputTokenCost", 
    "ModelType",
    "CreatedAt", 
    "UpdatedAt"
) VALUES (
    'Custom Model Pricing',
    0.0020000000,  -- $2.00 per 1K tokens
    0.0040000000,  -- $4.00 per 1K tokens
    'chat',
    NOW(), 
    NOW()
);

-- Link to specific model provider mappings
INSERT INTO "ModelCostMappings" (
    "ModelCostId",
    "ModelProviderMappingId",
    "IsActive",
    "CreatedAt"
) VALUES (
    (SELECT "Id" FROM "ModelCosts" WHERE "CostName" = 'Custom Model Pricing'),
    1,  -- Replace with actual ModelProviderMapping ID
    true,
    NOW()
);
```

## Pricing Sources

The default costs are based on the official pricing pages from each provider:
- [Anthropic Pricing](https://www.anthropic.com/pricing)
- [OpenAI Pricing](https://openai.com/pricing)

Always check the official pricing pages for the most up-to-date information, as these values may change.

## Cost Calculation Behavior

### Cost Calculation Service

The cost calculation logic is implemented in `ConduitLLM.Core.Services.CostCalculationService` and supports:

- **Token-based costs**: Input, output, and embedding tokens
- **Image generation costs**: Per-image pricing with quality multipliers
- **Video generation costs**: Per-second pricing with resolution multipliers
- **Advanced features**: Cached tokens, search units, inference steps
- **Batch processing**: Discounted rates for batch operations

### Embedding Cost Logic

Embedding cost (`EmbeddingTokenCost`) is used when **ALL** of the following conditions are met:
1. The model has an `EmbeddingTokenCost` defined
2. `CompletionTokens` equals 0 (no text generation)
3. `ImageCount` is null (no image generation)

### Regular Token Cost Logic

Regular token costs (`InputTokenCost`/`OutputTokenCost`) are used in all other cases:
- Text generation requests (CompletionTokens > 0)
- Multimodal requests (ImageCount > 0)
- Any combination of the above

### Known Issue: Multimodal Embedding Models

**Problem**: When an embedding model also generates images, it uses expensive `InputTokenCost` instead of cheaper `EmbeddingTokenCost`.

**Example Scenario**:
- Model: Multimodal embedding model
- Request: 5000 tokens + 2 images
- Costs: `InputTokenCost` = $0.10/1K tokens, `EmbeddingTokenCost` = $0.01/1K tokens (10x cheaper)
- **Current behavior**: Uses $0.10/1K tokens (expensive)
- **Expected behavior**: Should use $0.01/1K tokens (cheap)
- **Impact**: 10x higher token costs than expected

**Workaround**: For multimodal embedding models, consider:
1. Setting `InputTokenCost` = `EmbeddingTokenCost` if the model is primarily for embeddings
2. Creating separate cost configurations for embedding-only vs multimodal use cases
3. Using the Admin API to adjust costs based on actual usage patterns

### Advanced Cost Features

#### Cached Token Pricing
- `CachedInputTokenCost`: Discounted rate for cached prompt tokens
- `CachedInputWriteCost`: Cost for writing new content to cache
- Used by providers like Anthropic Claude and Google Gemini

#### Search Unit Pricing
- `CostPerSearchUnit`: Cost per 1000 search units for reranking models
- Used by models like Cohere Rerank
- 1 search unit = 1 query + up to 100 documents

#### Inference Step Pricing
- `CostPerInferenceStep`: Cost per iterative refinement step
- `DefaultInferenceSteps`: Standard step count for the model
- Used by image generation models like FLUX and SDXL

This comprehensive cost calculation system is fully tested and documented in the test suite at `ConduitLLM.Tests/Core/Services/CostCalculationService*Tests.cs`.