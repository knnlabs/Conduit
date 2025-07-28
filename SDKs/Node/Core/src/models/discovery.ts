import type { ProviderType } from '@knn_labs/conduit-common';

/**
 * Model capabilities that match the ILLMClient interface.
 */
export interface ModelCapabilities {
  chat: boolean;
  chat_stream: boolean;
  embeddings: boolean;
  image_generation: boolean;
  vision: boolean;
  video_generation: boolean;
  video_understanding: boolean;
  function_calling: boolean;
  tool_use: boolean;
  json_mode: boolean;
  max_tokens?: number;
  max_output_tokens?: number;
  supported_image_sizes?: string[];
  supported_video_resolutions?: string[];
  max_video_duration_seconds?: number;
}

/**
 * Represents a discovered model with its capabilities.
 */
export interface DiscoveredModel {
  id: string;
  provider: ProviderType;
  display_name?: string;
  capabilities: ModelCapabilities;
  metadata?: Record<string, unknown>;
  last_verified: string;
}

/**
 * Response model for getting all models.
 */
export interface ModelsDiscoveryResponse {
  data: DiscoveredModel[];
  count: number;
}

/**
 * Response model for provider-specific models.
 */
export interface ProviderModelsDiscoveryResponse {
  provider: ProviderType;
  data: DiscoveredModel[];
  count: number;
}

/**
 * Specific model capabilities to test.
 */
export enum ModelCapability {
  Chat = 'Chat',
  ChatStream = 'ChatStream',
  Embeddings = 'Embeddings',
  ImageGeneration = 'ImageGeneration',
  Vision = 'Vision',
  VideoGeneration = 'VideoGeneration',
  VideoUnderstanding = 'VideoUnderstanding',
  FunctionCalling = 'FunctionCalling',
  ToolUse = 'ToolUse',
  JsonMode = 'JsonMode'
}

/**
 * Response model for capability testing.
 */
export interface CapabilityTestResponse {
  model: string;
  capability: string;
  supported: boolean;
}

/**
 * Individual capability test within a bulk request.
 */
export interface CapabilityTest {
  model: string;
  capability: string;
}

/**
 * Request model for bulk capability testing.
 */
export interface BulkCapabilityTestRequest {
  tests: CapabilityTest[];
}

/**
 * Result of a single capability test.
 */
export interface CapabilityTestResult {
  model: string;
  capability: string;
  supported: boolean;
  error?: string;
}

/**
 * Response model for bulk capability testing.
 */
export interface BulkCapabilityTestResponse {
  results: CapabilityTestResult[];
  totalTests: number;
  successfulTests: number;
  failedTests: number;
}

/**
 * Request model for bulk model discovery.
 */
export interface BulkModelDiscoveryRequest {
  models: string[];
}

/**
 * Discovery result for a single model.
 */
export interface ModelDiscoveryResult {
  model: string;
  provider?: ProviderType;
  displayName?: string;
  capabilities: Record<string, boolean>;
  found: boolean;
  error?: string;
}

/**
 * Response model for bulk model discovery.
 */
export interface BulkModelDiscoveryResponse {
  results: ModelDiscoveryResult[];
  totalRequested: number;
  foundModels: number;
  notFoundModels: number;
}