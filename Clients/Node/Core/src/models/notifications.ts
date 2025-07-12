import type { VideoMetadata } from './videos';
import type { ImageData } from './images';
import type { NotificationMetadata } from './metadata';

/**
 * Event emitted when video generation progress is updated
 */
export interface VideoProgressEvent {
  taskId: string;
  progress: number;
  status: 'queued' | 'processing' | 'completed' | 'failed';
  estimatedTimeRemaining?: number;
  message?: string;
  metadata?: VideoMetadata;
}

/**
 * Event emitted when image generation progress is updated
 */
export interface ImageProgressEvent {
  taskId: string;
  progress: number;
  status: 'queued' | 'processing' | 'completed' | 'failed';
  message?: string;
  images?: ImageData[];
}

/**
 * Event emitted when spend is updated for a virtual key
 */
export interface SpendUpdateEvent {
  virtualKeyId: number;
  virtualKeyHash: string;
  amount: number;
  totalSpend: number;
  model: string;
  provider: string;
  timestamp: string;
  remainingBudget?: number;
}

/**
 * Event emitted when spending limit is approaching or exceeded
 */
export interface SpendLimitAlertEvent {
  virtualKeyId: number;
  virtualKeyHash: string;
  alertType: 'warning' | 'critical' | 'exceeded';
  currentSpend: number;
  spendLimit: number;
  percentageUsed: number;
  message: string;
  timestamp: string;
}

/**
 * Generic task update event for any async task
 */
export interface TaskUpdateEvent {
  taskId: string;
  taskType: 'video' | 'image' | 'batch' | 'other';
  status: 'queued' | 'processing' | 'completed' | 'failed' | 'cancelled';
  progress?: number;
  result?: any;
  error?: string;
  metadata?: NotificationMetadata;
}

/**
 * Callback function types for event subscriptions
 */
export type VideoProgressCallback = (event: VideoProgressEvent) => void;
export type ImageProgressCallback = (event: ImageProgressEvent) => void;
export type SpendUpdateCallback = (event: SpendUpdateEvent) => void;
export type SpendLimitAlertCallback = (event: SpendLimitAlertEvent) => void;
export type TaskUpdateCallback = (event: TaskUpdateEvent) => void;

/**
 * Subscription handle returned when subscribing to events
 */
export interface NotificationSubscription {
  /**
   * Unique identifier for this subscription
   */
  id: string;
  
  /**
   * The event type this subscription is for
   */
  eventType: 'videoProgress' | 'imageProgress' | 'spendUpdate' | 'spendLimitAlert' | 'taskUpdate';
  
  /**
   * Unsubscribe from this event
   */
  unsubscribe: () => void;
}

/**
 * Options for notification subscriptions
 */
export interface NotificationOptions {
  /**
   * Whether to automatically reconnect on connection loss
   */
  autoReconnect?: boolean;
  
  /**
   * Filter events by specific criteria
   */
  filter?: {
    virtualKeyId?: number;
    taskIds?: string[];
    models?: string[];
    providers?: string[];
  };
  
  /**
   * Error handler for subscription errors
   */
  onError?: (error: Error) => void;
  
  /**
   * Handler for connection state changes
   */
  onConnectionStateChange?: (state: 'connected' | 'disconnected' | 'reconnecting') => void;
}