# Node.js SDK Changelog

## [2025-01-20]

### @knn_labs/conduit-common v0.2.0
- Added new fields to `Usage` interface for Phase 1 and Phase 2 pricing features:
  - `is_batch?: boolean` - Indicates batch processing
  - `image_quality?: string` - Image quality tier
  - `cached_input_tokens?: number` - Cached input token count
  - `cached_write_tokens?: number` - Cache write token count
  - `search_units?: number` - Search unit count for reranking
  - `inference_steps?: number` - Inference steps for image generation
  - `image_count?: number` - Number of images generated
  - `video_duration_seconds?: number` - Video duration
  - `video_resolution?: string` - Video resolution
  - `audio_duration_seconds?: number` - Audio duration

### @knn_labs/conduit-admin-client v1.1.0
- Added new fields to `ModelCost` interface:
  - `batchProcessingMultiplier?: number` - Batch processing discount
  - `supportsBatchProcessing: boolean` - Batch support flag
  - `imageQualityMultipliers?: string` - JSON quality multipliers
  - `cachedInputTokenCost?: number` - Cached input token cost
  - `cachedInputWriteCost?: number` - Cache write cost
  - `costPerSearchUnit?: number` - Search unit cost
  - `costPerInferenceStep?: number` - Inference step cost
  - `defaultInferenceSteps?: number` - Default steps for model
- Updated `ModelCostDto`, `CreateModelCostDto`, and `UpdateModelCostDto` with same fields

### @knn_labs/conduit-core-client v0.3.0
- Updated to use new `Usage` interface from common package with all Phase 1 and Phase 2 pricing fields

## Purpose
These updates add support for the new pricing models implemented in Conduit:
- Phase 1: Batch processing discounts, image quality multipliers
- Phase 2: Prompt caching costs, search unit pricing (Cohere), inference step pricing (Fireworks)

All changes are backward compatible as new fields are optional.