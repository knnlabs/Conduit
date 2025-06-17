import { FilterOptions } from './common';

export interface ModelProviderMappingDto {
  id: number;
  modelId: string;
  providerId: string;
  providerModelId: string;
  isEnabled: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
  metadata?: string;
}

export interface CreateModelProviderMappingDto {
  modelId: string;
  providerId: string;
  providerModelId: string;
  isEnabled?: boolean;
  priority?: number;
  metadata?: string;
}

export interface UpdateModelProviderMappingDto {
  providerId?: string;
  providerModelId?: string;
  isEnabled?: boolean;
  priority?: number;
  metadata?: string;
}

export interface ModelMappingFilters extends FilterOptions {
  modelId?: string;
  providerId?: string;
  isEnabled?: boolean;
  minPriority?: number;
  maxPriority?: number;
}

export interface ModelProviderInfo {
  providerId: string;
  providerName: string;
  providerModelId: string;
  isAvailable: boolean;
  isEnabled: boolean;
  priority: number;
  estimatedCost?: {
    inputTokenCost: number;
    outputTokenCost: number;
    currency: string;
  };
}

export interface ModelRoutingInfo {
  modelId: string;
  primaryProvider?: ModelProviderInfo;
  fallbackProviders: ModelProviderInfo[];
  loadBalancingEnabled: boolean;
  routingStrategy: 'priority' | 'round-robin' | 'least-cost' | 'fastest';
}

export interface BulkMappingRequest {
  mappings: CreateModelProviderMappingDto[];
  replaceExisting?: boolean;
}

export interface BulkMappingResponse {
  created: ModelProviderMappingDto[];
  updated: ModelProviderMappingDto[];
  failed: {
    index: number;
    error: string;
    mapping: CreateModelProviderMappingDto;
  }[];
}

export interface ModelMappingSuggestion {
  modelId: string;
  suggestedProviders: {
    providerId: string;
    providerName: string;
    providerModelId: string;
    confidence: number;
    reasoning: string;
    estimatedPerformance?: {
      latency: number;
      reliability: number;
      costEfficiency: number;
    };
  }[];
}