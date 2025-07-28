import { FilterOptions } from './common';
import { ProviderType } from './providerType';

export interface ModelProviderMappingDto {
  id: number;
  modelId: string;
  providerId: string;
  providerType: ProviderType; // Added to match backend DTO
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
  /** Whether this model supports video generation capabilities */
  supportsVideoGeneration: boolean;
  /** Whether this model supports embeddings generation */
  supportsEmbeddings: boolean;
  
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
  providerId: number; // Changed from string to number to match backend
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
  supportsVideoGeneration?: boolean;
  supportsEmbeddings?: boolean;
  
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
  /**
   * The ID of the model mapping.
   * Required by backend for validation - must match the ID in the route.
   */
  id?: number;
  
  /**
   * The model ID/alias.
   * Required by backend even for updates (not just creates).
   */
  modelId?: string;
  
  providerId?: number; // Changed from string to number to match backend
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
  supportsVideoGeneration?: boolean;
  supportsEmbeddings?: boolean;
  
  // Extended Metadata Fields
  /**
   * @deprecated Legacy field - backend should derive this from individual capability flags
   */
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
  providerType: ProviderType;
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
    providerType: ProviderType;
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
  /** The provider name */
  provider: string;
  /** The model display name */
  displayName: string;
  /** The discovered capabilities */
  capabilities: {
    chat?: boolean;
    chatStream?: boolean;
    embeddings?: boolean;
    imageGeneration?: boolean;
    vision?: boolean;
    videoGeneration?: boolean;
    videoUnderstanding?: boolean;
    functionCalling?: boolean;
    toolUse?: boolean;
    jsonMode?: boolean;
    maxTokens?: number;
    maxOutputTokens?: number;
    supportedImageSizes?: string[] | null;
    supportedVideoResolutions?: string[] | null;
    maxVideoDurationSeconds?: number | null;
  };
  /** Model metadata */
  metadata?: {
    original_model_id?: string;
    inferred?: boolean;
    [key: string]: unknown;
  };
  /** When the model was last verified */
  lastVerified?: string;
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