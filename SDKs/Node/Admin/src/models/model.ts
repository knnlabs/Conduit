/**
 * Model management types for the Admin API
 */

import type { components } from '../generated/admin-api';

// Re-export ModelType enum from modelType.ts
export { ModelType } from './modelType';

// Model DTOs
export type ModelDto = components['schemas']['ConduitLLM.Admin.Models.Models.ModelDto'];
export type CreateModelDto = components['schemas']['ConduitLLM.Admin.Models.Models.CreateModelDto'];
export type UpdateModelDto = components['schemas']['ConduitLLM.Admin.Models.Models.UpdateModelDto'];

// Model Series DTOs  
export type ModelSeriesDto = components['schemas']['ConduitLLM.Admin.Models.ModelSeries.ModelSeriesDto'];
export type CreateModelSeriesDto = components['schemas']['ConduitLLM.Admin.Models.ModelSeries.CreateModelSeriesDto'];
export type UpdateModelSeriesDto = components['schemas']['ConduitLLM.Admin.Models.ModelSeries.UpdateModelSeriesDto'];
export type SimpleModelSeriesDto = components['schemas']['ConduitLLM.Admin.Models.ModelAuthors.SimpleModelSeriesDto'];
export type SeriesSimpleModelDto = components['schemas']['ConduitLLM.Admin.Models.ModelSeries.SeriesSimpleModelDto'];

// Model Author DTOs
export type ModelAuthorDto = components['schemas']['ConduitLLM.Admin.Models.ModelAuthors.ModelAuthorDto'];
export type CreateModelAuthorDto = components['schemas']['ConduitLLM.Admin.Models.ModelAuthors.CreateModelAuthorDto'];
export type UpdateModelAuthorDto = components['schemas']['ConduitLLM.Admin.Models.ModelAuthors.UpdateModelAuthorDto'];

// Simplified type aliases for convenience
export type Model = ModelDto;
export type ModelSeries = ModelSeriesDto;
export type ModelAuthor = ModelAuthorDto;