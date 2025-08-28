import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

// Type aliases for better readability
type ModelDto = components['schemas']['ConduitLLM.Admin.Models.Models.ModelDto'];
type CreateModelDto = components['schemas']['ConduitLLM.Admin.Models.Models.CreateModelDto'];
type UpdateModelDto = components['schemas']['ConduitLLM.Admin.Models.Models.UpdateModelDto'];

/**
 * Type-safe Model service using native fetch
 */
export class FetchModelService {
  constructor(private readonly client: FetchBaseApiClient) {}

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
   * Get model identifiers for a specific model
   */
  async getIdentifiers(id: number, config?: RequestConfig): Promise<Array<{
    id: number;
    identifier: string;
    provider: string;
    isPrimary: boolean;
  }>> {
    return this.client['get']<Array<{
      id: number;
      identifier: string;
      provider: string;
      isPrimary: boolean;
    }>>(
      `${ENDPOINTS.MODELS.BY_ID(id)}/identifiers`,
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
      provider: string;
      isPrimary: boolean;
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
}