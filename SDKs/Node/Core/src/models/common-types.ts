/**
 * Common type definitions for Core SDK to replace Record<string, any> usage
 */

import type { VideoWebhookMetadata } from './metadata';

/**
 * Video generation API request (internal)
 */
export interface VideoApiRequest {
  /** The prompt for video generation */
  prompt: string;
  /** Model to use */
  model: string;
  /** Duration in seconds */
  duration?: number;
  /** Video size/resolution */
  size?: string;
  /** Frames per second */
  fps?: number;
  /** Style preset */
  style?: string;
  /** Response format */
  response_format: 'url' | 'b64_json';
  /** User identifier */
  user?: string;
  /** Random seed */
  seed?: number;
  /** Number of videos to generate */
  n: number;
}

/**
 * Async video generation API request (internal)
 */
export interface AsyncVideoApiRequest extends VideoApiRequest {
  /** Webhook URL for completion notification */
  webhook_url?: string;
  /** Metadata to include in webhook */
  webhook_metadata?: VideoWebhookMetadata;
  /** Headers for webhook request */
  webhook_headers?: { [key: string]: string };
  /** Timeout in seconds for async processing */
  timeout_seconds?: number;
}


/**
 * Notification filter parameters
 */
export interface NotificationFilters {
  /** Filter by event type */
  eventType?: string;
  /** Filter by resource type */
  resourceType?: string;
  /** Filter by resource ID */
  resourceId?: string;
  /** Filter by severity */
  severity?: 'info' | 'warning' | 'error' | 'critical';
  /** Filter by read status */
  isRead?: boolean;
  /** Filter by date range */
  dateRange?: {
    start?: string;
    end?: string;
  };
}

/**
 * Generic task result wrapper
 * Used when task results can be of various types
 */
export type TaskResult<T = unknown> = T;

/**
 * Task metadata for tracking
 */
export interface TaskMetadata {
  /** Task type identifier */
  taskType: string;
  /** Resource being processed */
  resourceId?: string;
  /** User who initiated the task */
  initiatedBy?: string;
  /** Priority level */
  priority?: 'low' | 'normal' | 'high';
  /** Custom attributes */
  attributes?: {
    [key: string]: string | number | boolean;
  };
}