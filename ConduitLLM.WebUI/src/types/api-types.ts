/**
 * API response types for the WebUI
 * These mirror the SDK types but are defined locally to avoid importing the SDK on the client side
 */

// Virtual Key types
export interface VirtualKeyDto {
  id: number;
  keyName: string;
  keyPrefix: string;
  apiKey?: string;
  isEnabled: boolean;
  maxBudget?: number;
  budgetDuration?: 'Daily' | 'Monthly' | 'Total';
  currentSpend: number;
  lastResetDate?: string;
  rateLimitPerMinute?: number;
  requestCount: number;
  allowedModels?: string;
  allowedEndpoints?: string;
  allowedIpAddresses?: string;
  metadata?: string;
  createdAt: string;
  updatedAt: string;
  lastUsedAt?: string;
  expiresAt?: string;
}

// Provider types
export interface ProviderCredentialDto {
  id: number;
  providerName: string;
  apiKey?: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ProviderHealthStatusDto {
  providerId: string;
  isHealthy: boolean;
  lastCheckTime: string;
  responseTimeMs?: number;
  errorMessage?: string;
}

// Model Mapping types
export interface ModelProviderMappingDto {
  id: number;
  modelId: string;
  providerId: string;
  providerModelId: string;
  priority: number;
  isEnabled: boolean;
  metadata?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateModelProviderMappingDto {
  modelId: string;
  providerId: string;
  providerModelId: string;
  priority?: number;
  isEnabled?: boolean;
  metadata?: string;
}

// Other types that pages might need
export type BudgetDuration = 'Daily' | 'Monthly' | 'Total';

export interface ModelCapability {
  name: string;
  description?: string;
}