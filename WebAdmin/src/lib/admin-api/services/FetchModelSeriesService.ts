import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';

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
    return this.client['executeContractOperation']<ModelSeriesDto, CreateModelSeriesDto>(
      '/api/ModelSeries',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelSeries', {
        ...options,
        body: data,
      }),
      config,
      data,
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
    return this.client['executeContractOperation']<void, UpdateModelSeriesDto>(
      `/api/ModelSeries/${id}`,
      HttpMethod.PUT,
      (contractClient, options) => contractClient.PUT('/api/ModelSeries/{id}', {
        ...options,
        params: { path: { id } },
        body: data,
      }),
      config,
      data,
    );
  }

  /**
   * Delete a model series
   */
  async delete(id: number, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation']<void>(
      `/api/ModelSeries/${id}`,
      HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/api/ModelSeries/{id}', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }
}
