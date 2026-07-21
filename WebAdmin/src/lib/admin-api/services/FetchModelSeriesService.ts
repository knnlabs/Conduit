import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

// Type aliases for better readability
type ModelSeriesDto = components['schemas']['ModelSeriesDto'];
type CreateModelSeriesDto = components['schemas']['CreateModelSeriesDto'];
type UpdateModelSeriesDto = components['schemas']['UpdateModelSeriesDto'];
type SeriesSimpleModelDto = components['schemas']['SeriesSimpleModelDto'];

/**
 * Type-safe Model Series service using the local Admin transport.
 */
export class FetchModelSeriesService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all model series
   */
  async list(config?: RequestConfig): Promise<ModelSeriesDto[]> {
    return this.client['executeContractRead'](
      '/api/ModelSeries',
      (contractClient, options) => contractClient.GET('/api/ModelSeries', options),
      config,
    );
  }

  /**
   * Get a specific model series by ID
   */
  async get(id: number, config?: RequestConfig): Promise<ModelSeriesDto> {
    return this.client['executeContractRead'](
      `/api/ModelSeries/${id}`,
      (contractClient, options) => contractClient.GET('/api/ModelSeries/{id}', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }

  /**
   * Get models in a series
   */
  async getModels(id: number, config?: RequestConfig): Promise<SeriesSimpleModelDto[]> {
    return this.client['executeContractRead'](
      `/api/ModelSeries/${id}/models`,
      (contractClient, options) => contractClient.GET('/api/ModelSeries/{id}/models', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }

  /**
   * Create a new model series
   */
  async create(
    data: CreateModelSeriesDto,
    config?: RequestConfig
  ): Promise<ModelSeriesDto> {
    return this.client['post']<ModelSeriesDto, CreateModelSeriesDto>(
      ENDPOINTS.MODEL_SERIES.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update an existing model series
   */
  async update(
    id: number,
    data: UpdateModelSeriesDto,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['put']<void, UpdateModelSeriesDto>(
      ENDPOINTS.MODEL_SERIES.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a model series
   */
  async delete(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.MODEL_SERIES.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}
