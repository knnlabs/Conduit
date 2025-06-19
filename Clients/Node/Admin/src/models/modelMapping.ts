import { FilterOptions } from './common';

export interface ModelProviderMappingDto {
  id: number;
  modelId: string;
  providerId: string;
  providerModelId: string;
  isEnabled: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
  metadata?: string;
  
  // Model Capability Flags
  /** Whether this model supports vision/image input capabilities */
  supportsVision: boolean;
  /** Whether this model supports image generation capabilities */
  supportsImageGeneration: boolean;
  /** Whether this model supports audio transcription capabilities */
  supportsAudioTranscription: boolean;
  /** Whether this model supports text-to-speech capabilities */
  supportsTextToSpeech: boolean;
  /** Whether this model supports real-time audio streaming capabilities */
  supportsRealtimeAudio: boolean;
  
  // Extended Metadata Fields
  /** Optional model capabilities (e.g., vision, function-calling) */
  capabilities?: string;
  /** Optional maximum context length */
  maxContextLength?: number;
  /** Supported languages for transcription/TTS (comma-separated) */
  supportedLanguages?: string;
  /** Supported voices for TTS (comma-separated) */
  supportedVoices?: string;
  
  // Advanced Routing Fields
  /** Whether this mapping is the default for its capability type */
  isDefault: boolean;
  /** The capability type this mapping is default for (e.g., 'chat', 'image-generation') */
  defaultCapabilityType?: string;
}

export interface CreateModelProviderMappingDto {
  modelId: string;
  providerId: string;
  providerModelId: string;
  isEnabled?: boolean;
  priority?: number;
  metadata?: string;
  
  // Model Capability Flags
  supportsVision?: boolean;
  supportsImageGeneration?: boolean;
  supportsAudioTranscription?: boolean;
  supportsTextToSpeech?: boolean;
  supportsRealtimeAudio?: boolean;
  
  // Extended Metadata Fields
  capabilities?: string;
  maxContextLength?: number;
  supportedLanguages?: string;
  supportedVoices?: string;
  
  // Advanced Routing Fields
  isDefault?: boolean;
  defaultCapabilityType?: string;
}

export interface UpdateModelProviderMappingDto {
  providerId?: string;
  providerModelId?: string;
  isEnabled?: boolean;
  priority?: number;
  metadata?: string;
  
  // Model Capability Flags
  supportsVision?: boolean;
  supportsImageGeneration?: boolean;
  supportsAudioTranscription?: boolean;
  supportsTextToSpeech?: boolean;
  supportsRealtimeAudio?: boolean;
  
  // Extended Metadata Fields
  capabilities?: string;
  maxContextLength?: number;
  supportedLanguages?: string;
  supportedVoices?: string;
  
  // Advanced Routing Fields
  isDefault?: boolean;
  defaultCapabilityType?: string;
}

export interface ModelMappingFilters extends FilterOptions {
  modelId?: string;
  providerId?: string;
  isEnabled?: boolean;
  minPriority?: number;
  maxPriority?: number;
  
  // Capability Filters
  supportsVision?: boolean;
  supportsImageGeneration?: boolean;
  supportsAudioTranscription?: boolean;
  supportsTextToSpeech?: boolean;
  supportsRealtimeAudio?: boolean;
  isDefault?: boolean;
  defaultCapabilityType?: string;
}

export interface ModelProviderInfo {
  providerId: string;
  providerName: string;
  providerModelId: string;
  isAvailable: boolean;
  isEnabled: boolean;
  priority: number;
  estimatedCost?: {
    inputTokenCost: number;
    outputTokenCost: number;
    currency: string;
  };
}

export interface ModelRoutingInfo {
  modelId: string;
  primaryProvider?: ModelProviderInfo;
  fallbackProviders: ModelProviderInfo[];
  loadBalancingEnabled: boolean;
  routingStrategy: 'priority' | 'round-robin' | 'least-cost' | 'fastest';
}

export interface BulkMappingRequest {
  mappings: CreateModelProviderMappingDto[];
  replaceExisting?: boolean;
}

export interface BulkMappingResponse {
  created: ModelProviderMappingDto[];
  updated: ModelProviderMappingDto[];
  failed: {
    index: number;
    error: string;
    mapping: CreateModelProviderMappingDto;
  }[];
}

export interface ModelMappingSuggestion {
  modelId: string;
  suggestedProviders: {
    providerId: string;
    providerName: string;
    providerModelId: string;
    confidence: number;
    reasoning: string;
    estimatedPerformance?: {
      latency: number;
      reliability: number;
      costEfficiency: number;
    };
  }[];
}