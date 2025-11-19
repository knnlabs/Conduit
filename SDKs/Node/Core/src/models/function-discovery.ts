/**
 * Discovered function configuration for virtual key holders.
 * Provides metadata about available functions without exposing credentials.
 */
export interface FunctionDiscoveryDto {
  /**
   * The function configuration ID
   */
  id: number;

  /**
   * User-friendly name for this configuration
   */
  configurationName: string;

  /**
   * Provider type (e.g., "Exa", "Perplexity")
   */
  providerType: string;

  /**
   * Function purpose (e.g., "Search", "Answer", "RAG_Search")
   */
  purpose: string;

  /**
   * Optional description of this function configuration
   */
  description?: string;

  /**
   * Default execution mode (Synchronous or Asynchronous)
   */
  defaultExecutionMode: string;

  /**
   * Whether this function configuration is enabled
   */
  isEnabled: boolean;

  /**
   * Maximum execution timeout in seconds (null = no timeout)
   */
  timeoutSeconds?: number;
}

/**
 * Response model for function discovery list
 */
export interface FunctionDiscoveryResponse {
  /**
   * List of available function configurations
   */
  functions: FunctionDiscoveryDto[];

  /**
   * Total count of available functions
   */
  count: number;
}

/**
 * Parameter schema defining required and optional parameters
 */
export interface FunctionParameterSchema {
  /**
   * Required parameter names
   */
  required?: string[];

  /**
   * Optional parameters with type information
   */
  optional?: Record<string, ParameterDefinition>;

  /**
   * Example request demonstrating typical usage
   */
  example?: Record<string, unknown>;
}

/**
 * Definition for a single parameter
 */
export interface ParameterDefinition {
  /**
   * Parameter type (e.g., "string", "number", "boolean", "array", "select")
   */
  type: string;

  /**
   * Human-readable description
   */
  description?: string;

  /**
   * Default value
   */
  default?: unknown;

  /**
   * Minimum value (for numbers)
   */
  min?: number;

  /**
   * Maximum value (for numbers)
   */
  max?: number;

  /**
   * Available options (for select/enum types)
   */
  options?: string[];

  /**
   * Item type (for arrays)
   */
  itemType?: string;

  /**
   * Maximum number of items (for arrays)
   */
  maxItems?: number;

  /**
   * Whether this parameter is optional
   */
  optional?: boolean;
}

/**
 * Response model for function parameter schema discovery
 */
export interface FunctionParametersResponse {
  /**
   * The function configuration ID
   */
  functionConfigurationId: number;

  /**
   * User-friendly name for this configuration
   */
  configurationName: string;

  /**
   * Provider type (e.g., "Exa", "Perplexity")
   */
  providerType: string;

  /**
   * Function purpose (e.g., "Search", "Answer")
   */
  purpose: string;

  /**
   * Parameter schema defining required and optional parameters
   */
  parameterSchema?: FunctionParameterSchema | Record<string, unknown>;

  /**
   * Example request demonstrating typical usage
   */
  exampleRequest?: Record<string, unknown>;
}
