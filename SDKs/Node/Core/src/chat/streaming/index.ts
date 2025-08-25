/**
 * Chat streaming module exports
 * Framework-agnostic streaming functionality
 */

// Main streaming manager
export { ChatStreamingManager } from './chat-streaming-manager';

// Performance metrics utilities
export { PerformanceMetricsCalculator, MetricsUtils } from './performance-metrics';

// Types
export type {
  StreamingConfig,
  SendMessageOptions,
  StreamMessageOptions,
  StreamingCallbacks,
  StreamingError,
  StreamState,
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatCompletionChunk,
  StreamingPerformanceMetrics,
  UsageData,
  MetricsEventData,
  MessageMetadata
} from './types';

// Re-export MessageContent from models for convenience
export type { MessageContent } from '../../models/chat';