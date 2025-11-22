import type { ChatCompletionChunk } from './chat';

/**
 * Enhanced SSE (Server-Sent Events) event types supported by Conduit.
 * Combines OpenAI-compatible standard events with Conduit-specific extensions.
 * These event types allow for richer streaming responses that include
 * performance metrics and other metadata alongside content.
 *
 * @enum {string}
 * @since 0.3.0
 */
export enum EnhancedSSEEventType {
  /** Regular content event containing chat completion chunks (OpenAI compatible) */
  Content = 'content',
  /** Conduit extension: Model reasoning/thinking content separate from main response */
  Reasoning = 'reasoning',
  /** Conduit extension: Tool/function execution status and progress updates */
  ToolExecuting = 'tool-executing',
  /** Conduit extension: Individual tool execution results (optional, for detailed logging) */
  ToolResult = 'tool-result',
  /** Conduit extension: Live performance metrics during streaming */
  Metrics = 'metrics',
  /** Conduit extension: Final performance metrics at stream completion */
  MetricsFinal = 'metrics-final',
  /** Conduit extension: Error events during streaming */
  Error = 'error',
  /** Stream completion marker (OpenAI compatible) */
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
  data: ChatCompletionChunk | StreamingMetrics | FinalMetrics | ReasoningEvent | ToolExecutingEvent | ToolResultEvent | string;
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

/**
 * Reasoning event data - sent as "event: reasoning"
 * Contains model thinking/reasoning content separate from main response.
 *
 * @interface ReasoningEvent
 * @since 0.4.0
 *
 * @example
 * ```typescript
 * {
 *   content: "Let me think through this step by step..."
 * }
 * ```
 */
export interface ReasoningEvent {
  /** Reasoning content chunk */
  content: string;
}

/**
 * Tool execution status event - sent as "event: tool-executing"
 * Provides real-time feedback during function calling.
 *
 * @interface ToolExecutingEvent
 * @since 0.4.0
 *
 * @example
 * ```typescript
 * // Tool execution started
 * {
 *   tool_call_id: 'call_abc123',
 *   function_name: 'get_weather',
 *   status: 'started'
 * }
 *
 * // Tool execution completed
 * {
 *   tool_call_id: 'call_abc123',
 *   function_name: 'get_weather',
 *   status: 'completed',
 *   result: { temperature: 72, condition: 'sunny' },
 *   cost: 0.001
 * }
 * ```
 */
export interface ToolExecutingEvent {
  /** Tool call ID reference */
  tool_call_id?: string;
  /** Function name being executed */
  function_name?: string;
  /** Execution status: "started" | "completed" | "failed" */
  status: string;
  /** Function execution result (present when status = "completed") */
  result?: unknown;
  /** Cost of the function execution */
  cost?: number;
  /** Error message (present when status = "failed") */
  error_message?: string;
  /** Function execution ID for audit trail lookup */
  function_execution_id?: string;
}

/**
 * Tool result event - sent as "event: tool-result"
 * Contains individual tool execution outcome (optional, for detailed logging).
 *
 * @interface ToolResultEvent
 * @since 0.4.0
 *
 * @example
 * ```typescript
 * {
 *   tool_call_id: 'call_abc123',
 *   result: { temperature: 72, condition: 'sunny' }
 * }
 * ```
 */
export interface ToolResultEvent {
  /** Tool call ID reference */
  tool_call_id: string;
  /** Tool execution result data */
  result: unknown;
  /** Error message if execution failed */
  error?: string;
}

/**
 * Type guard to check if data is a ReasoningEvent.
 *
 * @param {unknown} data - The data to check
 * @returns {boolean} True if data is a ReasoningEvent
 * @since 0.4.0
 *
 * @example
 * ```typescript
 * if (isReasoningEvent(event.data)) {
 *   // TypeScript now knows event.data is ReasoningEvent
 *   console.warn(`Reasoning: ${event.data.content}`);
 * }
 * ```
 */
export function isReasoningEvent(data: unknown): data is ReasoningEvent {
  return (
    typeof data === 'object' &&
    data !== null &&
    'content' in data &&
    typeof (data as Record<string, unknown>).content === 'string'
  );
}

/**
 * Type guard to check if data is a ToolExecutingEvent.
 *
 * @param {unknown} data - The data to check
 * @returns {boolean} True if data is a ToolExecutingEvent
 * @since 0.4.0
 *
 * @example
 * ```typescript
 * if (isToolExecutingEvent(event.data)) {
 *   // TypeScript now knows event.data is ToolExecutingEvent
 *   console.warn(`Executing: ${event.data.function_name} - ${event.data.status}`);
 * }
 * ```
 */
export function isToolExecutingEvent(data: unknown): data is ToolExecutingEvent {
  return (
    typeof data === 'object' &&
    data !== null &&
    'status' in data &&
    typeof (data as Record<string, unknown>).status === 'string'
  );
}

/**
 * Type guard to check if data is a ToolResultEvent.
 *
 * @param {unknown} data - The data to check
 * @returns {boolean} True if data is a ToolResultEvent
 * @since 0.4.0
 *
 * @example
 * ```typescript
 * if (isToolResultEvent(event.data)) {
 *   // TypeScript now knows event.data is ToolResultEvent
 *   console.warn(`Tool result for ${event.data.tool_call_id}:`, event.data.result);
 * }
 * ```
 */
export function isToolResultEvent(data: unknown): data is ToolResultEvent {
  return (
    typeof data === 'object' &&
    data !== null &&
    'tool_call_id' in data &&
    'result' in data &&
    typeof (data as Record<string, unknown>).tool_call_id === 'string'
  );
}