import { FilterOptions } from './common';
import { ProviderReferenceDto } from './provider';

export interface ModelProviderMappingDto {
  id: number;
  modelAlias: string;  // The alias used by clients
  providerId: number;
  provider?: ProviderReferenceDto;
  providerModelId: string;
  modelProviderTypeAssociationId: number;  // REQUIRED: Links to provider-specific model metadata
  isEnabled: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
  notes?: string;
  /** Optional provider-specific request options (JSON object) merged into outgoing requests (OpenRouter). */
  providerOptions?: string;
  capabilities?: ModelCapabilitiesDto;
}

export interface ModelCapabilitiesDto {
  supportsVision: boolean;
  supportsImageGeneration: boolean;
  supportsVideoGeneration: boolean;
  supportsEmbeddings: boolean;
  // Audio + rerank capabilities added to the backend after the gate landed (issue #1038).
  supportsSpeechToText?: boolean;
  supportsTextToSpeech?: boolean;
  supportsRerank?: boolean;
  supportsChat: boolean;
  supportsFunctionCalling: boolean;
  supportsStreaming: boolean;
  maxInputTokens?: number | null;
  maxOutputTokens?: number | null;
}

export interface CreateModelProviderMappingDto {
  modelAlias: string;   // The alias used by clients
  providerId: number;
  providerModelId: string;
  modelProviderTypeAssociationId: number;  // REQUIRED: Links to provider-specific model metadata
  isEnabled?: boolean;
  priority?: number;
  notes?: string;
  providerOptions?: string;
}

export interface UpdateModelProviderMappingDto {
  /**
   * The ID of the model mapping.
   * Required by backend for validation - must match the ID in the route.
   */
  id?: number;
  modelAlias?: string;
  providerId?: number;
  providerModelId?: string;
  modelProviderTypeAssociationId?: number;  // Links to provider-specific model metadata
  isEnabled?: boolean;
  priority?: number;
  notes?: string;
  providerOptions?: string;
}

// For bulk operations
export interface BulkMappingResult {
  created: ModelProviderMappingDto[];
  errors: string[];
  totalProcessed: number;
  successCount: number;
  failureCount: number;
}

// For bulk mapping requests
export interface BulkMappingRequest {
  mappings: CreateModelProviderMappingDto[];
}

// For bulk mapping responses
export type BulkMappingResponse = BulkMappingResult;

// For bulk delete operations
export interface BulkDeleteResult {
  deletedIds: number[];
  errors: string[];
  totalProcessed: number;
  successCount: number;
  failureCount: number;
}

// For bulk update operations
export interface BulkUpdateResult {
  updated: ModelProviderMappingDto[];
  errors: string[];
  totalProcessed: number;
  successCount: number;
  failureCount: number;
}

// For discovered models
export interface DiscoveredModel {
  id: string;
  name: string;
  description?: string;
  capabilities?: string[];
  maxTokens?: number;
}


// For model routing information
export interface ModelRoutingInfo {
  modelAlias: string;
  providerId: number;
  providerModelId: string;
  priority: number;
}

// For model mapping suggestions
export interface ModelMappingSuggestion {
  modelAlias: string;
  providerModelId: string;
  confidence: number;
  reason?: string;
}

// For capability test results
export interface CapabilityTestResult {
  capability: string;
  supported: boolean;
  details?: string;
}

export interface ModelMappingFilterOptions extends FilterOptions {
  /**
   * Filter by model ID
   */
  modelId?: number;
  
  /**
   * Filter by provider ID
   */
  providerId?: number;
  
  /**
   * Filter by enabled status
   */
  isEnabled?: boolean;
  
  /**
   * Filter by minimum priority
   */
  minPriority?: number;
  
  /**
   * Filter by maximum priority
   */
  maxPriority?: number;
}