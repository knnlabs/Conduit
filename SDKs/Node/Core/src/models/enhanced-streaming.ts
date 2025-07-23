import type { ChatCompletionChunk } from './chat';

/**
 * Enhanced SSE event types supported by Conduit
 */
export enum EnhancedSSEEventType {
  Content = 'content',
  Metrics = 'metrics', 
  MetricsFinal = 'metrics-final',
  Error = 'error',
  Done = 'done',
}

/**
 * Performance metrics sent during streaming
 */
export interface StreamingMetrics {
  requestId?: string;
  elapsedMs?: number;
  tokensGenerated?: number;
  currentTokensPerSecond?: number;
  timeToFirstTokenMs?: number;
  avgInterTokenLatencyMs?: number;
}

/**
 * Final performance metrics sent at end of stream
 */
export interface FinalMetrics {
  total_latency_ms?: number;
  time_to_first_token_ms?: number;
  tokens_per_second?: number;
  prompt_tokens_per_second?: number;
  completion_tokens_per_second?: number;
  provider?: string;
  model?: string;
  streaming?: boolean;
  avg_inter_token_latency_ms?: number;
  // Usage data
  prompt_tokens?: number;
  completion_tokens?: number;
  total_tokens?: number;
}

/**
 * Enhanced streaming event that preserves SSE event types
 * Does not extend BaseStreamChunk as it represents wrapped events
 */
export interface EnhancedStreamEvent {
  type: EnhancedSSEEventType;
  data: ChatCompletionChunk | StreamingMetrics | FinalMetrics | string;
}

/**
 * Type guard for ChatCompletionChunk
 */
export function isChatCompletionChunk(data: unknown): data is ChatCompletionChunk {
  return (
    typeof data === 'object' &&
    data !== null &&
    'object' in data &&
    (data as any).object === 'chat.completion.chunk'
  );
}

/**
 * Type guard for StreamingMetrics
 */
export function isStreamingMetrics(data: unknown): data is StreamingMetrics {
  return (
    typeof data === 'object' &&
    data !== null &&
    ('currentTokensPerSecond' in data || 'tokensGenerated' in data || 'elapsedMs' in data)
  );
}

/**
 * Type guard for FinalMetrics
 */
export function isFinalMetrics(data: unknown): data is FinalMetrics {
  return (
    typeof data === 'object' &&
    data !== null &&
    ('tokens_per_second' in data || 'total_latency_ms' in data || 'completion_tokens' in data)
  );
}