import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

// Type aliases for better readability
type CapabilitiesDto = components['schemas']['ConduitLLM.Admin.Controllers.CapabilitiesDto'];
type CreateCapabilitiesDto = components['schemas']['ConduitLLM.Admin.Controllers.CreateCapabilitiesDto'];
type UpdateCapabilitiesDto = components['schemas']['ConduitLLM.Admin.Controllers.UpdateCapabilitiesDto'];
type CapabilitiesSimpleModelDto = components['schemas']['ConduitLLM.Admin.Controllers.CapabilitiesSimpleModelDto'];

/**
 * Type-safe Model Capabilities service using native fetch
 */
export class FetchModelCapabilitiesService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all model capabilities
   */
  async list(config?: RequestConfig): Promise<CapabilitiesDto[]> {
    return this.client['get']<CapabilitiesDto[]>(
      ENDPOINTS.MODEL_CAPABILITIES.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific model capabilities by ID
   */
  async get(id: number, config?: RequestConfig): Promise<CapabilitiesDto> {
    return this.client['get']<CapabilitiesDto>(
      ENDPOINTS.MODEL_CAPABILITIES.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get models using specific capabilities
   */
  async getModels(id: number, config?: RequestConfig): Promise<CapabilitiesSimpleModelDto[]> {
    return this.client['get']<CapabilitiesSimpleModelDto[]>(
      ENDPOINTS.MODEL_CAPABILITIES.MODELS(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create new model capabilities
   */
  async create(
    data: CreateCapabilitiesDto,
    config?: RequestConfig
  ): Promise<CapabilitiesDto> {
    return this.client['post']<CapabilitiesDto, CreateCapabilitiesDto>(
      ENDPOINTS.MODEL_CAPABILITIES.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update existing model capabilities
   */
  async update(
    id: number,
    data: UpdateCapabilitiesDto,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['put']<void, UpdateCapabilitiesDto>(
      ENDPOINTS.MODEL_CAPABILITIES.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete model capabilities
   */
  async delete(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.MODEL_CAPABILITIES.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}