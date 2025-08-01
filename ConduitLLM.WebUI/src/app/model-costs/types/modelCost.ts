// Re-export types from SDK
export type {
  ModelCostDto as ModelCost,
  CreateModelCostDto,
  UpdateModelCostDto,
  ModelCostFilters,
} from '@knn_labs/conduit-admin-client';

import type { ModelCostDto } from '@knn_labs/conduit-admin-client';

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