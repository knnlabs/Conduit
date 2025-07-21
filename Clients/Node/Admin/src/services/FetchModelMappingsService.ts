import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  DiscoveredModel,
  CapabilityTestResult,
  ModelMappingSuggestion,
  ModelRoutingInfo,
  BulkMappingResponse,
} from '../models/modelMapping';

// Type aliases for better readability - using generated types where available
type BulkModelMappingRequest = components['schemas']['BulkModelMappingRequest'];

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
   * Discover all available models from all providers
   */
  async discoverModels(config?: RequestConfig): Promise<DiscoveredModel[]> {
    return this.client['get']<DiscoveredModel[]>(
      ENDPOINTS.MODEL_MAPPINGS.DISCOVER_ALL,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Discover models from a specific provider
   */
  async discoverProviderModels(
    providerName: string,
    config?: RequestConfig
  ): Promise<DiscoveredModel[]> {
    return this.client['get']<DiscoveredModel[]>(
      ENDPOINTS.MODEL_MAPPINGS.DISCOVER_PROVIDER(providerName),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Test a specific capability for a model mapping
   */
  async testCapability(
    id: number,
    capability: string,
    testParams?: Record<string, unknown>,
    config?: RequestConfig
  ): Promise<CapabilityTestResult> {
    // Use the model alias endpoint instead of ID-based endpoint
    const mapping = await this.getById(id, config);
    return this.client['post']<CapabilityTestResult>(
      ENDPOINTS.MODEL_MAPPINGS.TEST_CAPABILITY(mapping.modelId, capability),
      testParams,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get routing information for a model
   */
  async getRouting(modelId: string, config?: RequestConfig): Promise<ModelRoutingInfo> {
    return this.client['get']<ModelRoutingInfo>(
      ENDPOINTS.MODEL_MAPPINGS.ROUTING(modelId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get model mapping suggestions
   */
  async getSuggestions(config?: RequestConfig): Promise<ModelMappingSuggestion[]> {
    return this.client['get']<ModelMappingSuggestion[]>(
      ENDPOINTS.MODEL_MAPPINGS.SUGGEST,
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
    mappings: CreateModelProviderMappingDto[],
    replaceExisting: boolean = false,
    config?: RequestConfig
  ): Promise<BulkMappingResponse> {
    const request: BulkModelMappingRequest = {
      mappings: mappings as unknown as BulkModelMappingRequest['mappings'], // Type compatibility
      replaceExisting,
      validateProviderModels: true,
    };

    return this.client['post']<BulkMappingResponse, BulkModelMappingRequest>(
      ENDPOINTS.MODEL_MAPPINGS.BULK,
      request,
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
   * Helper method to get mapping capabilities
   */
  getMappingCapabilities(mapping: ModelProviderMappingDto): string[] {
    const capabilities: string[] = [];
    
    if (mapping.supportsVision) capabilities.push('vision');
    if (mapping.supportsImageGeneration) capabilities.push('image-generation');
    if (mapping.supportsAudioTranscription) capabilities.push('audio-transcription');
    if (mapping.supportsTextToSpeech) capabilities.push('text-to-speech');
    if (mapping.supportsRealtimeAudio) capabilities.push('realtime-audio');
    if (mapping.supportsFunctionCalling) capabilities.push('function-calling');
    if (mapping.supportsStreaming) capabilities.push('streaming');
    if (mapping.supportsVideoGeneration) capabilities.push('video-generation');
    if (mapping.supportsEmbeddings) capabilities.push('embeddings');
    
    return capabilities;
  }

  /**
   * Helper method to format mapping display name
   */
  formatMappingName(mapping: ModelProviderMappingDto): string {
    return `${mapping.modelId} → ${mapping.providerId}:${mapping.providerModelId}`;
  }

  /**
   * Helper method to check if a model supports a specific capability
   */
  supportsCapability(mapping: ModelProviderMappingDto, capability: string): boolean {
    switch (capability) {
      case 'vision':
        return mapping.supportsVision;
      case 'image-generation':
        return mapping.supportsImageGeneration;
      case 'audio-transcription':
        return mapping.supportsAudioTranscription;
      case 'text-to-speech':
        return mapping.supportsTextToSpeech;
      case 'realtime-audio':
        return mapping.supportsRealtimeAudio;
      case 'function-calling':
        return mapping.supportsFunctionCalling;
      case 'streaming':
        return mapping.supportsStreaming;
      case 'video-generation':
        return mapping.supportsVideoGeneration;
      case 'embeddings':
        return mapping.supportsEmbeddings;
      default:
        return false;
    }
  }
}