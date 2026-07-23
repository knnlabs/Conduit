import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';
import type {
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  BulkMappingRequest,
  BulkMappingResponse,
  BulkModelMappingPreviewRequest,
  BulkModelMappingPreviewResponse,
  BulkDeleteResult,
  BulkUpdateResult,
} from '../models/modelMapping';


/**
 * Type-safe Model Mappings service using native fetch
 */
export class FetchModelMappingsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all model mappings
   * Note: The backend currently returns a plain array, not a paginated response
   */
  async list(
    config?: RequestConfig
  ): Promise<ModelProviderMappingDto[]> {
    return this.client['executeContractRead'](
      '/api/ModelProviderMapping',
      (contractClient, options) => contractClient.GET('/api/ModelProviderMapping', options),
      config,
    );
  }

  /**
   * Get a specific model mapping by ID
   */
  async getById(id: number, config?: RequestConfig): Promise<ModelProviderMappingDto> {
    return this.client['executeContractRead'](
      `/api/ModelProviderMapping/${id}`,
      (contractClient, options) => contractClient.GET('/api/ModelProviderMapping/{id}', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }

  /**
   * Create a new model mapping
   */
  async create(
    data: CreateModelProviderMappingDto,
    config?: RequestConfig
  ): Promise<ModelProviderMappingDto> {
    return this.client['executeContractOperation']<ModelProviderMappingDto, CreateModelProviderMappingDto>(
      '/api/ModelProviderMapping',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelProviderMapping', {
        ...options,
        body: data,
      }),
      config,
      data,
    );
  }

  /**
   * Update an existing model mapping
   */
  async update(
    id: number,
    data: UpdateModelProviderMappingDto,
    config?: RequestConfig
  ): Promise<void> {
    await this.client['executeContractOperation']<void, UpdateModelProviderMappingDto>(
      `/api/ModelProviderMapping/${id}`,
      HttpMethod.PUT,
      (contractClient, options) => contractClient.PUT('/api/ModelProviderMapping/{id}', {
        ...options,
        params: { path: { id } },
        body: data,
      }),
      config,
      data,
    );
  }

  /**
   * Delete a model mapping
   */
  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation']<void>(
      `/api/ModelProviderMapping/${id}`,
      HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/api/ModelProviderMapping/{id}', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }




  /**
   * Resolve associations and conflicts for discovered provider models.
   */
  async previewBulk(
    request: BulkModelMappingPreviewRequest,
    config?: RequestConfig
  ): Promise<BulkModelMappingPreviewResponse> {
    return this.client['executeContractOperation']<BulkModelMappingPreviewResponse, BulkModelMappingPreviewRequest>(
      '/api/ModelProviderMapping/bulk/preview',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelProviderMapping/bulk/preview', {
        ...options,
        body: request,
      }),
      config,
      request,
    );
  }

  /**
   * Bulk create model mappings
   */
  async bulkCreate(
    request: BulkMappingRequest,
    config?: RequestConfig
  ): Promise<BulkMappingResponse> {
    return this.client['executeContractOperation']<BulkMappingResponse, BulkMappingRequest>(
      '/api/ModelProviderMapping/bulk',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelProviderMapping/bulk', {
        ...options,
        body: request,
      }),
      config,
      request,
    );
  }

  /**
   * Bulk delete model mappings
   */
  async bulkDelete(
    ids: number[],
    config?: RequestConfig
  ): Promise<BulkDeleteResult> {
    return this.client['executeContractOperation']<BulkDeleteResult, number[]>(
      '/api/ModelProviderMapping/bulk/delete',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelProviderMapping/bulk/delete', {
        ...options,
        body: ids,
      }),
      config,
      ids,
    );
  }

  /**
   * Bulk enable model mappings
   */
  async bulkEnable(
    ids: number[],
    config?: RequestConfig
  ): Promise<BulkUpdateResult> {
    return this.client['executeContractOperation']<BulkUpdateResult, number[]>(
      '/api/ModelProviderMapping/bulk/enable',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelProviderMapping/bulk/enable', {
        ...options,
        body: ids,
      }),
      config,
      ids,
    );
  }

  /**
   * Bulk disable model mappings
   */
  async bulkDisable(
    ids: number[],
    config?: RequestConfig
  ): Promise<BulkUpdateResult> {
    return this.client['executeContractOperation']<BulkUpdateResult, number[]>(
      '/api/ModelProviderMapping/bulk/disable',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelProviderMapping/bulk/disable', {
        ...options,
        body: ids,
      }),
      config,
      ids,
    );
  }

  /**
   * Bulk update model mappings (legacy method for individual updates)
   */
  async bulkUpdate(
    updates: { id: number; data: UpdateModelProviderMappingDto }[],
    config?: RequestConfig
  ): Promise<void> {
    // This would need a specific endpoint - using individual updates for now
    await Promise.all(
      updates.map(({ id, data }) => this.update(id, data, config))
    );
  }

  /**
   * Helper method to check if a mapping is enabled
   */
  isMappingEnabled(mapping: ModelProviderMappingDto): boolean {
    return mapping.isEnabled === true;
  }

  /**
   * Helper method to format mapping display name
   */
  formatMappingName(mapping: ModelProviderMappingDto): string {
    return `${mapping.modelAlias} → ${mapping.providerId}:${mapping.providerModelId}`;
  }
}
