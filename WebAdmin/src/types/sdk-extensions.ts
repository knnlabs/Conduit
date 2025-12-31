/**
 * SDK Type Extensions
 * This file extends the SDK types to include properties that are returned by the API
 * but not yet defined in the SDK type definitions.
 */

import type { 
  VirtualKeyDto as BaseVirtualKeyDto, 
  UsageMetricsDto as BaseUsageMetricsDto,
  DateRange
} from '@knn_labs/conduit-admin-client';

// Re-export DateRange for convenience
export type { DateRange };

/**
 * Extended VirtualKeyDto with additional properties from API responses
 */
export interface VirtualKeyDto extends BaseVirtualKeyDto {
  // Additional properties from API responses
  keyHash?: string;
  hash?: string;
  budgetLimit?: number; // Additional property from API
  requestsPerMinute?: number; // Rate limiting
}

/**
 * Extended UsageMetricsDto with token tracking
 */
export interface UsageMetricsDto extends BaseUsageMetricsDto {
  totalInputTokens?: number;
  totalOutputTokens?: number;
}

/**
 * Extended ModelUsageDto with input/output token tracking
 */

/**
 * Extended KeyUsageDto with additional cost tracking
 */

/**
 * Request log entry with additional fields
 */

/**
 * Cost by model response with proper structure
 */
export interface CostByModelResponse {
  models: {
    modelId: string;
    totalRequests: number;
    totalTokens: number;
    totalCost: number;
    averageTokensPerRequest: number;
    averageCostPerRequest: number;
    successRate: number;
    averageLatency: number;
    popularKeys: {
      keyId: number;
      keyName: string;
      requestCount: number;
    }[];
  }[];
}

/**
 * Cost by key response with proper structure
 */
export interface CostByKeyResponse {
  keys: {
    keyId: number;
    keyName: string;
    totalRequests: number;
    totalCost: number;
    budgetUsed: number;
    budgetRemaining: number;
    averageCostPerRequest: number;
    requestsPerDay: number;
    popularModels: {
      modelId: string;
      requestCount: number;
      totalCost: number;
    }[];
    lastUsed: string;
  }[];
}

/**
 * Provider health status mapping
 */

/**
 * Notification type mapping for filtering
 */
