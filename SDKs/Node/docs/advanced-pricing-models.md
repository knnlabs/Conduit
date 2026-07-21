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

The Admin API's model-cost DTOs support flexible pricing models:

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

### Provider-aware prompt caching

Conduit observes automatic prefix caching from providers such as OpenAI, Groq, and DeepSeek without changing requests. Managed directives are limited to documented OpenRouter Claude and Alibaba explicit-cache routes and are configured through the Admin API:
```typescript
await admin.configuration.updatePromptCachingConfig({
  schemaVersion: 2,
  enabled: true,
  rules: [{
    name: 'Claude conversations',
    enabled: true,
    provider: 'OpenRouter',
    modelPattern: 'anthropic/*',
    strategy: 'OpenRouterAutomatic',
    ttl: '5m',
    injectionPoints: []
  }]
});
```

Rules are evaluated in order and the first enabled match wins. Explicit rules support up to four message breakpoints. Keep stable tools, instructions, and reference material at the beginning of prompts; short or changing prefixes will not produce useful cache hits.

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

Generate a client from `Services/ConduitLLM.Admin/openapi-admin.json`, or call the documented model
cost operations directly with the canonical `X-Master-Key` header. The former Admin Node package is
retired and receives no future releases.

## Backwards Compatibility

All advanced pricing fields are optional, ensuring full backwards compatibility:
- Existing integrations continue to work without modification
- New fields are only populated when relevant to the model/request
- Cost calculation falls back to standard token pricing when advanced fields are not configured

## See Also

- [Model Pricing Documentation](/docs/model-pricing/README.md)
- [Provider Integration Guide](/docs/Provider-Integration.md)
- [Admin API Reference](/docs/Admin-API.md)
