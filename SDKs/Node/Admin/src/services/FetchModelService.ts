import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import { 
  ProviderTypeAssociation,
  ProviderTypeAssociationInput,
  NormalizedProviderTypeAssociation,
  ValidationResult
} from '../types/models';
import {
  normalizeProviderType,
  getProviderMetadata,
  getAvailableProviders,
  getProviderConstraints,
  getProviderTypeName,
  ProviderMetadata,
  ProviderConstraints
} from '../types/providers';
import { ProviderType } from '../models/providerType';
import {
  validateProviderTypeAssociation,
  applyProviderDefaults
} from '../validation/modelValidation';
import {
  ModelValidationError,
  InvalidProviderTypeError
} from '../errors/modelErrors';

// Type aliases for better readability
type ModelDto = components['schemas']['ConduitLLM.Admin.Models.Models.ModelDto'];
type CreateModelDto = components['schemas']['ConduitLLM.Admin.Models.Models.CreateModelDto'];
type UpdateModelDto = components['schemas']['ConduitLLM.Admin.Models.Models.UpdateModelDto'];
type ModelProviderMappingDto = components['schemas']['ConduitLLM.Configuration.DTOs.ModelProviderMappingDto'];

/**
 * Type-safe Model service using native fetch
 */
export class FetchModelService {
  constructor(private readonly client: FetchBaseApiClient) {}
  
  /**
   * Get available provider types with metadata
   */
  getAvailableProviders(): ProviderMetadata[] {
    return getAvailableProviders();
  }
  
  /**
   * Get provider metadata by type
   */
  getProviderMetadata(provider: string): ProviderMetadata | undefined {
    return getProviderMetadata(provider);
  }
  
  /**
   * Get provider validation constraints
   */
  getProviderConstraints(): ProviderConstraints {
    return getProviderConstraints();
  }
  
  /**
   * Normalize a provider type string
   */
  normalizeProviderType(provider: string): ProviderType | undefined {
    return normalizeProviderType(provider);
  }

