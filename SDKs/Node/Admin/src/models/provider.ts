import { FilterOptions } from './common';
import { ProviderConfigMetadata } from './metadata';
import { ProviderType } from './providerType';

// Provider DTOs - Provider ID is the canonical identifier
export interface ProviderDto {
  id: number;
  providerType: ProviderType;
  providerName: string; // User-friendly display name, can be changed
  baseUrl?: string | null;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
  // Note: apiKey and organization moved to ProviderKeyCredential
}

export interface CreateProviderDto {
  providerType: ProviderType;
  providerName: string;
  baseUrl?: string | null;
  isEnabled?: boolean;
}

export interface UpdateProviderDto {
  providerName?: string;
  baseUrl?: string | null;
  isEnabled?: boolean;
}


export interface ProviderConnectionTestRequest {
  providerType: ProviderType;
  apiKey?: string;
  baseUrl?: string | null;
  organization?: string | null;
}

export interface ProviderConnectionTestResultDto {
  success: boolean;
  message: string;
  errorDetails?: string;
  providerType: ProviderType;
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
  configSchema?: ProviderConfigMetadata;
}

export interface ProviderHealthConfigurationDto {
  providerType: ProviderType;
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
  providerType: ProviderType;
  checkTime: string;
  isHealthy: boolean;
  responseTimeMs?: number;
  errorMessage?: string;
  statusCode?: number;
  modelsChecked?: string[];
}

export interface ProviderHealthStatusDto {
  providerType: ProviderType;
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

export interface CreateProviderHealthConfigurationDto {
  providerType: ProviderType;
  monitoringEnabled?: boolean;
  checkIntervalMinutes?: number;
  timeoutSeconds?: number;
  consecutiveFailuresThreshold?: number;
  notificationsEnabled?: boolean;
  customEndpointUrl?: string;
}

export interface ProviderHealthStatisticsDto {
  totalProviders: number;
  onlineProviders: number;
  offlineProviders: number;
  unknownProviders: number;
  averageResponseTimeMs: number;
  totalErrors: number;
  errorCategoryDistribution: Record<string, number>;
  timePeriodHours: number;
}

export enum StatusType {
  Online = 0,
  Offline = 1,
  Unknown = 2
}

export interface ProviderStatus {
  status: StatusType;
  statusMessage?: string;
  responseTimeMs: number;
  lastCheckedUtc: Date;
  errorCategory?: string;
}

export interface ProviderFilters extends FilterOptions {
  isEnabled?: boolean;
  providerType?: ProviderType;
  hasApiKey?: boolean;
  isHealthy?: boolean;
}

export interface ProviderHealthFilters extends FilterOptions {
  providerType?: ProviderType;
  isHealthy?: boolean;
  startDate?: string;
  endDate?: string;
  minResponseTime?: number;
  maxResponseTime?: number;
}

export interface ProviderUsageStatistics {
  providerType: ProviderType;
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

// Provider Key Credential interfaces
export interface ProviderKeyCredentialDto {
  id: number;
  providerCredentialId: number;
  apiKey: string;
  organization?: string;
  keyName?: string;
  isPrimary: boolean;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
  lastUsedAt?: string;
  usageCount: number;
  errorCount: number;
  rateLimitExceededCount: number;
}

export interface CreateProviderKeyCredentialDto {
  apiKey: string;
  organization?: string;
  keyName?: string;
  isPrimary?: boolean;
  isEnabled?: boolean;
}

export interface UpdateProviderKeyCredentialDto {
  apiKey?: string;
  organization?: string;
  keyName?: string;
  isEnabled?: boolean;
}

export interface ProviderKeyRotationDto {
  newApiKey: string;
  organization?: string;
  keyName?: string;
}

// API Key Test Response Types
export enum ApiKeyTestResult {
  SUCCESS = 'success',
  INVALID_KEY = 'invalid_key',
  IGNORED = 'ignored',
  PROVIDER_DOWN = 'provider_down',
  RATE_LIMITED = 'rate_limited',
  UNKNOWN_ERROR = 'unknown_error'
}

export interface StandardApiKeyTestResponse {
  result: ApiKeyTestResult;
  message: string;
  details?: {
    responseTimeMs?: number;
    modelsAvailable?: string[];
    providerMessage?: string; // Raw provider message for debugging
    errorCode?: string;
    statusCode?: number;
  };
}

// Lightweight DTO for referencing providers without exposing sensitive data
export interface ProviderReferenceDto {
  id: number;
  providerType: ProviderType;
  displayName: string;
  isEnabled: boolean;
}