import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  BulkMappingRequest,
  BulkMappingResponse,
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
    // Backend doesn't support pagination yet
    return this.client['get']<ModelProviderMappingDto[]>(
      ENDPOINTS.MODEL_MAPPINGS.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific model mapping by ID
   */
  async getById(id: number, config?: RequestConfig): Promise<ModelProviderMappingDto> {
    return this.client['get']<ModelProviderMappingDto>(
      ENDPOINTS.MODEL_MAPPINGS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new model mapping
   */
  async create(
    data: CreateModelProviderMappingDto,
    config?: RequestConfig
  ): Promise<ModelProviderMappingDto> {
    return this.client['post']<ModelProviderMappingDto, CreateModelProviderMappingDto>(
      ENDPOINTS.MODEL_MAPPINGS.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
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
    await this.client['put']<void, UpdateModelProviderMappingDto>(
      ENDPOINTS.MODEL_MAPPINGS.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a model mapping
   */
  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.MODEL_MAPPINGS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }




  /**
   * Bulk create model mappings
   */
  async bulkCreate(
    request: BulkMappingRequest,
    config?: RequestConfig
  ): Promise<BulkMappingResponse> {
    // Backend expects a direct array of mappings, not a request object
    const mappings = request.mappings;

    return this.client['post']<BulkMappingResponse, CreateModelProviderMappingDto[]>(
      ENDPOINTS.MODEL_MAPPINGS.BULK,
      mappings,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Bulk update model mappings
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
    return `${mapping.modelId} → ${mapping.providerId}:${mapping.providerModelId}`;
  }
}