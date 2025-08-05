/**
 * Performance metrics received from the Core API
 */
export interface StreamingPerformanceMetrics {
  ['tokens_per_second']?: number;
  ['completion_tokens_per_second']?: number;
  ['tokens_generated']?: number;
  ['time_to_first_token_ms']?: number;
  ['total_latency_ms']?: number;
  provider?: string;
  model?: string;
}

/**
 * Usage data in OpenAI format
 */
export interface UsageData {
  ['prompt_tokens']?: number;
  ['completion_tokens']?: number;
  ['total_tokens']?: number;
}


/**
 * SSE event types from Core API
 */
export enum SSEEventType {
  Content = 'content',
  Metrics = 'metrics',
  MetricsFinal = 'metrics-final',
  Error = 'error'
}

/**
 * Parsed SSE event
 */
export interface SSEEvent {
  event?: SSEEventType;
  data: unknown;
}

/**
 * Metrics event data
 */
export interface MetricsEventData {
  ['request_id']?: string;
  ['elapsed_ms']?: number;
  ['tokens_generated']?: number;
  ['current_tokens_per_second']?: number;
  ['completion_tokens_per_second']?: number;
  ['total_latency_ms']?: number;
  ['tokens_per_second']?: number;
  provider?: string;
  model?: string;
}