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
  /** When true, the provider-reported per-request cost is authoritative for billing (falls back to ModelCost). */
  trustProviderReportedCosts: boolean;
  /** Multiplier applied to the provider-reported cost when billing (1.0 = pass-through). */
  providerCostMarkupMultiplier: number;
  createdAt: string;
  updatedAt: string;
  // Note: apiKey and organization moved to ProviderKeyCredential
}

export interface CreateProviderDto {
  providerType: ProviderType;
  providerName: string;
  baseUrl?: string | null;
  isEnabled?: boolean;
  trustProviderReportedCosts?: boolean;
  providerCostMarkupMultiplier?: number;
}

export interface UpdateProviderDto {
  providerName?: string;
  baseUrl?: string | null;
  isEnabled?: boolean;
  trustProviderReportedCosts?: boolean;
  providerCostMarkupMultiplier?: number;
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

export interface ProviderFilters extends FilterOptions {
  isEnabled?: boolean;
  providerType?: ProviderType;
  hasApiKey?: boolean;
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
  providerId: number;
  apiKey: string;
  organization?: string;
  keyName?: string;
  isPrimary: boolean;
  isEnabled: boolean;
  providerAccountGroup?: number;
  baseUrl?: string;
  createdAt: string;
  updatedAt: string;
  lastUsedAt?: string;
  usageCount?: number;
  errorCount?: number;
  rateLimitExceededCount?: number;
}

export interface CreateProviderKeyCredentialDto {
  apiKey: string;
  organization?: string;
  keyName?: string;
  isPrimary?: boolean;
  isEnabled?: boolean;
  providerAccountGroup?: number;
  baseUrl?: string;
}

export interface UpdateProviderKeyCredentialDto {
  apiKey?: string;
  organization?: string;
  keyName?: string;
  isEnabled?: boolean;
  isPrimary?: boolean;
  providerAccountGroup?: number;
  baseUrl?: string;
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