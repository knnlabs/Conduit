import { FilterOptions } from './common';

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

export interface CreateProviderCredentialDto {
  providerName: string;
  apiKey?: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: string;
  isEnabled?: boolean;
}

export interface UpdateProviderCredentialDto {
  apiKey?: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: string;
  isEnabled?: boolean;
}

export interface ProviderConnectionTestRequest {
  providerName: string;
  apiKey?: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: string;
}

export interface ProviderConnectionTestResultDto {
  success: boolean;
  message: string;
  errorDetails?: string;
  providerName: string;
  modelsAvailable?: string[];
  responseTimeMs?: number;
  timestamp?: string;
}

export interface ProviderDataDto {
  name: string;
  displayName: string;
  supportedModels: string[];
  requiresApiKey: boolean;
  requiresEndpoint: boolean;
  requiresOrganizationId: boolean;
  configSchema?: Record<string, any>;
}

export interface ProviderHealthConfigurationDto {
  providerName: string;
  isEnabled: boolean;
  checkIntervalSeconds: number;
  timeoutSeconds: number;
  unhealthyThreshold: number;
  healthyThreshold: number;
  testModel?: string;
  lastCheckTime?: string;
  isHealthy?: boolean;
  consecutiveFailures?: number;
  consecutiveSuccesses?: number;
}

export interface UpdateProviderHealthConfigurationDto {
  isEnabled?: boolean;
  checkIntervalSeconds?: number;
  timeoutSeconds?: number;
  unhealthyThreshold?: number;
  healthyThreshold?: number;
  testModel?: string;
}

export interface ProviderHealthRecordDto {
  id: number;
  providerName: string;
  checkTime: string;
  isHealthy: boolean;
  responseTimeMs?: number;
  errorMessage?: string;
  statusCode?: number;
  modelsChecked?: string[];
}

export interface ProviderHealthStatusDto {
  providerName: string;
  isHealthy: boolean;
  lastCheckTime?: string;
  lastSuccessTime?: string;
  lastFailureTime?: string;
  consecutiveFailures: number;
  consecutiveSuccesses: number;
  averageResponseTimeMs?: number;
  uptime?: number;
  errorRate?: number;
}

export interface ProviderHealthSummaryDto {
  totalProviders: number;
  healthyProviders: number;
  unhealthyProviders: number;
  unconfiguredProviders: number;
  providers: ProviderHealthStatusDto[];
}

export interface ProviderFilters extends FilterOptions {
  isEnabled?: boolean;
  providerName?: string;
  hasApiKey?: boolean;
  isHealthy?: boolean;
}

export interface ProviderHealthFilters extends FilterOptions {
  providerName?: string;
  isHealthy?: boolean;
  startDate?: string;
  endDate?: string;
  minResponseTime?: number;
  maxResponseTime?: number;
}

export interface ProviderUsageStatistics {
  providerName: string;
  totalRequests: number;
  successfulRequests: number;
  failedRequests: number;
  averageResponseTime: number;
  totalCost: number;
  modelsUsed: Record<string, number>;
  errorTypes: Record<string, number>;
  timeRange: {
    start: string;
    end: string;
  };
}