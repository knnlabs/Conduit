import { FilterOptions } from './common';
import { ProviderReferenceDto } from './provider';

export interface ModelProviderMappingDto {
  id: number;
  modelAlias: string;  // The alias used by clients
  modelId: number;     // Reference to canonical Model entity
  providerId: number;
  provider?: ProviderReferenceDto;
  providerModelId: string;
  isEnabled: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
  notes?: string;
  
  // Provider-specific overrides
  maxContextTokensOverride?: number;
  
  // Provider metadata
  providerVariation?: string;  // e.g., "Q4_K_M", "GGUF", "instruct"
  qualityScore?: number;        // 1.0 = identical to original
  
  // Advanced Routing Fields
  isDefault: boolean;
  defaultCapabilityType?: string;
}

export interface CreateModelProviderMappingDto {
  modelAlias: string;   // The alias used by clients
  modelId: number;      // Reference to canonical Model entity (required)
  providerId: number;
  providerModelId: string;
  isEnabled?: boolean;
  priority?: number;
  
  // Provider-specific overrides
  maxContextTokensOverride?: number;
  
  // Provider metadata
  providerVariation?: string;
  qualityScore?: number;
  
  // Advanced Routing Fields
  isDefault?: boolean;
  defaultCapabilityType?: string;
  
  notes?: string;
}

export interface UpdateModelProviderMappingDto {
  /**
   * The ID of the model mapping.
   * Required by backend for validation - must match the ID in the route.
   */
  id?: number;
  modelAlias?: string;
  modelId?: number;
  providerId?: number;
  providerModelId?: string;
  isEnabled?: boolean;
  priority?: number;
  
  // Provider-specific overrides
  maxContextTokensOverride?: number;
  
  // Provider metadata
  providerVariation?: string;
  qualityScore?: number;
  
  // Advanced Routing Fields
  isDefault?: boolean;
  defaultCapabilityType?: string;
  
  notes?: string;
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
   * Filter by default status
   */
  isDefault?: boolean;
  
  /**
   * Filter by capability type
   */
  capabilityType?: string;
  
  /**
   * Filter by minimum priority
   */
  minPriority?: number;
  
  /**
   * Filter by maximum priority
   */
  maxPriority?: number;
}