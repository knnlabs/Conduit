/**
 * Model management types for the Admin API
 */

import type { components } from '../generated/admin-api';

// Re-export ModelType enum from modelType.ts
export { ModelType } from './modelType';

// Model DTOs
export type ModelDto = components['schemas']['ConduitLLM.Admin.Controllers.ModelDto'];
export type CreateModelDto = components['schemas']['ConduitLLM.Admin.Controllers.CreateModelDto'];
export type UpdateModelDto = components['schemas']['ConduitLLM.Admin.Controllers.UpdateModelDto'];

// Model Series DTOs  
export type ModelSeriesDto = components['schemas']['ConduitLLM.Admin.Controllers.ModelSeriesDto'];
export type CreateModelSeriesDto = components['schemas']['ConduitLLM.Admin.Controllers.CreateModelSeriesDto'];
export type UpdateModelSeriesDto = components['schemas']['ConduitLLM.Admin.Controllers.UpdateModelSeriesDto'];
export type SimpleModelSeriesDto = components['schemas']['ConduitLLM.Admin.Controllers.SimpleModelSeriesDto'];
export type SeriesSimpleModelDto = components['schemas']['ConduitLLM.Admin.Controllers.SeriesSimpleModelDto'];

// Model Author DTOs
export type ModelAuthorDto = components['schemas']['ConduitLLM.Admin.Controllers.ModelAuthorDto'];
export type CreateModelAuthorDto = components['schemas']['ConduitLLM.Admin.Controllers.CreateModelAuthorDto'];
export type UpdateModelAuthorDto = components['schemas']['ConduitLLM.Admin.Controllers.UpdateModelAuthorDto'];

// Model Capabilities DTOs
export type ModelCapabilitiesDto = components['schemas']['ConduitLLM.Admin.Controllers.ModelCapabilitiesDto'];
export type CreateCapabilitiesDto = components['schemas']['ConduitLLM.Admin.Controllers.CreateCapabilitiesDto'];
export type UpdateCapabilitiesDto = components['schemas']['ConduitLLM.Admin.Controllers.UpdateCapabilitiesDto'];
export type CapabilitiesSimpleModelDto = components['schemas']['ConduitLLM.Admin.Controllers.CapabilitiesSimpleModelDto'];

// Simplified type aliases for convenience
export type Model = ModelDto;
export type ModelSeries = ModelSeriesDto;
export type ModelAuthor = ModelAuthorDto;
export type ModelCapabilities = ModelCapabilitiesDto;