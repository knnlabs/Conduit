export { ConduitCoreClient } from './client/ConduitCoreClient';
export type { ClientConfig, RequestOptions } from './client/types';

export type {
  ChatCompletionMessage,
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatCompletionChoice,
  ChatCompletionChunk,
  ChatCompletionChunkChoice,
} from './models/chat';

export type {
  Model,
  ModelsResponse,
} from './models/models';

export type {
  Usage,
  ResponseFormat,
  FunctionDefinition,
  FunctionCall,
  Tool,
  ToolCall,
  FinishReason,
  PerformanceMetrics,
  ErrorResponse,
} from './models/common';

export {
  ConduitError,
  AuthenticationError,
  RateLimitError,
  ValidationError,
  NetworkError,
  StreamError,
} from './utils/errors';

