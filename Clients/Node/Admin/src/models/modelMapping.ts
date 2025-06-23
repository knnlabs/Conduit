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
  /** Whether this model supports function calling */
  supportsFunctionCalling: boolean;
  /** Whether this model supports streaming responses */
  supportsStreaming: boolean;
  
  // Extended Metadata Fields
  /** Optional model capabilities (e.g., vision, function-calling) */
  capabilities?: string;
  /** Optional maximum context length */
  maxContextLength?: number;
  /** The maximum output tokens for this model */
  maxOutputTokens?: number;
  /** Supported languages for transcription/TTS (comma-separated) */
  supportedLanguages?: string;
  /** Supported voices for TTS (comma-separated) */
  supportedVoices?: string;
  /** Supported input formats (comma-separated) */
  supportedFormats?: string;
  /** The tokenizer type used by this model */
  tokenizerType?: string;
  
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
  supportsFunctionCalling?: boolean;
  supportsStreaming?: boolean;
  
  // Extended Metadata Fields
  capabilities?: string;
  maxContextLength?: number;
  maxOutputTokens?: number;
  supportedLanguages?: string;
  supportedVoices?: string;
  supportedFormats?: string;
  tokenizerType?: string;
  
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
  supportsFunctionCalling?: boolean;
  supportsStreaming?: boolean;
  
  // Extended Metadata Fields
  capabilities?: string;
  maxContextLength?: number;
  maxOutputTokens?: number;
  supportedLanguages?: string;
  supportedVoices?: string;
  supportedFormats?: string;
  tokenizerType?: string;
  
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
  supportsFunctionCalling?: boolean;
  supportsStreaming?: boolean;
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

/** Represents a discovered model from a provider */
export interface DiscoveredModel {
  /** The model ID */
  modelId: string;
  /** The provider-specific model ID */
  providerModelId: string;
  /** The model display name */
  displayName: string;
  /** The model description */
  description?: string;
  /** The discovered capabilities */
  capabilities?: ModelCapabilities;
  /** The confidence score for this discovery (0-1) */
  confidence: number;
  /** Whether this model is recommended for mapping */
  isRecommended: boolean;
}

/** Represents model capabilities discovered during model discovery */
export interface ModelCapabilities {
  /** Whether the model supports chat completions */
  supportsChat: boolean;
  /** Whether the model supports vision/image input */
  supportsVision: boolean;
  /** Whether the model supports image generation */
  supportsImageGeneration: boolean;
  /** Whether the model supports audio transcription */
  supportsAudioTranscription: boolean;
  /** Whether the model supports text-to-speech */
  supportsTextToSpeech: boolean;
  /** Whether the model supports real-time audio streaming */
  supportsRealtimeAudio: boolean;
  /** Whether the model supports function calling */
  supportsFunctionCalling: boolean;
  /** Whether the model supports streaming responses */
  supportsStreaming: boolean;
  /** The maximum context length */
  maxContextLength?: number;
  /** The maximum output tokens */
  maxOutputTokens?: number;
  /** Supported languages (comma-separated) */
  supportedLanguages?: string;
  /** Supported voices for TTS (comma-separated) */
  supportedVoices?: string;
  /** Supported input formats (comma-separated) */
  supportedFormats?: string;
  /** The tokenizer type */
  tokenizerType?: string;
}

/** Represents the result of a capability test for a specific model */
export interface CapabilityTestResult {
  /** The model alias that was tested */
  modelAlias: string;
  /** The capability that was tested */
  capability: string;
  /** Whether the capability test was successful */
  isSupported: boolean;
  /** The confidence score of the test result (0-1) */
  confidence: number;
  /** Additional details about the test result */
  details?: string;
  /** Any error that occurred during testing */
  error?: string;
  /** The test duration in milliseconds */
  testDurationMs: number;
  /** When the test was performed */
  testedAt: string;
}