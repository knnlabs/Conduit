/**
 * Chat utilities for building and processing chat messages
 * Framework-agnostic utilities extracted from WebUI
 */

// Chat helpers
export { buildMessageContent, type ImageAttachment } from './chat-helpers';

// SSE parsing
export {
  SSEParser,
  parseSSEStream,
  SSEEventType,
  type SSEEvent
} from './sse-parser';

// Error handling
export {
  isOpenAIError,
  parseSSEError,
  processSSEEvent,
  handleSSEConnectionError,
  type OpenAIError,
  type OpenAIErrorResponse,
  type AppError,
  type ProcessedSSEEvent
} from './error-handlers';

// Structured content processing
export {
  processStructuredContent,
  getBlockQuoteMetadata,
  cleanBlockQuoteContent,
  type ProcessedBlockQuote
} from './structured-content';