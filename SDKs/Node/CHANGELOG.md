# Node.js SDK Changelog

## [2025-01-20]

### @knn_labs/conduit-common v0.2.0
- Added comprehensive usage tracking fields to `Usage` interface:
  - `is_batch?: boolean` - Indicates batch processing for discounted rates
  - `image_quality?: string` - Image quality tier for differentiated pricing
  - `cached_input_tokens?: number` - Tokens read from prompt cache (Anthropic, Gemini)
  - `cached_write_tokens?: number` - Tokens written to prompt cache
  - `search_units?: number` - Search units for reranking operations (Cohere)
  - `inference_steps?: number` - Inference steps for image generation (Fireworks)
  - `image_count?: number` - Number of images generated
  - `video_duration_seconds?: number` - Video duration for media pricing
  - `video_resolution?: string` - Video resolution tier
  - `audio_duration_seconds?: number` - Audio duration for TTS/STT pricing

### @knn_labs/conduit-admin-client v1.1.0
- Enhanced `ModelCost` interface with advanced pricing capabilities:
  - `batchProcessingMultiplier?: number` - Multiplier for batch processing discounts
  - `supportsBatchProcessing: boolean` - Indicates if model supports batch operations
  - `imageQualityMultipliers?: string` - JSON object mapping quality tiers to price multipliers
  - `cachedInputTokenCost?: number` - Cost per million cached input tokens
  - `cachedInputWriteCost?: number` - Cost per million tokens written to cache
  - `costPerSearchUnit?: number` - Cost per search unit for reranking models
  - `costPerInferenceStep?: number` - Cost per inference step for image generation
  - `defaultInferenceSteps?: number` - Default number of steps for estimation
- Updated `ModelCostDto`, `CreateModelCostDto`, and `UpdateModelCostDto` with same fields

### @knn_labs/conduit-core-client v0.3.0
- Updated to use enhanced `Usage` interface from common package
- Full support for advanced pricing models including prompt caching, search units, and inference steps

## Summary
These updates enable Conduit to support sophisticated pricing models beyond simple per-token billing:
- **Batch Processing**: Discounted rates for asynchronous batch operations (OpenAI, Anthropic)
- **Prompt Caching**: Reduced costs for repeated content (Anthropic Claude, Google Gemini)
- **Alternative Units**: Search units for reranking (Cohere), inference steps for images (Fireworks)
- **Quality Tiers**: Different pricing for image quality levels (DALL-E, Stable Diffusion)

All changes are backward compatible as new fields are optional.