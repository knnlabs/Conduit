import { ProviderType } from './providerType';

/**
 * Base model interface used by both Core and Admin SDKs
 */
export interface BaseModel {
  /** Unique identifier for the model */
  id: string;
  /** Human-readable name of the model */
  name: string;
  /** Provider that owns this model */
  providerId: string;
  /** Type of provider */
  providerType: ProviderType;
}

/**
 * Model feature support interface
 */
export interface ModelFeatureSupport {
  /** Whether the model supports vision/image inputs */
  supportsVision: boolean;
  /** Whether the model supports image generation */
  supportsImageGeneration: boolean;
  /** Whether the model supports audio transcription */
  supportsAudioTranscription: boolean;
  /** Whether the model supports text-to-speech */
  supportsTextToSpeech: boolean;
  /** Whether the model supports realtime audio */
  supportsRealtimeAudio: boolean;
  /** Whether the model supports function calling */
  supportsFunctionCalling: boolean;
  /** Maximum tokens the model supports */
  maxTokens?: number;
  /** Context window size */
  contextWindow?: number;
}

/**
 * Extended model information with capabilities
 */
export interface ModelWithCapabilities extends BaseModel {
  /** Model capabilities */
  capabilities: ModelFeatureSupport;
  /** Whether the model is enabled */
  isEnabled: boolean;
  /** Creation timestamp */
  createdAt: string;
  /** Last update timestamp */
  updatedAt?: string;
}

/**
 * Model usage statistics
 */
export interface ModelUsageStats {
  /** Model ID */
  modelId: string;
  /** Total requests made */
  totalRequests: number;
  /** Total tokens consumed */
  totalTokens: number;
  /** Total cost */
  totalCost: number;
  /** Average response time in ms */
  averageResponseTime: number;
  /** Success rate (0-1) */
  successRate: number;
  /** Time period for these stats */
  period: {
    start: string;
    end: string;
  };
}

/**
 * Model pricing information
 */
export interface ModelPricing {
  /** Model ID */
  modelId: string;
  /** Provider type */
  providerType: ProviderType;
  /** Input token cost (per 1K tokens) */
  inputCostPer1K: number;
  /** Output token cost (per 1K tokens) */
  outputCostPer1K: number;
  /** Currency (USD, EUR, etc.) */
  currency: string;
  /** Effective date for this pricing */
  effectiveDate: string;
}

/**
 * Model health/availability status
 */
export interface ModelHealthStatus {
  /** Model ID */
  modelId: string;
  /** Whether the model is currently available */
  isAvailable: boolean;
  /** Last successful check timestamp */
  lastChecked: string;
  /** Average response time in last check */
  responseTime?: number;
  /** Error message if not available */
  errorMessage?: string;
  /** Number of consecutive failures */
  consecutiveFailures: number;
}

/**
 * Type guard to check if object has model capabilities
 */
export function hasModelFeatureSupport(obj: unknown): obj is { capabilities: ModelFeatureSupport } {
  return typeof obj === 'object' && 
         obj !== null && 
         'capabilities' in obj &&
         typeof (obj as any).capabilities === 'object';
}

/**
 * Type guard to check if object is a BaseModel
 */
export function isBaseModel(obj: unknown): obj is BaseModel {
  return typeof obj === 'object' && 
         obj !== null && 
         'id' in obj &&
         'name' in obj &&
         'providerId' in obj &&
         'providerType' in obj;
}

/**
 * Discovered model from provider
 */
export interface DiscoveredModel {
  /** Model ID */
  id: string;
  /** Provider type */
  provider: ProviderType;
  /** Display name */
  display_name?: string;
  /** Model description */
  description?: string;
  /** Model capabilities */
  capabilities?: Record<string, boolean | number | string[]>;
  /** Additional metadata */
  metadata?: Record<string, unknown>;
}

/**
 * Model mapping between Conduit and provider models
 */
export interface ModelMapping {
  /** Unique identifier */
  id: number;
  /** Conduit model ID */
  modelId: string;
  /** Provider ID */
  providerId: string;
  /** Provider type */
  providerType: ProviderType;
  /** Provider's model ID */
  providerModelId: string;
  /** Whether mapping is enabled */
  isEnabled: boolean;
  /** Priority for routing */
  priority: number;
  /** Feature support */
  features: ModelFeatureSupport;
  /** Creation timestamp */
  createdAt: string;
  /** Update timestamp */
  updatedAt?: string;
  /** Additional metadata */
  metadata?: Record<string, unknown>;
}

/**
 * Model cost information
 */
export interface ModelCostInfo {
  /** Model pattern (exact match or prefix with *) */
  modelIdPattern: string;
  /** Cost per million input tokens */
  inputCostPerMillionTokens: number;
  /** Cost per million output tokens */
  outputCostPerMillionTokens: number;
  /** Cost per embedding token (if applicable) */
  embeddingTokenCost?: number;
  /** Cost per generated image */
  imageCostPerImage?: number;
  /** Cost per minute of audio */
  audioCostPerMinute?: number;
  /** Cost per thousand characters of audio */
  audioCostPerKCharacters?: number;
  /** Cost per minute of audio input */
  audioInputCostPerMinute?: number;
  /** Cost per minute of audio output */
  audioOutputCostPerMinute?: number;
  /** Cost per second of video */
  videoCostPerSecond?: number;
  /** Resolution multipliers for video */
  videoResolutionMultipliers?: Record<string, number>;
  /** Description */
  description?: string;
  /** Priority for pattern matching */
  priority?: number;
}