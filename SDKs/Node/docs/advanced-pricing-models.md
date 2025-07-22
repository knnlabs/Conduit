# Advanced Pricing Models in Conduit Node.js SDKs

This document describes the advanced pricing models supported by the Conduit Node.js SDKs.

## Overview

Conduit supports sophisticated pricing models beyond simple per-token billing:
- **Prompt Caching**: Reduced costs for cached input tokens (Anthropic Claude, Google Gemini)
- **Search Units**: Pricing for reranking operations (Cohere)
- **Inference Steps**: Per-step pricing for image generation (Fireworks)
- **Batch Processing**: Discounted rates for asynchronous batch operations
- **Quality Tiers**: Different pricing for image quality levels

## Usage Tracking

The `Usage` interface in `@knn_labs/conduit-common` provides comprehensive usage metrics:

```typescript
interface Usage {
  // Standard token usage
  prompt_tokens: number;
  completion_tokens: number;
  total_tokens: number;
  
  // Batch processing
  is_batch?: boolean;
  
  // Image generation
  image_quality?: string;
  image_count?: number;
  inference_steps?: number;        // Fireworks step-based pricing
  
  // Prompt caching
  cached_input_tokens?: number;    // Tokens read from cache
  cached_write_tokens?: number;    // Tokens written to cache
  
  // Search and reranking
  search_units?: number;           // Cohere reranking units
  
  // Media generation
  video_duration_seconds?: number;
  video_resolution?: string;
  audio_duration_seconds?: number;
}
```

## Model Cost Configuration

The Admin SDK's `ModelCost` interfaces support flexible pricing models:

```typescript
interface ModelCost {
  // Standard pricing
  inputCostPerMillionTokens?: number;
  outputCostPerMillionTokens?: number;
  
  // Batch processing
  batchProcessingMultiplier?: number;
  supportsBatchProcessing: boolean;
  
  // Image quality tiers
  imageQualityMultipliers?: string;  // JSON object with quality multipliers
  
  // Prompt caching pricing
  cachedInputTokenCost?: number;     // Cost per million cached input tokens
  cachedInputWriteCost?: number;     // Cost per million cache write tokens
  
  // Alternative units
  costPerSearchUnit?: number;        // Cohere reranking
  costPerInferenceStep?: number;     // Fireworks image generation
  defaultInferenceSteps?: number;    // Default steps for estimation
  
  // Fixed costs
  costPerRequest?: number;           // Fixed per-request cost
  costPerSecond?: number;            // Time-based billing
  costPerImage?: number;             // Per-image pricing
}
```

## Provider-Specific Pricing Models

### Anthropic Claude - Prompt Caching

Claude models support prompt caching for reduced costs on repeated content:
```typescript
const response = await client.chat.completions.create({
  model: 'claude-3-opus-20240229',
  messages: [
    { 
      role: 'system', 
      content: 'You are a helpful assistant.',
      cache_control: { type: 'ephemeral' }  // Enable caching
    },
    { role: 'user', content: 'Hello!' }
  ]
});

// Usage tracking
console.log(`Standard tokens: ${response.usage.prompt_tokens}`);
console.log(`Cached tokens: ${response.usage.cached_input_tokens}`);
console.log(`Cache writes: ${response.usage.cached_write_tokens}`);
```

**Pricing example**:
- Standard input: $15.00 per million tokens
- Cached input: $1.50 per million tokens (90% discount)
- Cache write: $18.75 per million tokens

### Cohere - Search Units

Cohere's reranking models use search units instead of tokens:
```typescript
// Reranking API response
const response = await client.rerank({
  model: 'rerank-english-v3.0',
  query: 'What is the capital of France?',
  documents: ['Paris is the capital...', 'London is...', ...]
});

// Usage tracking
console.log(`Search units used: ${response.usage.search_units}`);
// 1 search unit = 1 query + up to 100 documents
```

**Pricing**: $0.001 per search unit

### Fireworks - Inference Steps

