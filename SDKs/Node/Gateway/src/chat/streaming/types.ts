/**
 * Types for chat streaming functionality
 * Framework-agnostic types extracted from WebAdmin
 */

import type { CircuitState, CircuitBreakerStats } from '@knn_labs/conduit-common';
import type { ImageAttachment } from '../utils';
import type { MessageContent } from '../../models/chat';

/**
 * Performance metrics received from the Gateway API
 */
export interface StreamingPerformanceMetrics {
  tokens_per_second?: number;
  completion_tokens_per_second?: number;
  tokens_generated?: number;
  time_to_first_token_ms?: number;
  total_latency_ms?: number;
  provider?: string;
  model?: string;
}

/**
 * Usage data in OpenAI format
 */
export interface UsageData {
  prompt_tokens?: number;
  completion_tokens?: number;
  total_tokens?: number;
}

/**
 * Metrics event data
 */
export interface MetricsEventData {
  request_id?: string;
  elapsed_ms?: number;
  tokens_generated?: number;
  current_tokens_per_second?: number;
  completion_tokens_per_second?: number;
  total_latency_ms?: number;
  tokens_per_second?: number;
  provider?: string;
  model?: string;
  prompt_tokens?: number;
  completion_tokens?: number;
  total_tokens?: number;
}


/**
 * Chat completion request format
 */
export interface ChatCompletionRequest {
  messages: Array<{
    role: 'system' | 'user' | 'assistant';
    content: MessageContent;
  }>;
  model: string;
  stream?: boolean;
  temperature?: number;
  max_tokens?: number;
  top_p?: number;
  frequency_penalty?: number;
  presence_penalty?: number;
  seed?: number;
  stop?: string[];
  response_format?: {
    type: 'json_object';
  };
  [key: string]: unknown; // Allow dynamic parameters
}

/**
 * Chat completion response format
 */
export interface ChatCompletionResponse {
  id: string;
  object: string;
  created: number;
  model: string;
  choices: Array<{
    index: number;
    message: {
      role: string;
      content: string;
    };
    finish_reason: string;
  }>;
  usage?: UsageData;
}

/**
 * Chat completion chunk for streaming
 */
export interface ChatCompletionChunk {
  id: string;
  object: string;
  created: number;
  model: string;
  choices: Array<{
    index: number;
    delta: {
      role?: string;
      content?: string;
      reasoning?: string;
      channel?: string;
      [key: string]: unknown;
    };
    finish_reason?: string;
  }>;
  usage?: UsageData;
}

/**
 * Message metadata for tracking performance
 */
export interface MessageMetadata {
  tokensUsed?: number;
  tokensPerSecond?: number;
  latency?: number;
  finishReason?: string;
  provider?: string;
  model?: string;
  promptTokens?: number;
  completionTokens?: number;
  timeToFirstToken?: number;
  streaming?: boolean;
  hasReasoning?: boolean;
  reasoning?: string;
  toolCalls?: Array<{
    id: string;
    type: 'function';
    function: {
      name: string;
      arguments: string;
    };
  }>;
}

/**
 * Configuration for the streaming manager
 */
export interface StreamingConfig {
  apiEndpoint: string;
  timeoutMs?: number;
  trackPerformanceMetrics?: boolean;
  showTokensPerSecond?: boolean;
  useServerMetrics?: boolean;
  enableLogging?: boolean;
}

/**
 * Configuration for automatic retry on transient errors
 */
export interface StreamingRetryConfig {
  /** Maximum number of retry attempts (default: 3) */
  maxAttempts?: number;
  /** Base delay in milliseconds for exponential backoff (default: 1000) */
  baseDelayMs?: number;
  /** Maximum delay in milliseconds (default: 16000) */
  maxDelayMs?: number;
  /** Custom function to determine if an error should be retried */
  shouldRetry?: (error: StreamingError, attempt: number) => boolean;
}

/**
 * Information passed to the onRetrying callback
 */
export interface RetryInfo {
  /** The error that triggered the retry */
  error: StreamingError;
  /** Current attempt number (1-indexed) */
  attempt: number;
  /** Maximum number of attempts configured */
  maxAttempts: number;
  /** Delay in milliseconds before the retry will be attempted */
  delayMs: number;
}

/**
 * Configuration for circuit breaker in streaming
 */
