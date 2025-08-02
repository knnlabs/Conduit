import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  DiscoveredModel,
  BulkMappingRequest,
  BulkMappingResponse,
} from '../models/modelMapping';


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
   * Discover models from a specific provider
   */
  async discoverProviderModels(
    providerId: number,
    config?: RequestConfig
  ): Promise<DiscoveredModel[]> {
    return this.client['get']<DiscoveredModel[]>(
      ENDPOINTS.MODEL_MAPPINGS.DISCOVER(providerId),
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
    request: BulkMappingRequest,
    config?: RequestConfig
  ): Promise<BulkMappingResponse> {
    const apiRequest = {
      mappings: request.mappings,
      replaceExisting: request.replaceExisting ?? false,
      validateProviderModels: true,
    };

    return this.client['post']<BulkMappingResponse, typeof apiRequest>(
      ENDPOINTS.MODEL_MAPPINGS.BULK,
      apiRequest,
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