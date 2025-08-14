import { FilterOptions } from './common';
import type { ModelConfigMetadata } from './metadata';
import { ProviderType } from './providerType';
import { ModelType } from './modelType';

/** Pricing model type that determines how costs are calculated */
export enum PricingModel {
  Standard = 0,
  PerVideo = 1,
  PerSecondVideo = 2,
  InferenceSteps = 3,
  TieredTokens = 4,
  PerImage = 5,
  PerMinuteAudio = 6,
  PerThousandCharacters = 7
}

/** @deprecated Use ModelCostDto instead - pattern matching has been removed */
export interface ModelCost {
  id: number;
  modelIdPattern: string; // @deprecated - no longer used
  providerType: ProviderType; // @deprecated - costs are now mapped to specific models
  modelType: ModelType;
  inputCostPerMillionTokens?: number;
  outputCostPerMillionTokens?: number;
  costPerRequest?: number;
  costPerSecond?: number;
  costPerImage?: number;
  isActive: boolean;
  priority: number;
  effectiveDate: string;
  expiryDate?: string;
  metadata?: ModelConfigMetadata;
  createdAt: string;
  updatedAt: string;
  // Phase 1 fields
  batchProcessingMultiplier?: number;
  supportsBatchProcessing: boolean;
  imageQualityMultipliers?: string;
  // Phase 2 fields
  cachedInputTokenCost?: number;
  cachedInputWriteCost?: number;
  costPerSearchUnit?: number;
  costPerInferenceStep?: number;
  defaultInferenceSteps?: number;
}

export interface ModelCostDto {
  id: number;
  costName: string; // User-friendly name like "GPT-4 Standard Pricing"
  associatedModelAliases: string[]; // Model aliases using this cost
  pricingModel: PricingModel; // The pricing model type
  pricingConfiguration?: string; // JSON configuration for polymorphic pricing
  modelType: ModelType;
  inputCostPerMillionTokens: number; // Cost per million tokens in USD
  outputCostPerMillionTokens: number; // Cost per million tokens in USD
  embeddingCostPerMillionTokens?: number; // Cost per million tokens in USD
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  audioCostPerKCharacters?: number;
  audioInputCostPerMinute?: number;
  audioOutputCostPerMinute?: number;
  videoCostPerSecond?: number;
  videoResolutionMultipliers?: string; // JSON string
  imageResolutionMultipliers?: string; // JSON string for image resolution multipliers
  isActive: boolean;
  priority: number;
  effectiveDate: string;
  expiryDate?: string;
  description?: string;
  createdAt: string;
  updatedAt: string;
  // Phase 1 fields
  batchProcessingMultiplier?: number;
  supportsBatchProcessing: boolean;
  imageQualityMultipliers?: string; // JSON string
  // Phase 2 fields
  cachedInputCostPerMillionTokens?: number; // Cost per million tokens in USD
  cachedInputWriteCostPerMillionTokens?: number; // Cost per million tokens in USD
  costPerSearchUnit?: number;
  costPerInferenceStep?: number;
  defaultInferenceSteps?: number;
}

export interface CreateModelCostDto {
  costName: string; // Required: User-friendly name
  modelProviderMappingIds: number[]; // IDs of ModelProviderMapping entities
  pricingModel?: PricingModel; // Default: Standard
  pricingConfiguration?: string; // JSON configuration for polymorphic pricing
  modelType?: ModelType; // Default: ModelType.Chat
  priority?: number; // Default: 0
  description?: string;
  inputCostPerMillionTokens: number; // Cost per million tokens in USD
  outputCostPerMillionTokens: number; // Cost per million tokens in USD
  embeddingCostPerMillionTokens?: number; // Cost per million tokens in USD
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  audioCostPerKCharacters?: number;
  audioInputCostPerMinute?: number;
  audioOutputCostPerMinute?: number;
  videoCostPerSecond?: number;
  videoResolutionMultipliers?: string; // JSON string
  imageResolutionMultipliers?: string; // JSON string for image resolution multipliers
  // Phase 1 fields
  batchProcessingMultiplier?: number;
  supportsBatchProcessing?: boolean;
  imageQualityMultipliers?: string; // JSON string
  // Phase 2 fields
  cachedInputCostPerMillionTokens?: number; // Cost per million tokens in USD
  cachedInputWriteCostPerMillionTokens?: number; // Cost per million tokens in USD
  costPerSearchUnit?: number;
  costPerInferenceStep?: number;
  defaultInferenceSteps?: number;
}