export interface StreamingCircuitBreakerConfig {
  /** Enable circuit breaker (default: true) */
  enabled?: boolean;
  /** Number of consecutive failures to trip the circuit (default: 3) */
  failureThreshold?: number;
  /** Time window for counting failures in ms (default: 60000) */
  failureWindowMs?: number;
  /** Time to wait before half-open in ms (default: 30000) */
  resetTimeoutMs?: number;
  /** Enable logging (default: false) */
  enableLogging?: boolean;
}

/**
 * Circuit breaker state change event
 */
export interface CircuitBreakerEvent {
  /** Previous state */
  previousState: CircuitState;
  /** New state */
  newState: CircuitState;
  /** Circuit breaker statistics */
  stats: CircuitBreakerStats;
}

/**
 * Options for sending a message
 */
export interface SendMessageOptions {
  model: string;
  temperature?: number;
  maxTokens?: number;
  topP?: number;
  frequencyPenalty?: number;
  presencePenalty?: number;
  systemPrompt?: string;
  seed?: number;
  stop?: string[];
  responseFormat?: 'text' | 'json_object';
  stream?: boolean;
  dynamicParameters?: Record<string, unknown>;
}

/**
 * Options for streaming a message
 */
export interface StreamMessageOptions extends SendMessageOptions {
  stream: true; // Always true for streaming
  images?: ImageAttachment[];
  messages?: Array<{
    role: 'system' | 'user' | 'assistant';
    content: string;
    images?: ImageAttachment[];
  }>;
  functionConfigurationIds?: number[];
  /** Retry configuration for transient errors */
  retry?: StreamingRetryConfig;
  /** Circuit breaker configuration */
  circuitBreaker?: StreamingCircuitBreakerConfig;
}

/**
 * Callbacks for UI integration
 */
export interface StreamingCallbacks {
  /** Called for each chat completion chunk received */
  onChunk?: (chunk: ChatCompletionChunk) => void;
  /** Called when content delta is received (cumulative content provided) */
  onContent?: (content: string, totalContent: string) => void;
  /** Called when reasoning/thinking content is received */
  onReasoning?: (reasoning: string, totalReasoning: string) => void;
  /** Called when tool execution status updates are received */
  onToolExecuting?: (event: {
    tool_call_id?: string;
    function_name?: string;
    status: string;
    result?: unknown;
    cost?: number;
    error_message?: string;
    function_execution_id?: string;
  }) => void;
  /** Called when individual tool results are received (optional, for detailed logging) */
  onToolResult?: (event: {
    tool_call_id: string;
    result: unknown;
    error?: string;
  }) => void;
  /** Called when performance metrics are received */
  onMetrics?: (metrics: StreamingPerformanceMetrics | MetricsEventData) => void;
  /** Called when tokens per second updates are available */
  onTokensPerSecond?: (tokensPerSecond: number) => void;
  /** Called when an error occurs during streaming */
  onError?: (error: StreamingError) => void;
  /** Called when streaming completes successfully */
  onComplete?: (response: {
    content: string;
    metadata?: MessageMetadata;
  }) => void;
  /** Called when streaming starts */
  onStart?: () => void;
  /** Called when streaming is aborted */
  onAbort?: () => void;
  /** Called when a retry attempt is about to be made */
  onRetrying?: (info: RetryInfo) => void;
  /** Called when circuit breaker state changes */
  onCircuitStateChange?: (event: CircuitBreakerEvent) => void;
  /** Called when request is rejected due to open circuit */
  onCircuitOpen?: (stats: CircuitBreakerStats) => void;
}

/**
 * Error types for chat operations
 */
export type ChatErrorType = 'rate_limit' | 'model_not_found' | 'auth_error' | 'network_error' | 'server_error';

/**
 * Enhanced error type for streaming with detailed metadata
 */
export interface StreamingError extends Error {
  status?: number;
  code?: string;
  context?: string;
  retryable?: boolean;
  /** Error type for UI categorization */
  errorType?: ChatErrorType;
  /** HTTP status code */
  statusCode?: number;
  /** Seconds until retry is allowed (for rate limits) */
  retryAfter?: number;
  /** Actionable suggestions for the user */
  suggestions?: string[];
  /** Technical details for developers */
  technical?: string;
  /** Whether the error can be automatically retried */
  recoverable?: boolean;
  /** Partial content accumulated before error */
  partialContent?: string;
}

/**
 * Stream processing state
 */
export interface StreamState {
  isStreaming: boolean;
  totalContent: string;
  totalReasoning: string;
  startTime: number;
  metrics: Partial<StreamingPerformanceMetrics & UsageData>;
  abortController: AbortController | null;
}