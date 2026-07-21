import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';

// Type aliases for better readability
type ModelAuthorDto = components['schemas']['ModelAuthorDto'];
type CreateModelAuthorDto = components['schemas']['CreateModelAuthorDto'];
type UpdateModelAuthorDto = components['schemas']['UpdateModelAuthorDto'];
type SimpleModelSeriesDto = components['schemas']['SimpleModelSeriesDto'];

/**
 * Type-safe Model Author service using the local Admin transport.
 */
export class FetchModelAuthorService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all model authors
   */
  async list(config?: RequestConfig): Promise<ModelAuthorDto[]> {
    return this.client['executeContractRead'](
      '/api/ModelAuthor',
      (contractClient, options) => contractClient.GET('/api/ModelAuthor', options),
      config,
    );
  }

  /**
   * Get a specific model author by ID
   */
  async get(id: number, config?: RequestConfig): Promise<ModelAuthorDto> {
    return this.client['executeContractRead'](
      `/api/ModelAuthor/${id}`,
      (contractClient, options) => contractClient.GET('/api/ModelAuthor/{id}', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }

  /**
   * Get series by author
   */
  async getSeries(id: number, config?: RequestConfig): Promise<SimpleModelSeriesDto[]> {
    return this.client['executeContractRead'](
      `/api/ModelAuthor/${id}/series`,
      (contractClient, options) => contractClient.GET('/api/ModelAuthor/{id}/series', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }

  // TODO: Add getModelCount(id: number) method for efficient model counting
  // Currently requires multiple API calls: getSeries() then modelSeries.getModels() for each series
  // This should be a single backend endpoint that returns the total model count for an author

  /**
   * Create a new model author
   */
  async create(
    data: CreateModelAuthorDto,
    config?: RequestConfig
  ): Promise<ModelAuthorDto> {
    return this.client['executeContractOperation']<ModelAuthorDto, CreateModelAuthorDto>(
      '/api/ModelAuthor',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ModelAuthor', {
        ...options,
        body: data,
      }),
      config,
      data,
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
    return this.client['executeContractOperation']<void, UpdateModelAuthorDto>(
      `/api/ModelAuthor/${id}`,
      HttpMethod.PUT,
      (contractClient, options) => contractClient.PUT('/api/ModelAuthor/{id}', {
        ...options,
        params: { path: { id } },
        body: data,
      }),
      config,
      data,
    );
  }

  /**
   * Delete a model author
   */
  async delete(id: number, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation']<void>(
      `/api/ModelAuthor/${id}`,
      HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/api/ModelAuthor/{id}', {
        ...options,
        params: { path: { id } },
      }),
      config,
    );
  }
}
