import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

// Type aliases for better readability
type ModelSeriesDto = components['schemas']['ConduitLLM.Admin.Models.ModelSeries.ModelSeriesDto'];
type CreateModelSeriesDto = components['schemas']['ConduitLLM.Admin.Models.ModelSeries.CreateModelSeriesDto'];
type UpdateModelSeriesDto = components['schemas']['ConduitLLM.Admin.Models.ModelSeries.UpdateModelSeriesDto'];
type SeriesSimpleModelDto = components['schemas']['ConduitLLM.Admin.Models.ModelSeries.SeriesSimpleModelDto'];

/**
 * Type-safe Model Series service using native fetch
 */
export class FetchModelSeriesService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all model series
   */
  async list(config?: RequestConfig): Promise<ModelSeriesDto[]> {
    return this.client['get']<ModelSeriesDto[]>(
      ENDPOINTS.MODEL_SERIES.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific model series by ID
   */
  async get(id: number, config?: RequestConfig): Promise<ModelSeriesDto> {
    return this.client['get']<ModelSeriesDto>(
      ENDPOINTS.MODEL_SERIES.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get models in a series
   */
  async getModels(id: number, config?: RequestConfig): Promise<SeriesSimpleModelDto[]> {
    return this.client['get']<SeriesSimpleModelDto[]>(
      ENDPOINTS.MODEL_SERIES.MODELS(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
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