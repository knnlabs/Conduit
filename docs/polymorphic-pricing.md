# Polymorphic Pricing System Documentation

## Overview

The Conduit polymorphic pricing system supports multiple pricing models to accommodate different AI providers' billing structures. Instead of forcing all providers into a per-token model, the system can handle flat rates, per-second billing, tiered pricing, and more.

## Table of Contents

1. [Architecture](#architecture)
2. [Pricing Models](#pricing-models)
3. [Configuration Guide](#configuration-guide)
4. [API Reference](#api-reference)
5. [Migration Guide](#migration-guide)
6. [Examples](#examples)
7. [Troubleshooting](#troubleshooting)

## Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                     CostCalculationService                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              PolymorphicPricingCalculator            │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────────────┐    │   │
│  │  │ Standard │ │PerVideo │ │PerSecondVideo    │    │   │
│  │  └──────────┘ └──────────┘ └──────────────────┘    │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────────────┐    │   │
│  │  │  Tiered  │ │  Steps   │ │    PerImage      │    │   │
│  │  └──────────┘ └──────────┘ └──────────────────┘    │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              ▼
                    ┌──────────────────┐
                    │  Redis Cache      │
                    │ (Parsed Configs)  │
                    └──────────────────┘
```

### Database Schema

```sql
-- ModelCost table with polymorphic pricing
CREATE TABLE "ModelCosts" (
    "Id" SERIAL PRIMARY KEY,
    "CostName" VARCHAR(255) NOT NULL,
    "PricingModel" INTEGER NOT NULL DEFAULT 0,  -- Enum: Standard=0, PerVideo=1, etc.
    "PricingConfiguration" TEXT,                 -- JSON configuration
    "ModelType" VARCHAR(50) NOT NULL,
    -- Standard pricing fields (used when PricingModel=Standard)
    "InputCostPerMillionTokens" DECIMAL(18,6),
    "OutputCostPerMillionTokens" DECIMAL(18,6),
    -- Other cost fields...
);
```

## Pricing Models

### 1. Standard (Per Token)
Traditional token-based pricing used by most LLM providers.

**Enum Value**: `PricingModel.Standard` (0)

**Configuration**: Not required (uses standard database fields)

**Calculation**:
```csharp
cost = (inputTokens / 1,000,000) * inputCostPerMillionTokens +
       (outputTokens / 1,000,000) * outputCostPerMillionTokens
```

**Use Cases**: OpenAI GPT models, Anthropic Claude, Google Gemini

---

### 2. PerVideo (Flat Rate)
Fixed prices for specific resolution and duration combinations.

**Enum Value**: `PricingModel.PerVideo` (1)

**Configuration Example**:
```json
{
  "rates": {
    "512p_6": 0.10,
    "768p_6": 0.28,
    "1080p_6": 0.49
  }
}
```

**Calculation**:
- Looks up exact match for `{resolution}_{duration}`
- No interpolation between values
- Fails if exact match not found

**Use Cases**: MiniMax video generation

---

### 3. PerSecondVideo
Per-second billing with resolution multipliers.

**Enum Value**: `PricingModel.PerSecondVideo` (2)

**Configuration Example**:
```json
{
  "baseRate": 0.09,
  "resolutionMultipliers": {
    "480p": 0.5,
    "720p": 1.0,
    "1080p": 1.5,
    "4k": 2.5
  }
}
```

**Calculation**:
```csharp
cost = duration * baseRate * resolutionMultiplier
```

**Use Cases**: Replicate video models

---

### 4. InferenceSteps
Cost per denoising/inference step for image generation.

**Enum Value**: `PricingModel.InferenceSteps` (3)

**Configuration Example**:
```json
{
  "costPerStep": 0.00035,
  "defaultSteps": 20,
  "modelSteps": {
    "model-fast": 10,
    "model-quality": 30
  }
}
```

**Calculation**:
```csharp
steps = modelSteps[modelId] ?? defaultSteps ?? usage.Steps
cost = steps * costPerStep
```

**Use Cases**: Fireworks AI image generation

---

### 5. TieredTokens
Different token rates based on context length.

**Enum Value**: `PricingModel.TieredTokens` (4)

**Configuration Example**:
```json
{
  "tiers": [
    {
      "maxContext": 200000,
      "inputCost": 400,
      "outputCost": 2200
    },
    {
      "maxContext": null,
      "inputCost": 1300,
      "outputCost": 2200
    }
  ]
}
```

**Calculation**:
- Find tier where `totalTokens <= maxContext` (or maxContext is null)
- Apply that tier's rates to all tokens

**Use Cases**: MiniMax abab-6.5 models with context-based pricing

---

### 6. PerImage
Simple per-image pricing with quality/resolution multipliers.

**Enum Value**: `PricingModel.PerImage` (5)

**Configuration Example**:
```json
{
  "baseRate": 0.04,
  "qualityMultipliers": {
    "standard": 1.0,
    "hd": 1.5
  },
  "resolutionMultipliers": {
    "1024x1024": 1.0,
    "1792x1024": 1.5
  }
}
```

**Calculation**:
```csharp
cost = baseRate * qualityMultiplier * resolutionMultiplier
```

**Use Cases**: DALL-E, Midjourney-style pricing

---

### 7. PerMinuteAudio
Audio processing charged per minute.

**Enum Value**: `PricingModel.PerMinuteAudio` (6)

**Configuration Example**:
```json
{
  "ratePerMinute": 0.15
}
```

**Calculation**:
```csharp
cost = (duration / 60) * ratePerMinute
```

**Use Cases**: Speech synthesis, audio transcription

---

### 8. PerThousandCharacters
Text-to-speech pricing based on character count.

**Enum Value**: `PricingModel.PerThousandCharacters` (7)

**Configuration Example**:
```json
{
  "ratePerThousand": 0.015
}
```

**Calculation**:
```csharp
cost = (characterCount / 1000) * ratePerThousand
```

**Use Cases**: TTS services that charge by character count

## Configuration Guide

### Setting Up a New Pricing Model

1. **Choose the appropriate PricingModel enum value**
2. **Create the JSON configuration** (if not using Standard model)
3. **Set up the ModelCost record**

Example using the Admin API:

```csharp
var modelCost = new CreateModelCostDto
{
    CostName = "MiniMax Video Generation",
    PricingModel = PricingModel.PerVideo,
    PricingConfiguration = @"{
        ""rates"": {
            ""512p_6"": 0.10,
            ""768p_6"": 0.28,
            ""1080p_6"": 0.49
        }
    }",
    ModelType = "video",
    ModelProviderMappingIds = new[] { mappingId }
};
```

### Configuration Validation

All configurations are validated when saved:
- JSON must be valid
- Required fields must be present
- Numeric values must be positive
- Arrays must not be empty

### Batch Processing Discounts

All pricing models support batch processing discounts:

```csharp
if (modelCost.SupportsBatchProcessing && usage.BatchProcessing)
{
    finalCost *= modelCost.BatchProcessingMultiplier; // e.g., 0.5 for 50% off
}
```

## API Reference

### DTOs

#### CreateModelCostDto
```typescript
interface CreateModelCostDto {
  costName: string;                    // Required: Descriptive name
  modelProviderMappingIds: number[];   // Required: Model mappings
  pricingModel?: PricingModel;         // Default: Standard
  pricingConfiguration?: string;       // JSON config (required for non-Standard)
  modelType?: string;                  // Default: "chat"
  
  // Standard pricing fields (used when pricingModel=Standard)
  inputCostPerMillionTokens?: number;
  outputCostPerMillionTokens?: number;
  
  // Batch processing
  supportsBatchProcessing?: boolean;
  batchProcessingMultiplier?: number;
}
```

#### ModelCostDto
```typescript
interface ModelCostDto {
  id: number;
  costName: string;
  associatedModelAliases: string[];
  pricingModel: PricingModel;
  pricingConfiguration?: string;
  modelType: string;
  
  // Standard pricing fields
  inputCostPerMillionTokens: number;
  outputCostPerMillionTokens: number;
  
  // Metadata
  isActive: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
}
```

### Endpoints

#### Create Model Cost
```http
POST /api/admin/model-costs
Content-Type: application/json

{
  "costName": "GPT-4 Turbo Standard",
  "modelProviderMappingIds": [1, 2],
  "pricingModel": 0,
  "inputCostPerMillionTokens": 10.0,
  "outputCostPerMillionTokens": 30.0
}
```

#### Update Model Cost
```http
PUT /api/admin/model-costs/{id}
Content-Type: application/json

{
  "pricingModel": 1,
  "pricingConfiguration": "{\"rates\": {\"720p_5\": 0.25}}"
}
```

#### Calculate Cost
```http
POST /api/costs/calculate
Content-Type: application/json

{
  "modelId": "gpt-4-turbo",
  "usage": {
    "promptTokens": 1000,
    "completionTokens": 500
  }
}
```

## Migration Guide

### Migrating from Legacy Pattern-Based System

The old system used pattern matching on model IDs. The new system uses explicit model mappings.

#### Step 1: Identify Existing Patterns
```sql
SELECT DISTINCT "ModelIdPattern", "ProviderType" 
FROM "ModelCosts" 
WHERE "PricingModel" IS NULL;
```

#### Step 2: Create Model Mappings
For each pattern, create explicit ModelProviderMapping records:

```csharp
// Old: pattern = "gpt-4*"
// New: Create mappings for gpt-4, gpt-4-turbo, gpt-4-vision, etc.
```

#### Step 3: Migrate Model Costs
```csharp
foreach (var oldCost in oldCosts)
{
    var newCost = new ModelCost
    {
        CostName = $"{oldCost.ModelIdPattern} - Migrated",
        PricingModel = PricingModel.Standard,
        InputCostPerMillionTokens = oldCost.InputCostPerMillionTokens * 1000,
        OutputCostPerMillionTokens = oldCost.OutputCostPerMillionTokens * 1000,
        // Map to appropriate model mappings
    };
}
```

### Data Migration Script

```sql
-- Add new columns if not exists
ALTER TABLE "ModelCosts" 
ADD COLUMN IF NOT EXISTS "PricingModel" INTEGER DEFAULT 0,
ADD COLUMN IF NOT EXISTS "PricingConfiguration" TEXT,
ADD COLUMN IF NOT EXISTS "CostName" VARCHAR(255);

-- Migrate existing costs to Standard pricing model
UPDATE "ModelCosts" 
SET "PricingModel" = 0,
    "CostName" = CONCAT("ModelIdPattern", ' - Legacy')
WHERE "PricingModel" IS NULL;

-- Convert costs from per-token to per-million-tokens
UPDATE "ModelCosts"
SET "InputCostPerMillionTokens" = "InputCostPerMillionTokens" * 1000000,
    "OutputCostPerMillionTokens" = "OutputCostPerMillionTokens" * 1000000
WHERE "InputCostPerMillionTokens" < 1;
```

## Examples

### Example 1: OpenAI GPT-4 Turbo (Standard Pricing)

```json
{
  "costName": "GPT-4 Turbo",
  "pricingModel": 0,
  "modelType": "chat",
  "inputCostPerMillionTokens": 10.0,
  "outputCostPerMillionTokens": 30.0,
  "supportsBatchProcessing": true,
  "batchProcessingMultiplier": 0.5
}
```

### Example 2: MiniMax Video (Flat Rate)

```json
{
  "costName": "MiniMax Video Generation",
  "pricingModel": 1,
  "modelType": "video",
  "pricingConfiguration": {
    "rates": {
      "512p_6": 0.10,
      "768p_6": 0.28,
      "1080p_6": 0.49,
      "1080p_10": 0.76
    }
  }
}
```

### Example 3: Replicate SDXL (Per Second)

```json
{
  "costName": "Replicate SDXL Lightning",
  "pricingModel": 2,
  "modelType": "video",
  "pricingConfiguration": {
    "baseRate": 0.09,
    "resolutionMultipliers": {
      "480p": 0.5,
      "720p": 1.0,
      "1080p": 1.5
    }
  }
}
```

### Example 4: Fireworks Image Generation (Steps)

```json
{
  "costName": "Fireworks Stable Diffusion",
  "pricingModel": 3,
  "modelType": "image",
  "pricingConfiguration": {
    "costPerStep": 0.00035,
    "defaultSteps": 20,
    "modelSteps": {
      "stable-diffusion-xl-1024-v1-0": 25,
      "stable-diffusion-xl-lightning": 4
    }
  }
}
```

### Example 5: MiniMax with Tiered Pricing

```json
{
  "costName": "MiniMax abab-6.5 Tiered",
  "pricingModel": 4,
  "modelType": "chat",
  "pricingConfiguration": {
    "tiers": [
      {
        "maxContext": 200000,
        "inputCost": 400,
        "outputCost": 2200
      },
      {
        "maxContext": null,
        "inputCost": 1300,
        "outputCost": 2200
      }
    ]
  }
}
```

## Troubleshooting

### Common Issues

#### 1. "Invalid pricing configuration"
**Cause**: JSON configuration is malformed or missing required fields.

**Solution**: Validate JSON and ensure all required fields are present:
```bash
# Validate JSON
echo '{"rates": {...}}' | jq .
```

#### 2. "No matching rate found for video configuration"
**Cause**: PerVideo model requires exact match for resolution_duration.

**Solution**: Add the specific combination to the rates configuration:
```json
{
  "rates": {
    "720p_8": 0.35  // Add this exact combination
  }
}
```

#### 3. "Pricing model not supported for usage type"
**Cause**: Using wrong pricing model for the usage data.

**Solution**: Ensure usage data matches the pricing model:
- Token-based usage → Standard or TieredTokens
- Video usage → PerVideo or PerSecondVideo
- Image usage → PerImage or InferenceSteps

#### 4. Cost calculations returning 0
**Cause**: Configuration not properly parsed or cached.

**Solution**: 
1. Check Redis cache: `redis-cli GET "modelcost:parsed:{costId}"`
2. Clear cache: `redis-cli DEL "modelcost:parsed:{costId}"`
3. Verify configuration in database

### Debugging Cost Calculations

Enable debug logging:

```csharp
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "ConduitLLM.Core.Services.CostCalculationService": "Debug"
    }
  }
}
```

Check logs for:
- Selected pricing model
- Parsed configuration
- Calculation steps
- Final cost

### Performance Monitoring

Monitor these metrics:
- Cost calculation duration
- Cache hit rate for parsed configurations
- Failed calculations by pricing model

```sql
-- Find failed calculations
SELECT model_id, pricing_model, error_message, COUNT(*)
FROM cost_calculation_logs
WHERE status = 'failed'
GROUP BY model_id, pricing_model, error_message;
```

## Best Practices

1. **Always validate configurations** before saving
2. **Use appropriate precision** for costs (6 decimal places recommended)
3. **Cache parsed configurations** to avoid repeated JSON parsing
4. **Test with representative data** before deploying new pricing models
5. **Monitor calculation performance** and optimize hot paths
6. **Document custom configurations** for team members
7. **Version control pricing changes** for audit trail
8. **Set up alerts** for calculation failures

## Future Enhancements

Planned pricing models:
- **VolumeDiscounts**: Tiered discounts based on monthly usage
- **TimeBasedRates**: Peak/off-peak pricing
- **BundledPricing**: Package deals across multiple models
- **PerRequest**: Simple flat rate per API call

## Support

For issues or questions:
- GitHub Issues: https://github.com/knnlabs/Conduit/issues
- Documentation: This file
- API Reference: `/api/swagger`