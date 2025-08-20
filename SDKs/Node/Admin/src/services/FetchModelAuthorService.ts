import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

// Type aliases for better readability
type ModelAuthorDto = components['schemas']['ConduitLLM.Admin.Controllers.ModelAuthorDto'];
type CreateModelAuthorDto = components['schemas']['ConduitLLM.Admin.Controllers.CreateModelAuthorDto'];
type UpdateModelAuthorDto = components['schemas']['ConduitLLM.Admin.Controllers.UpdateModelAuthorDto'];
type SimpleModelSeriesDto = components['schemas']['ConduitLLM.Admin.Controllers.SimpleModelSeriesDto'];

/**
 * Type-safe Model Author service using native fetch
 */
export class FetchModelAuthorService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all model authors
   */
  async list(config?: RequestConfig): Promise<ModelAuthorDto[]> {
    return this.client['get']<ModelAuthorDto[]>(
      ENDPOINTS.MODEL_AUTHORS.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific model author by ID
   */
  async get(id: number, config?: RequestConfig): Promise<ModelAuthorDto> {
    return this.client['get']<ModelAuthorDto>(
      ENDPOINTS.MODEL_AUTHORS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get series by author
   */
  async getSeries(id: number, config?: RequestConfig): Promise<SimpleModelSeriesDto[]> {
    return this.client['get']<SimpleModelSeriesDto[]>(
      ENDPOINTS.MODEL_AUTHORS.SERIES(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new model author
   */
  async create(
    data: CreateModelAuthorDto,
    config?: RequestConfig
  ): Promise<ModelAuthorDto> {
    return this.client['post']<ModelAuthorDto, CreateModelAuthorDto>(
      ENDPOINTS.MODEL_AUTHORS.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update an existing model author
   */
  async update(
    id: number,
    data: UpdateModelAuthorDto,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['put']<void, UpdateModelAuthorDto>(
      ENDPOINTS.MODEL_AUTHORS.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a model author
   */
  async delete(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.MODEL_AUTHORS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}