  /**
   * Get all models with their capabilities
   */
  async list(config?: RequestConfig): Promise<ModelDto[]> {
    return this.client['get']<ModelDto[]>(
      ENDPOINTS.MODELS.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific model by ID
   */
  async get(id: number, config?: RequestConfig): Promise<ModelDto> {
    return this.client['get']<ModelDto>(
      ENDPOINTS.MODELS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Validate a provider type association without saving
   */
  validateIdentifier(data: Partial<ProviderTypeAssociationInput>): ValidationResult<ProviderTypeAssociationInput> {
    return validateProviderTypeAssociation(data);
  }
  
  /**
   * Get model identifiers for a specific model with normalized provider types
   */
  async getIdentifiers(id: number, config?: RequestConfig): Promise<NormalizedProviderTypeAssociation[]> {
    const identifiers = await this.client['get']<ProviderTypeAssociation[]>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/identifiers`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
    
    // Normalize provider types in the response
    return identifiers.map(identifier => {
      // Provider is now a number from the API
      const normalizedProvider = identifier.provider ? identifier.provider as ProviderType : null;
      return {
        ...identifier,
        normalizedProvider: normalizedProvider ?? null,
        providerName: normalizedProvider ? getProviderTypeName(normalizedProvider) : null
      };
    });
  }

  /**
   * Get available providers for a model - returns associations with matching providers
   */
  async getModelProviders(id: number, config?: RequestConfig): Promise<Array<{
    associationId: number;
    identifier: string;
    provider: string | null;
    providerVariation: string | null;
    maxInputTokens: number | null;
    maxOutputTokens: number | null;
    speedScore: number | null;
    qualityScore: number | null;
    isPrimary: boolean;
    availableProviders: Array<{
      providerId: number;
      providerName: string;
      providerType: string;
    }>;
  }>> {
    return this.client['get']<Array<{
      associationId: number;
      identifier: string;
      provider: string | null;
      providerVariation: string | null;
      maxInputTokens: number | null;
      maxOutputTokens: number | null;
      speedScore: number | null;
      qualityScore: number | null;
      isPrimary: boolean;
      availableProviders: Array<{
        providerId: number;
        providerName: string;
        providerType: string;
      }>;
    }>>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/available-providers`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get models by type
   */
  async getByType(type: string, config?: RequestConfig): Promise<ModelDto[]> {
    return this.client['get']<ModelDto[]>(
      ENDPOINTS.MODELS.BY_TYPE(type),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get models by provider
   */
  async getByProvider(provider: string, config?: RequestConfig): Promise<ModelDto[]> {
    return this.client['get']<ModelDto[]>(
      ENDPOINTS.MODELS.BY_PROVIDER(provider),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Search for models by name
   */
  async search(query: string, config?: RequestConfig): Promise<ModelDto[]> {
    const params = new URLSearchParams({ query });
    return this.client['get']<ModelDto[]>(
      `${ENDPOINTS.MODELS.SEARCH}?${params.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new model
   */
  async create(
    data: CreateModelDto,
    config?: RequestConfig
  ): Promise<ModelDto> {
    return this.client['post']<ModelDto, CreateModelDto>(
      ENDPOINTS.MODELS.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update an existing model
   */
  async update(
    id: number,
    data: UpdateModelDto,
    config?: RequestConfig
  ): Promise<ModelDto> {
    return this.client['put']<ModelDto, UpdateModelDto>(
      ENDPOINTS.MODELS.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a model
   */
  async delete(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.MODELS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get models with their provider mapping status and details
   * This is a helper method that checks which models have provider mappings
   */
  async listWithMappingStatus(config?: RequestConfig): Promise<Array<ModelDto & { 
    hasProviderMappings: boolean;
    providerCount: number;
    providers: Array<{
      id: number;
      identifier: string;
      provider: number | null;
      isPrimary: boolean;
      normalizedProvider: ProviderType | null;
      providerName: string | null;
    }>;
  }>> {
    // Get all models
    const models = await this.list(config);
    
    // Check each model for provider mappings in parallel
    const modelsWithStatus = await Promise.all(
      models.map(async (model) => {
        if (!model.id) {
          return { 
            ...model, 
            hasProviderMappings: false,
            providerCount: 0,
            providers: []
          };
        }
        
        try {
          const identifiers = await this.getIdentifiers(model.id, config);
          return { 
            ...model, 
            hasProviderMappings: identifiers.length > 0,
            providerCount: identifiers.length,
            providers: identifiers
          };
        } catch {
          // If there's an error getting identifiers, assume no mappings
          return { 
            ...model, 
            hasProviderMappings: false,
            providerCount: 0,
            providers: []
          };
        }
      })
    );
    
    return modelsWithStatus;
  }

  /**
   * Get all provider mappings for a specific model
   */
  async getProviderMappings(id: number, config?: RequestConfig): Promise<ModelProviderMappingDto[]> {
    return this.client['get']<ModelProviderMappingDto[]>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/provider-mappings`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new provider mapping for a model
   */
  async createProviderMapping(
    id: number, 
    mapping: ModelProviderMappingDto, 
    config?: RequestConfig
  ): Promise<ModelProviderMappingDto> {
    return this.client['post']<ModelProviderMappingDto, ModelProviderMappingDto>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/provider-mappings`,
      mapping,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update a provider mapping for a model
   */
  async updateProviderMapping(
    id: number,
    mappingId: number,
    mapping: ModelProviderMappingDto,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['put']<void, ModelProviderMappingDto>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/provider-mappings/${mappingId}`,
      mapping,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a provider mapping for a model
   */
  async deleteProviderMapping(
    id: number,
    mappingId: number,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['delete']<void>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/provider-mappings/${mappingId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new model identifier with validation and normalization
   */
  async createIdentifier(
    id: number,
    data: ProviderTypeAssociationInput,
    config?: RequestConfig
  ): Promise<NormalizedProviderTypeAssociation> {
    // Validate input
    const validation = validateProviderTypeAssociation(data);
    if (!validation.valid) {
      throw new ModelValidationError('Validation failed', validation.errors ?? {});
    }
    
    // Normalize provider type to numeric value
    if (data.provider !== undefined && data.provider !== null) {
      const normalized = normalizeProviderType(data.provider);
      if (!normalized) {
        throw new InvalidProviderTypeError(String(data.provider));
      }
      // Keep as numeric value for API
      data.provider = normalized;
    }
    
    // Apply provider defaults
    const dataWithDefaults = applyProviderDefaults(data);
    
    const result = await this.client['post']<ProviderTypeAssociation, typeof dataWithDefaults>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/identifiers`,
      dataWithDefaults,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
    
    // Return normalized result (provider is now a number from API)
    const normalizedProvider = result.provider ? result.provider as ProviderType : null;
    return {
      ...result,
      normalizedProvider: normalizedProvider ?? null,
      providerName: normalizedProvider ? getProviderTypeName(normalizedProvider) : null
    };
  }

  /**
   * Update a model identifier with validation and normalization
   */
  async updateIdentifier(
    id: number,
    identifierId: number,
    data: ProviderTypeAssociationInput,
    config?: RequestConfig
  ): Promise<void> {
    // Validate input
    const validation = validateProviderTypeAssociation(data);
    if (!validation.valid) {
      throw new ModelValidationError('Validation failed', validation.errors ?? {});
    }
    
    // Normalize provider type to numeric value
    if (data.provider !== undefined && data.provider !== null) {
      const normalized = normalizeProviderType(data.provider);
      if (!normalized) {
        throw new InvalidProviderTypeError(String(data.provider));
      }
      // Keep as numeric value for API
      data.provider = normalized;
    }
    
    // Apply provider defaults
    const dataWithDefaults = applyProviderDefaults(data);
    
    return this.client['put']<void, typeof dataWithDefaults>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/identifiers/${identifierId}`,
      dataWithDefaults,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a model identifier
   */
  async deleteIdentifier(
    id: number,
    identifierId: number,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['delete']<void>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/identifiers/${identifierId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}