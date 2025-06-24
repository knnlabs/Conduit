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
  ImageGenerationRequest,
  ImageGenerationResponse,
  ImageEditRequest,
  ImageEditResponse,
  ImageVariationRequest,
  ImageVariationResponse,
  ImageData,
  ImageModel,
} from './models/images';

export {
  IMAGE_MODELS,
  IMAGE_MODEL_CAPABILITIES,
  IMAGE_DEFAULTS,
} from './models/images';

export type {
  VideoGenerationRequest,
  VideoGenerationResponse,
  AsyncVideoGenerationRequest,
  AsyncVideoGenerationResponse,
  VideoData,
  VideoUsage,
  VideoMetadata,
  VideoTaskStatus,
  VideoTaskPollingOptions,
  VideoModelCapabilities,
  WebhookPayloadBase,
  VideoCompletionWebhookPayload,
  VideoProgressWebhookPayload,
} from './models/videos';

export {
  VideoModels,
  VideoResolutions,
  VideoResponseFormats,
  VideoDefaults,
  getVideoModelCapabilities,
  validateVideoGenerationRequest,
  validateAsyncVideoGenerationRequest,
} from './models/videos';

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

export type {
  TaskStatusResponse,
  TaskPollingOptions,
  CleanupTasksResponse,
} from './services/TasksService';

export {
  TaskDefaults,
  TaskHelpers,
} from './services/TasksService';

export * from './utils/capabilities';

