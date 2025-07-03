/**
 * SDK Type Extensions
 * This file extends the SDK types to include properties that are returned by the API
 * but not yet defined in the SDK type definitions.
 */

import type { 
  VirtualKeyDto as BaseVirtualKeyDto, 
  UsageMetricsDto as BaseUsageMetricsDto,
  ModelUsageDto as BaseModelUsageDto,
  KeyUsageDto as BaseKeyUsageDto
} from '@knn_labs/conduit-admin-client';

/**
 * Extended VirtualKeyDto with additional properties from API responses
 */
export interface VirtualKeyDto extends BaseVirtualKeyDto {
  // Additional properties from API responses
  keyHash?: string;
  hash?: string;
  budgetLimit?: number; // Legacy property name
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
export interface ModelUsageDto extends BaseModelUsageDto {
  inputTokens?: number;
  outputTokens?: number;
  cost?: number; // Legacy property name for totalCost
}

/**
 * Extended KeyUsageDto with additional cost tracking
 */
export interface KeyUsageDto extends BaseKeyUsageDto {
  cost?: number; // Legacy property name for totalCost
  requestCount?: number; // Legacy property name for totalRequests
}

/**
 * Request log entry with additional fields
 */
export interface RequestLogEntry {
  id: number;
  virtualKeyId: number;
  model: string;
  endpoint?: string;
  clientIP?: string;
  statusCode: number;
  latency: number;
  inputTokens?: number;
  outputTokens?: number;
  totalTokens?: number;
  cost: number;
  error?: string;
  createdAt: string;
}

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
export const HealthStatusMap = {
  'healthy': 'healthy',
  'degraded': 'warning',
  'unhealthy': 'critical',
  'unknown': 'unknown'
} as const;

/**
 * Notification type mapping for filtering
 */
export const NotificationTypeMap = {
  'VirtualKeyCreated': 'virtualkey',
  'VirtualKeyUpdated': 'virtualkey',
  'VirtualKeyDeleted': 'virtualkey',
  'VirtualKeyBudgetExceeded': 'virtualkey',
  'ProviderHealthChange': 'provider',
  'ModelDiscovered': 'provider',
  'SecurityThreatDetected': 'security',
  'SystemHealthChange': 'system',
  'ConfigurationChange': 'system'
} as const;