import { FilterOptions } from './common';

/** Function provider type (e.g., Exa) */
export enum FunctionProviderType {
  Exa = 1
}

/** Function purpose category */
export enum FunctionPurpose {
  Search = 1,
  Answer = 2,
  Enrich = 3,
  Other = 99
}

/** Function execution mode */
export enum FunctionExecutionMode {
  Synchronous = 1,
  Asynchronous = 2
}

/** Function execution state */
export enum ExecutionState {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4
}

/** Function cost pricing model */
export enum FunctionPricingModel {
  FlatRate = 1,
  PerResult = 2,
  PerToken = 3,
  TimeBased = 4,
  Tiered = 5,
  Hybrid = 6
}

// ============================================================================
// Function Configuration
// ============================================================================

export interface FunctionConfigurationDto {
  id: number;
  configurationName: string;
  providerType: FunctionProviderType;
  purpose: FunctionPurpose;
  description?: string;
  defaultExecutionMode: FunctionExecutionMode;
  timeoutSeconds: number;
  isEnabled: boolean;
  metadata?: string; // JSON
  createdAt: string;
  updatedAt: string;
}

export interface CreateFunctionConfigurationDto {
  configurationName: string;
  providerType: FunctionProviderType;
  purpose: FunctionPurpose;
  description?: string;
  defaultExecutionMode?: FunctionExecutionMode; // Default: Synchronous
  timeoutSeconds?: number; // Default: 30
  isEnabled?: boolean; // Default: true
  metadata?: string; // JSON
}

export interface UpdateFunctionConfigurationDto {
  id: number;
  configurationName?: string;
  purpose?: FunctionPurpose;
  description?: string;
  defaultExecutionMode?: FunctionExecutionMode;
  timeoutSeconds?: number;
  isEnabled?: boolean;
  metadata?: string; // JSON
}

// ============================================================================
// Function Credential
// ============================================================================

export interface FunctionCredentialDto {
  id: number;
  functionConfigurationId: number;
  keyName: string;
  apiKey: string;
  functionAccountGroup: number;
  isPrimary: boolean;
  isEnabled: boolean;
  metadata?: string; // JSON
  createdAt: string;
  updatedAt: string;
  functionConfiguration?: FunctionConfigurationDto;
}

export interface CreateFunctionCredentialDto {
  functionConfigurationId: number;
  keyName: string;
  apiKey: string;
  functionAccountGroup?: number; // Default: 0
  isPrimary?: boolean; // Default: false
  isEnabled?: boolean; // Default: true
  metadata?: string; // JSON
}

export interface UpdateFunctionCredentialDto {
  id: number;
  keyName?: string;
  apiKey?: string;
  functionAccountGroup?: number;
  isPrimary?: boolean;
  isEnabled?: boolean;
  metadata?: string; // JSON
}

export interface TestCredentialRequestDto {
  credentialId: number;
  apiKeyOverride?: string;
}

export interface TestCredentialResponseDto {
  success: boolean;
  message?: string;
  details?: Record<string, unknown>;
  durationMs: number;
}

// ============================================================================
// Function Cost
// ============================================================================

export interface FunctionCostDto {
  id: number;
  costName: string;
  providerType: FunctionProviderType;
  purpose?: FunctionPurpose;
  pricingModel: FunctionPricingModel;
  pricingConfiguration: string; // JSON
  baseCost?: number;
  isActive: boolean;
  priority: number;
  effectiveDate: string;
  expiryDate?: string;
  description?: string;
  createdAt: string;
  updatedAt: string;
  // Related data
  associatedConfigurations?: string[]; // Configuration names using this cost
}

export interface CreateFunctionCostDto {
  costName: string;
  providerType: FunctionProviderType;
  purpose?: FunctionPurpose;
  pricingModel: FunctionPricingModel;
  pricingConfiguration: string; // JSON
  baseCost?: number;
  isActive?: boolean; // Default: true
  priority?: number; // Default: 0
  effectiveDate?: string; // Default: now
  expiryDate?: string;
  description?: string;
}

export interface UpdateFunctionCostDto {
  id: number;
  costName?: string;
  purpose?: FunctionPurpose;
  pricingModel?: FunctionPricingModel;
  pricingConfiguration?: string; // JSON
  baseCost?: number;
  isActive?: boolean;
  priority?: number;
  effectiveDate?: string;
  expiryDate?: string;
  description?: string;
}

// ============================================================================
// Function Cost Mapping
// ============================================================================

export interface FunctionCostMappingDto {
  id: number;
  functionConfigurationId: number;
  functionCostId: number;
  effectiveDate: string;
  expiryDate?: string;
  createdAt: string;
  updatedAt: string;
  functionConfiguration?: FunctionConfigurationDto;
  functionCost?: FunctionCostDto;
}

