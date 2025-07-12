import { FilterOptions } from './common';
import type { ModelConfigMetadata } from './metadata';

export interface ModelCost {
  id: number;
  modelIdPattern: string;
  providerName: string;
  modelType: 'chat' | 'embedding' | 'image' | 'audio' | 'video';
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
}

export interface ModelCostDto {
  id: number;
  modelId: string;
  inputTokenCost: number;
  outputTokenCost: number;
  currency: string;
  effectiveDate: string;
  expiryDate?: string;
  providerId?: string;
  description?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateModelCostDto {
  modelId: string;
  inputTokenCost: number;
  outputTokenCost: number;
  currency?: string;
  effectiveDate?: string;
  expiryDate?: string;
  providerId?: string;
  description?: string;
  isActive?: boolean;
}

export interface UpdateModelCostDto {
  inputTokenCost?: number;
  outputTokenCost?: number;
  currency?: string;
  effectiveDate?: string;
  expiryDate?: string;
  providerId?: string;
  description?: string;
  isActive?: boolean;
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
  costPerThousandInputTokens: number;
  costPerThousandOutputTokens: number;
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
    inputTokenCost: number;
    outputTokenCost: number;
    effectiveDate: string;
    expiryDate?: string;
    changeReason?: string;
  }[];
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
  providerName: string;
  modelType: string;
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