// Import common SignalR types from Common package
import {
  HubConnectionState,
  HttpTransportType,
  DefaultTransports,
  SignalRConnectionOptions,
  SignalRLogLevel
} from '@knn_labs/conduit-common';

// Re-export for backward compatibility
export {
  HubConnectionState,
  HttpTransportType,
  DefaultTransports,
  SignalRLogLevel
};

export type { SignalRConnectionOptions };

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
  eventType: 'TaskStarted';
  taskId: string;
  taskType: string;
  metadata: Record<string, unknown>;
}

/**
 * Task progress event data.
 */
export interface TaskProgressEvent {
  eventType: 'TaskProgress';
  taskId: string;
  progress: number;
  message?: string;
}

/**
 * Task completed event data.
 */
export interface TaskCompletedEvent {
  eventType: 'TaskCompleted';
  taskId: string;
  result: Record<string, unknown>;
}

/**
 * Task failed event data.
 */
export interface TaskFailedEvent {
  eventType: 'TaskFailed';
  taskId: string;
  error: string;
  isRetryable: boolean;
}

/**
 * Task cancelled event data.
 */
export interface TaskCancelledEvent {
  eventType: 'TaskCancelled';
  taskId: string;
  reason?: string;
}

/**
 * Task timed out event data.
 */
export interface TaskTimedOutEvent {
  eventType: 'TaskTimedOut';
  taskId: string;
  timeoutSeconds: number;
}

/**
 * Video generation started event data.
 */
export interface VideoGenerationStartedEvent {
  eventType: 'VideoGenerationStarted';
  taskId: string;
  prompt: string;
  estimatedSeconds: number;
}

/**
 * Video generation progress event data.
 */
export interface VideoGenerationProgressEvent {
  eventType: 'VideoGenerationProgress';
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
  eventType: 'VideoGenerationCompleted';
  taskId: string;
  videoUrl: string;
  duration: number;
  metadata: Record<string, unknown>;
}

/**
 * Image generation started event data.
 */
export interface ImageGenerationStartedEvent {
  eventType: 'ImageGenerationStarted';
  taskId: string;
  prompt: string;
  model: string;
}

/**
 * Image generation progress event data.
 */
export interface ImageGenerationProgressEvent {
  eventType: 'ImageGenerationProgress';
  taskId: string;
  progress: number;
  stage?: string;
}

/**
 * Image generation completed event data.
 */
export interface ImageGenerationCompletedEvent {
  eventType: 'ImageGenerationCompleted';
  taskId: string;
  imageUrl: string;
  metadata: Record<string, unknown>;
}

/**
 * Video generation failed event data.
 */
export interface VideoGenerationFailedEvent {
  eventType: 'VideoGenerationFailed';
  taskId: string;
  error: string;
  errorCode?: string;
  isRetryable: boolean;
}

/**
 * Image generation failed event data.
 */
export interface ImageGenerationFailedEvent {
  eventType: 'ImageGenerationFailed';
  taskId: string;
  error: string;
  errorCode?: string;
  isRetryable: boolean;
}

/**
 * Union type for all task events.
 */
export type TaskEvent = 
  | TaskStartedEvent
  | TaskProgressEvent
  | TaskCompletedEvent
  | TaskFailedEvent
  | TaskCancelledEvent
  | TaskTimedOutEvent;

/**
 * Union type for all video generation events.
 */
export type VideoGenerationEvent = 
  | VideoGenerationStartedEvent
  | VideoGenerationProgressEvent
  | VideoGenerationCompletedEvent
  | VideoGenerationFailedEvent;

/**
 * Union type for all image generation events.
 */
export type ImageGenerationEvent = 
  | ImageGenerationStartedEvent
  | ImageGenerationProgressEvent
  | ImageGenerationCompletedEvent
  | ImageGenerationFailedEvent;