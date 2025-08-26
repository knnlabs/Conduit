/**
 * Types for chat streaming functionality
 * Framework-agnostic types extracted from WebUI
 */

import type { ImageAttachment } from '../utils';
import type { MessageContent } from '../../models/chat';

/**
 * Performance metrics received from the Core API
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
}

/**
 * Callbacks for UI integration
 */
export interface StreamingCallbacks {
  onChunk?: (chunk: ChatCompletionChunk) => void;
  onContent?: (content: string, totalContent: string) => void;
  onMetrics?: (metrics: StreamingPerformanceMetrics | MetricsEventData) => void;
  onTokensPerSecond?: (tokensPerSecond: number) => void;
  onError?: (error: StreamingError) => void;
  onComplete?: (response: {
    content: string;
    metadata?: MessageMetadata;
  }) => void;
  onStart?: () => void;
  onAbort?: () => void;
}

/**
 * Enhanced error type for streaming
 */
export interface StreamingError extends Error {
  status?: number;
  code?: string;
  context?: string;
  retryable?: boolean;
}

/**
 * Stream processing state
 */
export interface StreamState {
  isStreaming: boolean;
  totalContent: string;
  startTime: number;
  metrics: Partial<StreamingPerformanceMetrics & UsageData>;
  abortController: AbortController | null;
}