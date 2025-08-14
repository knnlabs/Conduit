import type { ChatCompletionChunk } from './chat';

/**
 * Enhanced SSE (Server-Sent Events) event types supported by Conduit.
 * These event types allow for richer streaming responses that include
 * performance metrics and other metadata alongside content.
 * 
 * @enum {string}
 * @since 0.3.0
 */
export enum EnhancedSSEEventType {
  /** Regular content event containing chat completion chunks */
  Content = 'content',
  /** Live performance metrics during streaming */
  Metrics = 'metrics', 
  /** Final performance metrics at stream completion */
  MetricsFinal = 'metrics-final',
  /** Error events during streaming */
  Error = 'error',
  /** Stream completion marker */
  Done = 'done',
}

/**
 * Performance metrics sent during streaming (matches Core API format).
 * These metrics provide real-time insights into the streaming performance.
 * 
 * @interface StreamingMetrics
 * @since 0.3.0
 * 
 * @example
 * ```typescript
 * {
 *   request_id: 'req-123',
 *   elapsed_ms: 1500,
 *   tokens_generated: 25,
 *   current_tokens_per_second: 16.67,
 *   time_to_first_token_ms: 120,
 *   avg_inter_token_latency_ms: 60
 * }
 * ```
 */
export interface StreamingMetrics {
  /** Unique identifier for the streaming request */
  request_id?: string;
  /** Total elapsed time in milliseconds since stream start */
  elapsed_ms?: number;
  /** Number of tokens generated so far */
  tokens_generated?: number;
  /** Current token generation rate (tokens per second) */
  current_tokens_per_second?: number;
  /** Time to first token in milliseconds */
  time_to_first_token_ms?: number;
  /** Average latency between tokens in milliseconds */
  avg_inter_token_latency_ms?: number;
}

/**
 * Final performance metrics sent at the end of a streaming response.
 * Provides comprehensive performance statistics for the entire request.
 * 
 * @interface FinalMetrics
 * @since 0.3.0
 * 
 * @example
 * ```typescript
 * {
 *   total_latency_ms: 2500,
 *   time_to_first_token_ms: 150,
 *   tokens_per_second: 42.0,
 *   prompt_tokens_per_second: 200,
 *   completion_tokens_per_second: 42.0,
 *   provider: 'openai',
 *   model: 'gpt-4',
 *   streaming: true,
 *   avg_inter_token_latency_ms: 59.5,
 *   prompt_tokens: 50,
 *   completion_tokens: 105,
 *   total_tokens: 155
 * }
 * ```
 */
export interface FinalMetrics {
  /** Total end-to-end latency in milliseconds */
  total_latency_ms?: number;
  /** Time to first token in milliseconds */
  time_to_first_token_ms?: number;
  /** Overall tokens per second for the completion */
  tokens_per_second?: number;
  /** Processing speed for prompt tokens (tokens/second) */
  prompt_tokens_per_second?: number;
  /** Generation speed for completion tokens (tokens/second) */
  completion_tokens_per_second?: number;
  /** LLM provider name (e.g., 'openai', 'anthropic') */
  provider?: string;
  /** Model identifier (e.g., 'gpt-4', 'claude-3') */
  model?: string;
  /** Whether streaming was used for this request */
  streaming?: boolean;
  /** Average latency between consecutive tokens in milliseconds */
  avg_inter_token_latency_ms?: number;
  // Usage data
  /** Number of tokens in the prompt */
  prompt_tokens?: number;
  /** Number of tokens in the completion */
  completion_tokens?: number;
  /** Total token count (prompt + completion) */
  total_tokens?: number;
}

/**
 * Enhanced streaming event that preserves SSE event types.
 * Wraps different types of data (content, metrics, errors) with their event type.
 * Does not extend BaseStreamChunk as it represents wrapped events.
 * 
 * @interface EnhancedStreamEvent
 * @since 0.3.0
 * 
 * @example
 * ```typescript
 * // Content event
 * {
 *   type: 'content',
 *   data: { id: 'chatcmpl-123', object: 'chat.completion.chunk', ... }
 * }
 * 
 * // Metrics event
 * {
 *   type: 'metrics',
 *   data: { current_tokens_per_second: 42.5, tokens_generated: 30 }
 * }
 * ```
 */
export interface EnhancedStreamEvent {
  /** The type of SSE event */
  type: EnhancedSSEEventType;
  /** The event data, type depends on the event type */
  data: ChatCompletionChunk | StreamingMetrics | FinalMetrics | string;
}

/**
 * Type guard to check if data is a ChatCompletionChunk.
 * 
 * @param {unknown} data - The data to check
 * @returns {boolean} True if data is a ChatCompletionChunk
 * @since 0.3.0
 * 
 * @example
 * ```typescript
 * if (isChatCompletionChunk(event.data)) {
 *   // TypeScript now knows event.data is ChatCompletionChunk
 *   console.warn(event.data.choices[0].delta.content);
 * }
 * ```
 */
export function isChatCompletionChunk(data: unknown): data is ChatCompletionChunk {
  return (
    typeof data === 'object' &&
    data !== null &&
    'object' in data &&
    (data as Record<string, unknown>).object === 'chat.completion.chunk'
  );
}

/**
 * Type guard to check if data is StreamingMetrics.
 * 
 * @param {unknown} data - The data to check
 * @returns {boolean} True if data is StreamingMetrics
 * @since 0.3.0
 * 
 * @example
 * ```typescript
 * if (isStreamingMetrics(event.data)) {
 *   // TypeScript now knows event.data is StreamingMetrics
 *   console.warn(`Speed: ${event.data.current_tokens_per_second} tokens/sec`);
 * }
 * ```
 */
export function isStreamingMetrics(data: unknown): data is StreamingMetrics {
  return (
    typeof data === 'object' &&
    data !== null &&
    ('current_tokens_per_second' in data || 'tokens_generated' in data || 'elapsed_ms' in data)
  );
}

/**
 * Type guard to check if data is FinalMetrics.
 * 
 * @param {unknown} data - The data to check
 * @returns {boolean} True if data is FinalMetrics
 * @since 0.3.0
 * 
 * @example
 * ```typescript
 * if (isFinalMetrics(event.data)) {
 *   // TypeScript now knows event.data is FinalMetrics
 *   console.warn(`Total tokens: ${event.data.total_tokens}`);
 *   console.warn(`Average speed: ${event.data.tokens_per_second} tokens/sec`);
 * }
 * ```
 */
export function isFinalMetrics(data: unknown): data is FinalMetrics {
  return (
    typeof data === 'object' &&
    data !== null &&
    ('tokens_per_second' in data || 'total_latency_ms' in data || 'completion_tokens' in data)
  );
}