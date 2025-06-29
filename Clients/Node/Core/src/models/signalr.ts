/**
 * SignalR connection state.
 */
export enum HubConnectionState {
  Disconnected = 'Disconnected',
  Connecting = 'Connecting',
  Connected = 'Connected',
  Disconnecting = 'Disconnecting',
  Reconnecting = 'Reconnecting'
}

/**
 * SignalR hub endpoints.
 */
export const SignalREndpoints = {
  TaskHub: '/hubs/tasks',
  VideoGenerationHub: '/hubs/video-generation',
  ImageGenerationHub: '/hubs/image-generation',
  NavigationStateHub: '/hubs/navigation-state'
} as const;

/**
 * SignalR transport types.
 */
export enum HttpTransportType {
  None = 0,
  WebSockets = 1,
  ServerSentEvents = 2,
  LongPolling = 4
}

/**
 * Default transport configuration.
 */
export const DefaultTransports = 
  HttpTransportType.WebSockets | 
  HttpTransportType.ServerSentEvents | 
  HttpTransportType.LongPolling;

/**
 * SignalR connection options.
 */
export interface SignalRConnectionOptions {
  /**
   * Access token for authentication.
   */
  accessTokenFactory?: () => string | Promise<string>;
  
  /**
   * Transport types to use.
   */
  transport?: HttpTransportType;
  
  /**
   * Logging level.
   */
  logLevel?: SignalRLogLevel;
  
  /**
   * Close timeout in milliseconds.
   */
  closeTimeout?: number;
  
  /**
   * Headers to include with requests.
   */
  headers?: Record<string, string>;
}

/**
 * SignalR logging levels.
 */
export enum SignalRLogLevel {
  Trace = 0,
  Debug = 1,
  Information = 2,
  Warning = 3,
  Error = 4,
  Critical = 5,
  None = 6
}

/**
 * Task hub server interface.
 */
export interface ITaskHubServer {
  subscribeToTask(taskId: string): Promise<void>;
  unsubscribeFromTask(taskId: string): Promise<void>;
  subscribeToTaskType(taskType: string): Promise<void>;
  unsubscribeFromTaskType(taskType: string): Promise<void>;
}

/**
 * Video generation hub server interface.
 */
export interface IVideoGenerationHubServer {
  subscribeToTask(taskId: string): Promise<void>;
  unsubscribeFromTask(taskId: string): Promise<void>;
}

/**
 * Image generation hub server interface.
 */
export interface IImageGenerationHubServer {
  subscribeToTask(taskId: string): Promise<void>;
  unsubscribeFromTask(taskId: string): Promise<void>;
}

/**
 * Task started event data.
 */
export interface TaskStartedEvent {
  taskId: string;
  taskType: string;
  metadata: any;
}

/**
 * Task progress event data.
 */
export interface TaskProgressEvent {
  taskId: string;
  progress: number;
  message?: string;
}

/**
 * Task completed event data.
 */
export interface TaskCompletedEvent {
  taskId: string;
  result: any;
}

/**
 * Task failed event data.
 */
export interface TaskFailedEvent {
  taskId: string;
  error: string;
  isRetryable: boolean;
}

/**
 * Task cancelled event data.
 */
export interface TaskCancelledEvent {
  taskId: string;
  reason?: string;
}

/**
 * Task timed out event data.
 */
export interface TaskTimedOutEvent {
  taskId: string;
  timeoutSeconds: number;
}

/**
 * Video generation started event data.
 */
export interface VideoGenerationStartedEvent {
  taskId: string;
  prompt: string;
  estimatedSeconds: number;
}

/**
 * Video generation progress event data.
 */
export interface VideoGenerationProgressEvent {
  taskId: string;
  progress: number;
  currentFrame?: number;
  totalFrames?: number;
  message?: string;
}

/**
 * Video generation completed event data.
 */
export interface VideoGenerationCompletedEvent {
  taskId: string;
  videoUrl: string;
  duration: number;
  metadata: any;
}

/**
 * Image generation started event data.
 */
export interface ImageGenerationStartedEvent {
  taskId: string;
  prompt: string;
  model: string;
}

/**
 * Image generation progress event data.
 */
export interface ImageGenerationProgressEvent {
  taskId: string;
  progress: number;
  stage?: string;
}

/**
 * Image generation completed event data.
 */
export interface ImageGenerationCompletedEvent {
  taskId: string;
  imageUrl: string;
  metadata: any;
}