import { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import { ProviderType } from '../models/providerType';
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
        super.get<ModelProviderMappingDto[]>(ENDPOINTS.MODEL_MAPPINGS.BY_MODEL(modelId)),
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

  async importMappings(file: File | Blob, format: 'csv' | 'json'): Promise<BulkMappingResponse> {
    if (!['csv', 'json'].includes(format)) {
      throw new ValidationError(`Unsupported format: ${format}. Supported formats: csv, json`);
    }

    const formData = new FormData();
    formData.append('file', file, `mappings.${format}`);
    formData.append('format', format);

    const response = await this.post<BulkMappingResponse>(
      ENDPOINTS.MODEL_MAPPINGS.IMPORT,
      formData
    );

    await this.invalidateCache();
    return response;
  }

  async exportMappings(format: 'csv' | 'json'): Promise<Blob> {
    if (!['csv', 'json'].includes(format)) {
      throw new ValidationError(`Unsupported format: ${format}. Supported formats: csv, json`);
    }

    const response = await this.get<Blob>(
      ENDPOINTS.MODEL_MAPPINGS.EXPORT,
      { format },
      {
        responseType: 'blob',
      }
    );

    return response;
  }

  // Discovery Operations
  async discoverProviderModels(providerType: ProviderType): Promise<DiscoveredModel[]> {
    const cacheKey = this.getCacheKey('discover-provider', providerType.toString());
    return this.withCache(
      cacheKey,
      () => super.get<DiscoveredModel[]>(ENDPOINTS.MODEL_MAPPINGS.DISCOVER_PROVIDER(providerType)),
      CACHE_TTL.SHORT
    );
  }

  async discoverModelCapabilities(providerType: ProviderType, modelId: string): Promise<DiscoveredModel> {
    if (!modelId?.trim()) {
      throw new ValidationError('Model ID is required');
    }

    const cacheKey = this.getCacheKey('discover-model', providerType.toString(), modelId);
    return this.withCache(
      cacheKey,
      () => super.get<DiscoveredModel>(ENDPOINTS.MODEL_MAPPINGS.DISCOVER_MODEL(providerType, modelId)),
      CACHE_TTL.MEDIUM
    );
  }

  async testCapability(modelAlias: string, capability: string): Promise<CapabilityTestResult> {
    if (!modelAlias?.trim()) {
      throw new ValidationError('Model alias is required');
    }
    if (!capability?.trim()) {
      throw new ValidationError('Capability is required');
    }

    const cacheKey = this.getCacheKey('test-capability', modelAlias, capability);
    return this.withCache(
      cacheKey,
      () => super.get<CapabilityTestResult>(ENDPOINTS.MODEL_MAPPINGS.TEST_CAPABILITY(modelAlias, capability)),
      CACHE_TTL.SHORT
    );
  }

  // Advanced Operations
  async getRoutingInfo(modelId: string): Promise<ModelRoutingInfo> {
    if (!modelId?.trim()) {
      throw new ValidationError('Model ID is required');
    }

    const cacheKey = this.getCacheKey('model-routing-info', modelId);
    return this.withCache(
      cacheKey,
      () => super.get<ModelRoutingInfo>(ENDPOINTS.MODEL_MAPPINGS.ROUTING(modelId)),
      CACHE_TTL.MEDIUM
    );
  }

  async suggestOptimalMapping(modelId: string): Promise<ModelMappingSuggestion> {
    if (!modelId?.trim()) {
      throw new ValidationError('Model ID is required');
    }

    return super.post<ModelMappingSuggestion>(
      ENDPOINTS.MODEL_MAPPINGS.SUGGEST,
      { modelId }
    );
  }

  /**
   * Discover all available models across all configured providers
   * @returns Array of discovered models with their capabilities
   */
  async discoverModels(): Promise<DiscoveredModel[]> {
    const cacheKey = 'discover-all-models';
    return this.withCache(
      cacheKey,
      () => super.get<DiscoveredModel[]>(ENDPOINTS.MODEL_MAPPINGS.DISCOVER_ALL),
      CACHE_TTL.SHORT
    );
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}