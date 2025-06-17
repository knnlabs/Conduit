import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import {
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  ModelMappingFilters,
  ModelRoutingInfo,
  BulkMappingRequest,
  BulkMappingResponse,
  ModelMappingSuggestion,
} from '../models/modelMapping';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

const createMappingSchema = z.object({
  modelId: z.string().min(1),
  providerId: z.string().min(1),
  providerModelId: z.string().min(1),
  isEnabled: z.boolean().optional(),
  priority: z.number().min(0).max(100).optional(),
  metadata: z.string().optional(),
});

const updateMappingSchema = z.object({
  providerId: z.string().min(1).optional(),
  providerModelId: z.string().min(1).optional(),
  isEnabled: z.boolean().optional(),
  priority: z.number().min(0).max(100).optional(),
  metadata: z.string().optional(),
});

export class ModelMappingService extends BaseApiClient {
  async create(request: CreateModelProviderMappingDto): Promise<ModelProviderMappingDto> {
    try {
      createMappingSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid model mapping request', error);
    }

    const response = await this.post<ModelProviderMappingDto>(
      ENDPOINTS.MODEL_MAPPINGS.BASE,
      request
    );

    await this.invalidateCache();
    return response;
  }

  async list(filters?: ModelMappingFilters): Promise<ModelProviderMappingDto[]> {
    const params = filters
      ? {
          modelId: filters.modelId,
          providerId: filters.providerId,
          isEnabled: filters.isEnabled,
          minPriority: filters.minPriority,
          maxPriority: filters.maxPriority,
          sortBy: filters.sortBy?.field,
          sortDirection: filters.sortBy?.direction,
        }
      : undefined;

    const cacheKey = this.getCacheKey('model-mappings', params);
    return this.withCache(
      cacheKey,
      () => super.get<ModelProviderMappingDto[]>(ENDPOINTS.MODEL_MAPPINGS.BASE, params),
      CACHE_TTL.MEDIUM
    );
  }

  async getById(id: number): Promise<ModelProviderMappingDto> {
    const cacheKey = this.getCacheKey('model-mapping', id);
    return this.withCache(
      cacheKey,
      () => super.get<ModelProviderMappingDto>(ENDPOINTS.MODEL_MAPPINGS.BY_ID(id)),
      CACHE_TTL.MEDIUM
    );
  }

  async getByModel(modelId: string): Promise<ModelProviderMappingDto[]> {
    const cacheKey = this.getCacheKey('model-mapping-by-model', modelId);
    return this.withCache(
      cacheKey,
      () =>
        super.get<ModelProviderMappingDto[]>(ENDPOINTS.MODEL_MAPPINGS.BY_MODEL(modelId)),
      CACHE_TTL.MEDIUM
    );
  }

  async update(id: number, request: UpdateModelProviderMappingDto): Promise<void> {
    try {
      updateMappingSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid model mapping update request', error);
    }

    await this.put(ENDPOINTS.MODEL_MAPPINGS.BY_ID(id), request);
    await this.invalidateCache();
  }

  async deleteById(id: number): Promise<void> {
    await super.delete(ENDPOINTS.MODEL_MAPPINGS.BY_ID(id));
    await this.invalidateCache();
  }

  async getAvailableProviders(): Promise<string[]> {
    const cacheKey = 'available-providers';
    return this.withCache(
      cacheKey,
      () => super.get<string[]>(ENDPOINTS.MODEL_MAPPINGS.PROVIDERS),
      CACHE_TTL.LONG
    );
  }

  async updatePriority(id: number, priority: number): Promise<void> {
    await this.update(id, { priority });
  }

  async enableMapping(id: number): Promise<void> {
    await this.update(id, { isEnabled: true });
  }

  async disableMapping(id: number): Promise<void> {
    await this.update(id, { isEnabled: false });
  }

  async reorderMappings(_modelId: string, mappingIds: number[]): Promise<void> {
    const updates = mappingIds.map((id, index) => ({
      id,
      priority: mappingIds.length - index,
    }));

    await Promise.all(
      updates.map((update) =>
        this.updatePriority(update.id, update.priority)
      )
    );
  }

  // Stub methods
  async getRoutingInfo(_modelId: string): Promise<ModelRoutingInfo> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getRoutingInfo requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/modelprovidermapping/routing/{modelId}'
    );
  }

  async bulkCreate(_request: BulkMappingRequest): Promise<BulkMappingResponse> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'bulkCreate requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/modelprovidermapping/bulk'
    );
  }

  async importMappings(_file: File | Blob, _format: 'csv' | 'json'): Promise<BulkMappingResponse> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'importMappings requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/modelprovidermapping/import'
    );
  }

  async exportMappings(_format: 'csv' | 'json'): Promise<Blob> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'exportMappings requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/modelprovidermapping/export'
    );
  }

  async suggestOptimalMapping(_modelId: string): Promise<ModelMappingSuggestion> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'suggestOptimalMapping requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/modelprovidermapping/suggest'
    );
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}