export interface CreateFunctionCostMappingDto {
  functionConfigurationId: number;
  functionCostId: number;
  effectiveDate?: string; // Default: now
  expiryDate?: string;
}

export interface UpdateFunctionCostMappingDto {
  id: number;
  functionCostId?: number;
  effectiveDate?: string;
  expiryDate?: string;
}

// ============================================================================
// Function Execution
// ============================================================================

export interface FunctionExecutionDto {
  id: string; // Guid
  functionConfigurationId: number;
  virtualKeyId: number;
  state: ExecutionState;
  executionMode: FunctionExecutionMode;
  requestJson: string; // JSON
  responseJson?: string; // JSON
  errorMessage?: string;
  estimatedCost?: number;
  actualCost?: number;
  costCalculationDetails?: string; // JSON
  startedAt?: string;
  completedAt?: string;
  duration?: number; // TimeSpan in milliseconds
  createdAt: string;
  updatedAt: string;
  functionConfiguration?: FunctionConfigurationDto;
}

// ============================================================================
// Pricing Configuration Types
// ============================================================================

/** Base pricing configuration interface */
export interface BasePricingConfig {
  pricingModel: FunctionPricingModel;
}

/** Flat rate pricing - single fixed cost per execution */
export interface FlatRatePricingConfig extends BasePricingConfig {
  pricingModel: FunctionPricingModel.FlatRate;
  costPerExecution: number;
}

/** Per-result pricing - cost scales with number of results */
export interface PerResultPricingConfig extends BasePricingConfig {
  pricingModel: FunctionPricingModel.PerResult;
  costPerResult: number;
  minimumCost?: number;
}

/** Per-token pricing - similar to LLM pricing */
export interface PerTokenPricingConfig extends BasePricingConfig {
  pricingModel: FunctionPricingModel.PerToken;
  costPerMillionTokens: number;
  minimumCost?: number;
}

/** Time-based pricing - cost by execution duration */
export interface TimeBasedPricingConfig extends BasePricingConfig {
  pricingModel: FunctionPricingModel.TimeBased;
  costPerSecond: number;
  minimumCost?: number;
  roundUpToNearestSecond?: boolean;
}

/** Tiered pricing - different rates for different result count ranges */
export interface TieredPricingConfig extends BasePricingConfig {
  pricingModel: FunctionPricingModel.Tiered;
  tiers: Array<{
    minResults: number;
    maxResults?: number;
    costPerResult: number;
  }>;
  baseCost?: number;
}

/** Hybrid pricing - combines multiple cost factors (Exa-specific) */
export interface ExaHybridPricingConfig extends BasePricingConfig {
  pricingModel: FunctionPricingModel.Hybrid;
  // Search costs
  neuralSearchCosts: {
    tier1: { minResults: number; maxResults: number; costPerResult: number }; // 1-25
    tier2: { minResults: number; maxResults?: number; costPerResult: number }; // 26-100
  };
  keywordSearchCosts: {
    tier1: { minResults: number; maxResults: number; costPerResult: number };
    tier2: { minResults: number; maxResults?: number; costPerResult: number };
  };
  // Content extraction costs
  contentExtractionCosts: {
    text: number; // Per page
    highlights: number; // Per page
    summary: number; // Per page
  };
}

/** Union type for all pricing configurations */
export type FunctionPricingConfig =
  | FlatRatePricingConfig
  | PerResultPricingConfig
  | PerTokenPricingConfig
  | TimeBasedPricingConfig
  | TieredPricingConfig
  | ExaHybridPricingConfig;

// ============================================================================
// List Responses with Pagination
// ============================================================================

export interface FunctionConfigurationListResponse {
  items: FunctionConfigurationDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface FunctionCredentialListResponse {
  items: FunctionCredentialDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface FunctionCostListResponse {
  items: FunctionCostDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface FunctionExecutionListResponse {
  items: FunctionExecutionDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ============================================================================
// Filter Options
// ============================================================================

export interface FunctionConfigurationFilters extends FilterOptions {
  providerType?: FunctionProviderType;
  purpose?: FunctionPurpose;
  isEnabled?: boolean;
}

export interface FunctionCostFilters extends FilterOptions {
  providerType?: FunctionProviderType;
  purpose?: FunctionPurpose;
  pricingModel?: FunctionPricingModel;
  isActive?: boolean;
}

export interface FunctionExecutionFilters extends FilterOptions {
  virtualKeyId?: number;
  functionConfigurationId?: number;
  state?: ExecutionState;
  startDate?: string;
  endDate?: string;
}
