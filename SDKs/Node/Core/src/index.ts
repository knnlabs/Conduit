// Export only the fetch-based client as the main client
export { FetchConduitCoreClient as ConduitCoreClient } from './FetchConduitCoreClient';
export { FetchConduitCoreClient } from './FetchConduitCoreClient';
export type { ClientConfig, RequestOptions, SignalRConfig } from './client/types';
export { HttpMethod } from './client/HttpMethod';
export type { RequestOptions as CoreRequestOptions, ApiResponse } from './client/HttpMethod';

export type {
  ChatCompletionMessage,
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatCompletionChoice,
  ChatCompletionChunk,
  ChatCompletionChunkChoice,
  MessageContent,
  TextContent,
  ImageContent,
} from './models/chat';

export { ContentHelpers } from './models/chat';

// Enhanced streaming types
export type {
  EnhancedStreamEvent,
  EnhancedSSEEventType,
  StreamingMetrics,
  FinalMetrics,
} from './models/enhanced-streaming';

export type {
  EnhancedStreamingResponse,
} from './models/enhanced-streaming-response';

export {
  isChatCompletionChunk,
  isStreamingMetrics,
  isFinalMetrics,
} from './models/enhanced-streaming';

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
export { ImagesService } from './services/ImagesService';
export { VideosService } from './services/VideosService';
export type {
  VideoProgress,
  VideoProgressCallbacks,
  GenerateWithProgressResult
} from './services/VideosService';

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

// export { HealthService } from './services/HealthService'; // Removed - using HealthService from FetchConduitCoreClient

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

export type {
  ModelCapabilities,
  DiscoveredModel,
  ModelsDiscoveryResponse,
  ProviderModelsDiscoveryResponse,
  CapabilityTestResponse,
  CapabilityTest,
  BulkCapabilityTestRequest,
  CapabilityTestResult,
  BulkCapabilityTestResponse,
  BulkModelDiscoveryRequest,
  ModelDiscoveryResult,
  BulkModelDiscoveryResponse,
} from './models/discovery';

export { ModelCapability } from './models/discovery';

export { DiscoveryService } from './services/DiscoveryService';
export { ProviderModelsService } from './services/ProviderModelsService';

export { SignalRService } from './services/SignalRService';
// export { ConnectionService } from './services/ConnectionService'; // Removed - using ConnectionService from FetchConduitCoreClient
export { TaskHubClient } from './signalr/TaskHubClient';
export { VideoGenerationHubClient } from './signalr/VideoGenerationHubClient';
export { ImageGenerationHubClient } from './signalr/ImageGenerationHubClient';

export type {
  HubConnectionState,
  SignalRConnectionOptions,
  SignalRLogLevel,
  HttpTransportType,
  ITaskHubServer,
  IVideoGenerationHubServer,
  IImageGenerationHubServer,
  TaskStartedEvent,
  TaskProgressEvent,
  TaskCompletedEvent,
  TaskFailedEvent,
  TaskCancelledEvent,
  TaskTimedOutEvent,
  VideoGenerationStartedEvent,
  VideoGenerationProgressEvent,
  VideoGenerationCompletedEvent,
  ImageGenerationStartedEvent,
  ImageGenerationProgressEvent,
  ImageGenerationCompletedEvent
} from './models/signalr';

export { SignalREndpoints, DefaultTransports } from './models/signalr';

export * from './utils/capabilities';

export type {
  EmbeddingRequest,
  EmbeddingResponse,
  EmbeddingData,
  EmbeddingUsage,
} from './models/embeddings';

export {
  EmbeddingModels,
  EmbeddingEncodingFormats,
  validateEmbeddingRequest,
  convertEmbeddingToFloatArray,
  calculateCosineSimilarity,
} from './models/embeddings';

export { EmbeddingsService, EmbeddingHelpers } from './services/EmbeddingsService';

export { NotificationsService } from './services/NotificationsService';

export type {
  VideoProgressEvent,
  ImageProgressEvent,
  SpendUpdateEvent,
  SpendLimitAlertEvent,
  TaskUpdateEvent,
  VideoProgressCallback,
  ImageProgressCallback,
  SpendUpdateCallback,
  SpendLimitAlertCallback,
  TaskUpdateCallback,
  NotificationSubscription,
  NotificationOptions,
} from './models/notifications';

// Export metadata and common types
export * from './models/metadata';
export * from './models/common-types';
export * from './models/providerType';

