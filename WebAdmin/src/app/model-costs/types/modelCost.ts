// Re-export types from SDK
export type {
  ModelCostDto as ModelCost,
  CreateModelCostDto,
  UpdateModelCostDto,
  ModelCostFilters,
} from '@/lib/admin-api';

import type { ModelCostDto, ModelProviderMappingDto } from '@/lib/admin-api';

// Extended type to include additional fields from API response
export interface ExtendedModelProviderMappingDto extends ModelProviderMappingDto {
  providerName?: string;
  providerTypeName?: string;
}

// Legacy interface mapping for backward compatibility
// The new SDK uses different field names
export interface LegacyModelCost {
  id: number;
  modelIdPattern: string;
  providerType?: number;
  providerName?: string;
  // ... other legacy fields
}

// Helper type for list responses
export interface ModelCostListResponse {
  items: ModelCostDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}