import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';
import {
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
  providerTypeToOrdinal,
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
type ModelDto = components['schemas']['ModelDto'];
type CreateModelDto = components['schemas']['CreateModelDto'];
type UpdateModelDto = components['schemas']['UpdateModelDto'];
type ModelProviderMappingDto = components['schemas']['ModelProviderMappingDto'];
type ModelIdentifierDto = components['schemas']['ModelIdentifierDto'];
type ModelProviderAvailabilityDto = components['schemas']['ModelProviderAvailabilityDto'];
type CreatedModelIdentifierDto = components['schemas']['CreatedModelIdentifierDto'];
type CreateModelIdentifierDto = components['schemas']['CreateModelIdentifierDto'];
type UpdateModelIdentifierDto = components['schemas']['UpdateModelIdentifierDto'];

export type CatalogImportCounts = components['schemas']['CatalogImportCounts'];
export type ProviderCatalogImportResult = components['schemas']['ProviderCatalogImportResult'];
export type BundledModelCatalogImportResult = components['schemas']['BundledModelCatalogImportResult'];

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
    return this.client['executeContractRead'](
      '/api/Model',
      (contractClient, options) => contractClient.GET('/api/Model', options),
      config,
    );
  }

  /** Merge every provider model catalog bundled with the running Admin release. */
  async importBundledCatalog(config?: RequestConfig): Promise<BundledModelCatalogImportResult> {
    return this.client['executeContractOperation']<BundledModelCatalogImportResult>(
      '/api/Model/bundled-catalog/import',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/Model/bundled-catalog/import', options),
      config,
    );
  }

  /**
   * Get a specific model by ID
   */
  async get(id: number, config?: RequestConfig): Promise<ModelDto> {
    return this.client['executeContractRead'](
      `/api/Model/${id}`,
      (contractClient, options) => contractClient.GET('/api/Model/{id}', {
        ...options,
        params: { path: { id } },
      }),
      config,
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
    const identifiers: ModelIdentifierDto[] = await this.client['executeContractRead'](
      `/api/Model/${id}/identifiers`,
      (contractClient, options) => contractClient.GET('/api/Model/{id}/identifiers', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );

    // Normalize provider types in the response
    return identifiers.map(identifier => {
      // Provider is now a number from the API
      const normalizedProvider = identifier.provider
        ? normalizeProviderType(identifier.provider) ?? null
        : null;
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
  async getModelProviders(id: number, config?: RequestConfig): Promise<ModelProviderAvailabilityDto[]> {
    return this.client['executeContractRead'](
      `/api/Model/${id}/available-providers`,
      (contractClient, options) => contractClient.GET('/api/Model/{id}/available-providers', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }

  /**
   * Get models by provider
   */
  async getByProvider(provider: string, config?: RequestConfig): Promise<ModelDto[]> {
    return this.client['executeContractRead'](
      `/api/Model/provider/models/${encodeURIComponent(provider)}`,
      (contractClient, options) => contractClient.GET('/api/Model/provider/models/{provider}', {
        ...options,
        params: { path: { provider } },
      }),
      config,
    );
  }

  /**
   * Search for models by name
   */
  async search(query: string, config?: RequestConfig): Promise<ModelDto[]> {
    const params = new URLSearchParams({ query });
    return this.client['executeContractRead'](
      `/api/Model/search?${params.toString()}`,
      (contractClient, options) => contractClient.GET('/api/Model/search', {
        ...options,
        params: { query: { query } },
      }),
      config,
    );
  }

  /**
   * Create a new model
   */
  async create(
    data: CreateModelDto,
    config?: RequestConfig
  ): Promise<ModelDto> {
    return this.client['executeContractOperation']<ModelDto, CreateModelDto>(
      '/api/Model',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/Model', {
        ...options,
        body: data,
      }),
      config,
      data,
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
    return this.client['executeContractOperation']<ModelDto, UpdateModelDto>(
      `/api/Model/${id}`,
      HttpMethod.PUT,
      (contractClient, options) => contractClient.PUT('/api/Model/{id}', {
        ...options,
        params: { path: { id } },
        body: data,
      }),
      config,
      data,
    );
  }

  /**
   * Delete a model
   */
  async delete(id: number, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation']<void>(
      `/api/Model/${id}`,
      HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/api/Model/{id}', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }

  /**
   * Get models with their provider mapping status and details.
   * Uses identifiers already included in the model response (single API call).
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
    const models = await this.list(config);

    return models.map(model => {
      const identifiers = model.identifiers ?? [];

      const providers = identifiers.map(i => {
        const normalizedProvider = i.provider ? normalizeProviderType(i.provider) ?? null : null;
        return {
          id: i.id ?? 0,
          identifier: i.identifier ?? '',
          provider: i.provider ?? null,
          isPrimary: i.isPrimary ?? false,
          normalizedProvider: normalizedProvider ?? null,
          providerName: normalizedProvider ? getProviderTypeName(normalizedProvider) : null
        };
      });

      return {
        ...model,
        hasProviderMappings: providers.length > 0,
        providerCount: providers.length,
        providers
      };
    });
  }

  /**
   * Get models with server-side pagination, search, and filtering.
   * Returns a paginated result with total count for UI pagination.
   */
  async listPaginated(options: {
    page?: number;
    pageSize?: number;
    search?: string;
    capability?: string;
    hasProviders?: boolean;
  } = {}, config?: RequestConfig): Promise<{
    items: Array<ModelDto & {
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
    }>;
    totalCount: number;
    currentPage: number;
    pageSize: number;
    totalPages: number;
  }> {
    const params = new URLSearchParams();
    if (options.page !== undefined) params.set('page', String(options.page));
    if (options.pageSize !== undefined) params.set('pageSize', String(options.pageSize));
    if (options.search) params.set('search', options.search);
    if (options.capability) params.set('capability', options.capability);
    if (options.hasProviders !== undefined) params.set('hasProviders', String(options.hasProviders));

    const queryString = params.toString();
    const resolvedPath = queryString ? `/api/Model/paged?${queryString}` : '/api/Model/paged';

    const response = await this.client['executeContractRead'](
      resolvedPath,
      (contractClient, requestOptions) => contractClient.GET('/api/Model/paged', {
        ...requestOptions,
        params: { query: options },
      }),
      config,
    );

    // Enrich items with provider mapping status from included identifiers
    const items = (response.items ?? []).map(model => {
      const identifiers = model.identifiers ?? [];

      const providers = identifiers.map(i => {
        const normalizedProvider = i.provider ? normalizeProviderType(i.provider) ?? null : null;
        return {
          id: i.id ?? 0,
          identifier: i.identifier ?? '',
          provider: i.provider ?? null,
          isPrimary: i.isPrimary ?? false,
          normalizedProvider: normalizedProvider ?? null,
          providerName: normalizedProvider ? getProviderTypeName(normalizedProvider) : null
        };
      });

      return {
        ...model,
        hasProviderMappings: providers.length > 0,
        providerCount: providers.length,
        providers
      };
    });

    return {
      items,
      totalCount: response.totalCount ?? 0,
      currentPage: response.currentPage ?? 0,
      pageSize: response.pageSize ?? 0,
      totalPages: response.totalPages ?? 0
    };
  }

  /**
   * Get all provider mappings for a specific model
   */
  async getProviderMappings(id: number, config?: RequestConfig): Promise<ModelProviderMappingDto[]> {
    return this.client['executeContractRead'](
      `/api/Model/${id}/provider-mappings`,
      (contractClient, options) => contractClient.GET('/api/Model/{id}/provider-mappings', {
        ...options,
        params: { path: { id } },
      }),
      config,
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
    return this.client['executeContractOperation']<ModelProviderMappingDto, ModelProviderMappingDto>(
      `/api/Model/${id}/provider-mappings`,
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/Model/{id}/provider-mappings', {
        ...options,
        params: { path: { id } },
        body: mapping,
      }),
      config,
      mapping,
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
    return this.client['executeContractOperation']<void, ModelProviderMappingDto>(
      `/api/Model/${id}/provider-mappings/${mappingId}`,
      HttpMethod.PUT,
      (contractClient, options) => contractClient.PUT('/api/Model/{id}/provider-mappings/{mappingId}', {
        ...options,
        params: { path: { id, mappingId } },
        body: mapping,
      }),
      config,
      mapping,
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
    return this.client['executeContractOperation']<void>(
      `/api/Model/${id}/provider-mappings/${mappingId}`,
      HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/api/Model/{id}/provider-mappings/{mappingId}', {
        ...options,
        params: { path: { id, mappingId } },
      }),
      config,
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
      data.provider = providerTypeToOrdinal(normalized);
    }

    // Apply provider defaults
    const dataWithDefaults = applyProviderDefaults(data);
    const requestBody: CreateModelIdentifierDto = {
      ...dataWithDefaults,
      provider: typeof dataWithDefaults.provider === 'number' ? dataWithDefaults.provider : undefined,
    };

    const result = await this.client['executeContractOperation']<CreatedModelIdentifierDto, CreateModelIdentifierDto>(
      `/api/Model/${id}/identifiers`,
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/Model/{id}/identifiers', {
        ...options,
        params: { path: { id } },
        body: requestBody,
      }),
      config,
      requestBody,
    );

    // Return normalized result (provider is now a number from API)
    const normalizedProvider = result.provider
      ? normalizeProviderType(result.provider) ?? null
      : null;
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
      data.provider = providerTypeToOrdinal(normalized);
    }

    // Apply provider defaults
    const dataWithDefaults = applyProviderDefaults(data);
    const requestBody: UpdateModelIdentifierDto = {
      ...dataWithDefaults,
      provider: typeof dataWithDefaults.provider === 'number' ? dataWithDefaults.provider : undefined,
    };

    return this.client['executeContractOperation']<void, UpdateModelIdentifierDto>(
      `/api/Model/${id}/identifiers/${identifierId}`,
      HttpMethod.PUT,
      (contractClient, options) => contractClient.PUT('/api/Model/{id}/identifiers/{identifierId}', {
        ...options,
        params: { path: { id, identifierId } },
        body: requestBody,
      }),
      config,
      requestBody,
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
    return this.client['executeContractOperation']<void>(
      `/api/Model/${id}/identifiers/${identifierId}`,
      HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/api/Model/{id}/identifiers/{identifierId}', {
        ...options,
        params: { path: { id, identifierId } },
      }),
      config,
    );
  }
}
