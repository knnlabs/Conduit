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
  AudioFile,
  VoiceSettings,
  AudioTranscriptionRequest,
  AudioTranscriptionResponse,
  TranscriptionSegment,
  TranscriptionWord,
  AudioTranslationRequest,
  AudioTranslationResponse,
  TextToSpeechRequest,
  TextToSpeechResponse,
  HybridAudioRequest,
  HybridAudioResponse,
  RealtimeConnectionRequest,
  RealtimeSessionConfig,
  RealtimeMessage,
  RealtimeSession,
  AudioMetadata,
  AudioProcessingOptions,
  AudioError,
  AudioValidation,
} from './models/audio';

export type {
  AudioFormat,
  TranscriptionFormat,
  TimestampGranularity,
  TextToSpeechModel,
  Voice,
  TranscriptionModel,
} from './models/audio';

export { AudioService, AudioUtils } from './services/AudioService';

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

export type {
  BatchOperationStatusEnum,
  SpendUpdateDto,
  VirtualKeyUpdateDto,
  WebhookSendDto,
  BatchSpendUpdateRequest,
  BatchVirtualKeyUpdateRequest,
  BatchWebhookSendRequest,
  BatchOperationStartResponse,
  BatchOperationMetadata,
  BatchItemResult,
  BatchItemError,
  BatchOperationStatusResponse,
  BatchOperationPollOptions,
  BatchValidationOptions,
  BatchValidationResult,
} from './models/batchOperations';

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

export { BatchOperationsService } from './services/BatchOperationsService';

export type {
  HealthCheckResponse,
  HealthCheckItem,
  HealthStatus,
  HealthCheckOptions,
  SimpleHealthStatus,
  HealthSummary,
  WaitForHealthOptions,
} from './models/health';

export { HealthService } from './services/HealthService';

export type {
  MetricsSnapshot,
  HttpMetrics,
  ResponseTimeMetrics,
  BusinessMetrics,
  CostMetrics,
  ModelUsageStats,
  VirtualKeyStats,
  SystemMetrics,
  InfrastructureMetrics,
  DatabaseMetrics,
  RedisMetrics,
  SignalRMetrics,
  RabbitMQMetrics,
  ProviderHealthStatus,
  HistoricalMetricsRequest,
  HistoricalMetricsResponse,
  MetricSeries,
  MetricDataPoint,
  KPISummary,
} from './models/metrics';

export { MetricsService } from './services/MetricsService';

export * from './utils/capabilities';

