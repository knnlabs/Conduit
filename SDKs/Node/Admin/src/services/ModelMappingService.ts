import { FetchBaseApiClient } from '../client/FetchBaseApiClient';
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
  DiscoveredModel,
  CapabilityTestResult,
} from '../models/modelMapping';
import { ValidationError } from '../utils/errors';
import { z } from 'zod';

const createMappingSchema = z.object({
  modelId: z.string().min(1),
  providerId: z.number().int().positive(), // Changed from string to number to match backend
  providerModelId: z.string().min(1),
  isEnabled: z.boolean().optional(),
  priority: z.number().min(0).max(1000).optional(),
  metadata: z.string().optional(),
  // Model Capability Flags
  supportsVision: z.boolean().optional(),
  supportsImageGeneration: z.boolean().optional(),
  supportsAudioTranscription: z.boolean().optional(),
  supportsTextToSpeech: z.boolean().optional(),
  supportsRealtimeAudio: z.boolean().optional(),
  supportsFunctionCalling: z.boolean().optional(),
  supportsStreaming: z.boolean().optional(),
  supportsVideoGeneration: z.boolean().optional(),
  supportsEmbeddings: z.boolean().optional(),
  // Extended Metadata Fields
  capabilities: z.string().optional(),
  maxContextLength: z.number().optional(),
  maxOutputTokens: z.number().optional(),
  supportedLanguages: z.string().optional(),
  supportedVoices: z.string().optional(),
  supportedFormats: z.string().optional(),
  tokenizerType: z.string().optional(),
  // Advanced Routing Fields
  isDefault: z.boolean().optional(),
  defaultCapabilityType: z.string().optional(),
});

const updateMappingSchema = z.object({
  id: z.number().optional(),
  modelId: z.string().min(1).optional(),
  providerId: z.number().int().positive().optional(), // Changed from string to number to match backend
  providerModelId: z.string().min(1).optional(),
  isEnabled: z.boolean().optional(),
  priority: z.number().min(0).max(1000).optional(),
  metadata: z.string().optional(),
  // Model Capability Flags
  supportsVision: z.boolean().optional(),
  supportsImageGeneration: z.boolean().optional(),
  supportsAudioTranscription: z.boolean().optional(),
  supportsTextToSpeech: z.boolean().optional(),
  supportsRealtimeAudio: z.boolean().optional(),
  supportsFunctionCalling: z.boolean().optional(),
  supportsStreaming: z.boolean().optional(),
  supportsVideoGeneration: z.boolean().optional(),
  supportsEmbeddings: z.boolean().optional(),
  // Extended Metadata Fields
  capabilities: z.string().optional(),
  maxContextLength: z.number().optional(),
  maxOutputTokens: z.number().optional(),
  supportedLanguages: z.string().optional(),
  supportedVoices: z.string().optional(),
  supportedFormats: z.string().optional(),
  tokenizerType: z.string().optional(),
  // Advanced Routing Fields
  isDefault: z.boolean().optional(),
  defaultCapabilityType: z.string().optional(),
});

export class ModelMappingService extends FetchBaseApiClient {
  async create(request: CreateModelProviderMappingDto): Promise<ModelProviderMappingDto> {
    try {
      createMappingSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid model mapping request', { validationError: error });
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
        super.get<ModelProviderMappingDto[]>(`${ENDPOINTS.MODEL_MAPPINGS.BASE  }?modelId=${modelId}` /* BY_MODEL endpoint does not exist */),
      CACHE_TTL.MEDIUM
    );
  }

  async update(id: number, request: UpdateModelProviderMappingDto): Promise<void> {
    try {
      updateMappingSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid model mapping update request', { validationError: error });
    }

    await this.put<void>(ENDPOINTS.MODEL_MAPPINGS.BY_ID(id), request);
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

  // Bulk Operations
  async bulkCreate(request: BulkMappingRequest): Promise<BulkMappingResponse> {
    if (!request.mappings || request.mappings.length === 0) {
      throw new ValidationError('At least one mapping must be provided');
    }

    if (request.mappings.length > 100) {
      throw new ValidationError('Cannot create more than 100 mappings in a single request');
    }

    // Validate each mapping
    request.mappings.forEach((mapping, index) => {
      try {
        createMappingSchema.parse(mapping);
      } catch (error) {
        throw new ValidationError(`Invalid mapping at index ${index}`, { validationError: error, index });
      }
    });

    const response = await this.post<BulkMappingResponse>(
      ENDPOINTS.MODEL_MAPPINGS.BULK,
      request
    );

    await this.invalidateCache();
    return response;
  }

  async importMappings(): Promise<BulkMappingResponse> {
    throw new Error('IMPORT endpoint no longer exists in the API.');
  }

  async exportMappings(): Promise<Blob> {
    throw new Error('EXPORT endpoint no longer exists in the API.');
  }

  // Discovery Operations
  async discoverProviderModels(providerId: number): Promise<DiscoveredModel[]> {
    const cacheKey = this.getCacheKey('discover-provider', providerId.toString());
    return this.withCache(
      cacheKey,
      () => super.get<DiscoveredModel[]>(ENDPOINTS.MODEL_MAPPINGS.DISCOVER(providerId)),
      CACHE_TTL.SHORT
    );
  }

  async discoverModelCapabilities(): Promise<DiscoveredModel> {
    throw new Error('DISCOVER_MODEL endpoint no longer exists. Use DISCOVER with a specific provider ID instead.');
  }

  async testCapability(): Promise<CapabilityTestResult> {
    throw new Error('TEST_CAPABILITY endpoint no longer exists in the API.');
  }

  // Advanced Operations
  async getRoutingInfo(): Promise<ModelRoutingInfo> {
    throw new Error('ROUTING endpoint no longer exists in the API.');
  }

  async suggestOptimalMapping(): Promise<ModelMappingSuggestion> {
    throw new Error('SUGGEST endpoint no longer exists in the API.');
  }


  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}