Fireworks image models charge per inference step:
```typescript
const response = await client.images.generate({
  model: 'stable-diffusion-xl',
  prompt: 'A beautiful sunset',
  n: 3,
  steps: 75  // Number of inference steps
});

// Usage tracking
console.log(`Inference steps: ${response.usage.inference_steps}`);
console.log(`Images generated: ${response.usage.image_count}`);
```

**Pricing**: $0.0005 per inference step

### Google Gemini - Multi-Modal Input

Gemini models have different rates for different input types:
```typescript
// Text input
const textResponse = await client.chat.completions.create({
  model: 'gemini-1.5-pro',
  messages: [{ role: 'user', content: 'Hello!' }]
});

// Audio input (3.3x more expensive)
const audioResponse = await client.chat.completions.create({
  model: 'gemini-1.5-pro',
  messages: [{ 
    role: 'user', 
    content: [
      { type: 'audio', audio_url: '...' },
      { type: 'text', text: 'What is in this audio?' }
    ]
  }]
});
```

## Cost Calculation Examples

### Example 1: Claude with Caching
```typescript
// Model cost configuration
const modelCost = {
  inputCostPerMillionTokens: 15.00,
  outputCostPerMillionTokens: 75.00,
  cachedInputTokenCost: 1.50,
  cachedInputWriteCost: 18.75
};

// Usage from API
const usage = {
  prompt_tokens: 1000,
  cached_input_tokens: 800,  // 800 tokens from cache
  cached_write_tokens: 200,   // 200 new tokens cached
  completion_tokens: 500,
  total_tokens: 1500
};

// Cost calculation
const inputCost = (200 * 15.00) / 1_000_000;        // Non-cached input
const cachedCost = (800 * 1.50) / 1_000_000;        // Cached input
const cacheWriteCost = (200 * 18.75) / 1_000_000;   // Cache writes
const outputCost = (500 * 75.00) / 1_000_000;       // Output

const totalCost = inputCost + cachedCost + cacheWriteCost + outputCost;
```

### Example 2: Batch Processing Discount
```typescript
// Model cost with batch support
const modelCost = {
  inputCostPerMillionTokens: 3.00,
  outputCostPerMillionTokens: 15.00,
  batchProcessingMultiplier: 0.5,  // 50% discount
  supportsBatchProcessing: true
};

// Batch usage
const usage = {
  prompt_tokens: 10000,
  completion_tokens: 5000,
  total_tokens: 15000,
  is_batch: true  // Batch processing enabled
};

// Cost with batch discount
const inputCost = (10000 * 3.00 * 0.5) / 1_000_000;
const outputCost = (5000 * 15.00 * 0.5) / 1_000_000;
```

## Implementation Details

### Usage Response Structure

All Conduit API responses include a standardized `usage` object:

```typescript
interface ChatCompletionResponse {
  id: string;
  model: string;
  choices: Choice[];
  usage: Usage;  // Always present
  performance?: PerformanceMetrics;
}
```

### Admin API - Model Cost Management

```typescript
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

const admin = new ConduitAdminClient({ apiKey: 'admin-key' });

// Create model cost with advanced pricing
await admin.modelCosts.create({
  modelId: 'claude-3-opus-20240229',
  inputTokenCost: 15.00,
  outputTokenCost: 75.00,
  cachedInputTokenCost: 1.50,
  cachedInputWriteCost: 18.75,
  supportsBatchProcessing: false
});

// Update inference step pricing
await admin.modelCosts.update('stable-diffusion-xl', {
  costPerInferenceStep: 0.0005,
  defaultInferenceSteps: 50
});
```

## Backwards Compatibility

All advanced pricing fields are optional, ensuring full backwards compatibility:
- Existing integrations continue to work without modification
- New fields are only populated when relevant to the model/request
- Cost calculation falls back to standard token pricing when advanced fields are not configured

## See Also

- [Model Pricing Documentation](/docs/model-pricing/README.md)
- [Provider Integration Guide](/docs/Provider-Integration.md)
- [Admin API Reference](/docs/Admin-API.md)