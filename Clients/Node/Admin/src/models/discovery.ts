/**
 * Models for discovery API operations
 */

// Base types
export interface CapabilityTest {
  /** The model name to test */
  model: string;
  /** The capability to test (e.g., "Chat", "ImageGeneration", "Vision") */
  capability: string;
}

export interface CapabilityTestResult {
  /** The model name that was tested */
  model: string;
  /** The capability that was tested */
  capability: string;
  /** Whether the model supports this capability */
  supported: boolean;
  /** Additional metadata about the capability */
  metadata?: Record<string, any>;
  /** Error message if the test failed */
  error?: string;
}

export interface ModelDiscoveryResult {
  /** The model name */
  model: string;
  /** Whether the model was found */
  found: boolean;
  /** The provider that hosts this model */
  provider?: string;
  /** Available capabilities for this model */
  capabilities?: string[];
  /** Model metadata */
  metadata?: Record<string, any>;
  /** Error message if discovery failed */
  error?: string;
}

// Request types
export interface BulkCapabilityTestRequest {
  /** Array of model-capability pairs to test */
  tests: CapabilityTest[];
  /** Optional virtual key for authentication */
  virtualKey?: string;
}

export interface BulkModelDiscoveryRequest {
  /** Array of model names to discover */
  models: string[];
  /** Whether to include capability information in results */
  includeCapabilities?: boolean;
  /** Filter results to only include models with these capabilities */
  filterByCapabilities?: string[];
  /** Optional virtual key for authentication */
  virtualKey?: string;
}

export interface ModelDiscoveryRequest {
  /** Array of model names to discover */
  models: string[];
  /** Required capabilities that discovered models must have */
  requiredCapabilities?: string[];
  /** Optional virtual key for authentication */
  virtualKey?: string;
}

// Response types
export interface BulkCapabilityTestResponse {
  /** Array of capability test results */
  results: CapabilityTestResult[];
  /** Total number of tests performed */
  totalTests: number;
  /** Number of successful tests */
  successfulTests: number;
  /** Number of failed tests */
  failedTests: number;
  /** Timestamp when the tests were performed */
  timestamp: string;
  /** Execution time in milliseconds */
  executionTimeMs: number;
}

export interface BulkModelDiscoveryResponse {
  /** Array of model discovery results */
  results: ModelDiscoveryResult[];
  /** Total number of models requested */
  totalRequested: number;
  /** Number of models found */
  foundModels: number;
  /** Number of models not found */
  notFoundModels: number;
  /** Timestamp when discovery was performed */
  timestamp: string;
  /** Execution time in milliseconds */
  executionTimeMs: number;
}

export interface CapabilityTestResponse {
  /** The model name that was tested */
  model: string;
  /** The capability that was tested */
  capability: string;
  /** Whether the model supports this capability */
  supported: boolean;
  /** Additional metadata about the capability */
  metadata?: Record<string, any>;
  /** Timestamp when the test was performed */
  timestamp: string;
}

// Single endpoint response types
export interface DiscoveryModel {
  /** The model name */
  name: string;
  /** The provider that hosts this model */
  provider: string;
  /** Available capabilities */
  capabilities: string[];
  /** Model metadata */
  metadata?: Record<string, any>;
  /** Whether the model is currently available */
  available: boolean;
}

export interface DiscoveryModelsResponse {
  /** Array of all discovered models */
  models: DiscoveryModel[];
  /** Total number of models */
  totalModels: number;
  /** Number of available models */
  availableModels: number;
  /** Timestamp when discovery was performed */
  timestamp: string;
}

export interface DiscoveryProviderModelsResponse {
  /** The provider name */
  provider: string;
  /** Array of models from this provider */
  models: DiscoveryModel[];
  /** Total number of models for this provider */
  totalModels: number;
  /** Number of available models for this provider */
  availableModels: number;
  /** Whether the provider is currently available */
  providerAvailable: boolean;
  /** Timestamp when discovery was performed */
  timestamp: string;
}

// Common capability types for discovery (using different naming than utils to avoid conflicts)
export type DiscoveryCapability = 
  | 'Chat'
  | 'ImageGeneration' 
  | 'Vision'
  | 'AudioTranscription'
  | 'TextToSpeech'
  | 'Embeddings'
  | 'CodeGeneration'
  | 'FunctionCalling'
  | 'Streaming';

// Utility types
export interface DiscoveryStats {
  /** Total number of models available */
  totalModels: number;
  /** Number of providers available */
  totalProviders: number;
  /** Models grouped by capability */
  modelsByCapability: Record<string, string[]>;
  /** Providers grouped by capability */
  providersByCapability: Record<string, string[]>;
  /** Cache statistics */
  cacheStats?: {
    hitRate: number;
    totalRequests: number;
    cacheSize: number;
  };
}

export interface DiscoveryError {
  /** Error code */
  code: string;
  /** Error message */
  message: string;
  /** Additional error details */
  details?: Record<string, any>;
  /** Timestamp when error occurred */
  timestamp: string;
}