export interface UpdateModelCostDto {
  id: number; // Required for update
  costName: string; // Required: User-friendly name
  modelProviderMappingIds: number[]; // IDs of ModelProviderMapping entities
  pricingModel?: PricingModel;
  pricingConfiguration?: string; // JSON configuration for polymorphic pricing
  modelType?: ModelType;
  priority?: number;
  description?: string;
  isActive?: boolean;
  inputCostPerMillionTokens?: number; // Cost per million tokens in USD
  outputCostPerMillionTokens?: number; // Cost per million tokens in USD
  embeddingCostPerMillionTokens?: number; // Cost per million tokens in USD
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  audioCostPerKCharacters?: number;
  audioInputCostPerMinute?: number;
  audioOutputCostPerMinute?: number;
  videoCostPerSecond?: number;
  videoResolutionMultipliers?: string; // JSON string
  imageResolutionMultipliers?: string; // JSON string for image resolution multipliers
  // Phase 1 fields
  batchProcessingMultiplier?: number;
  supportsBatchProcessing?: boolean;
  imageQualityMultipliers?: string; // JSON string
  // Phase 2 fields
  cachedInputCostPerMillionTokens?: number; // Cost per million tokens in USD
  cachedInputWriteCostPerMillionTokens?: number; // Cost per million tokens in USD
  costPerSearchUnit?: number;
  costPerInferenceStep?: number;
  defaultInferenceSteps?: number;
}

export interface ModelCostFilters extends FilterOptions {
  modelId?: string;
  providerId?: string;
  currency?: string;
  isActive?: boolean;
  effectiveAfter?: string;
  effectiveBefore?: string;
  minInputCost?: number;
  maxInputCost?: number;
  minOutputCost?: number;
  maxOutputCost?: number;
}

export interface ModelCostCalculation {
  modelId: string;
  inputTokens: number;
  outputTokens: number;
  inputCost: number;
  outputCost: number;
  totalCost: number;
  currency: string;
  costPerMillionInputTokens: number;
  costPerMillionOutputTokens: number;
}

export interface BulkModelCostUpdate {
  modelIds: string[];
  adjustment: {
    type: 'percentage' | 'fixed';
    value: number;
    applyTo: 'input' | 'output' | 'both';
  };
  effectiveDate?: string;
  reason?: string;
}

export interface ModelCostHistory {
  modelId: string;
  history: {
    id: number;
    inputCostPerMillionTokens: number;
    outputCostPerMillionTokens: number;
    effectiveDate: string;
    expiryDate?: string;
    changeReason?: string;
  }[];
}

// Model Cost Mapping - Links costs to specific models
export interface ModelCostMappingDto {
  id: number;
  modelCostId: number;
  modelProviderMappingId: number;
  isActive: boolean;
  createdAt: string;
  modelAlias?: string; // From ModelProviderMapping
  providerModelId?: string; // From ModelProviderMapping
  costName?: string; // From ModelCost
}

export interface CreateModelCostMappingDto {
  modelCostId: number;
  modelProviderMappingIds: number[]; // Can map multiple models at once
  isActive?: boolean; // Default: true
}

export interface UpdateModelCostMappingDto {
  modelCostId: number;
  modelProviderMappingIds: number[]; // Replaces all existing mappings
}

export interface CostEstimate {
  scenarios: {
    name: string;
    inputTokens: number;
    outputTokens: number;
  }[];
  models: string[];
  results: {
    scenario: string;
    costs: {
      modelId: string;
      totalCost: number;
      inputCost: number;
      outputCost: number;
      currency: string;
    }[];
  }[];
  recommendations?: {
    mostCostEffective: string;
    bestValueForMoney: string;
    notes: string[];
  };
}

export interface ModelCostComparison {
  baseModel: string;
  comparisonModels: string[];
  inputTokens: number;
  outputTokens: number;
  results: {
    modelId: string;
    totalCost: number;
    costDifference: number;
    percentageDifference: number;
    currency: string;
  }[];
}

export interface ModelCostOverview {
  modelName: string;
  providerType: ProviderType;
  modelType: ModelType;
  totalRequests: number;
  totalTokens: number;
  totalCost: number;
  averageCostPerRequest: number;
  costTrend: 'increasing' | 'decreasing' | 'stable';
  trendPercentage: number;
}

export interface CostTrend {
  date: string;
  cost: number;
  requests: number;
  tokens: number;
}

export interface ImportResult {
  success: number;
  failed: number;
  errors: Array<{ row: number; error: string }>;
}