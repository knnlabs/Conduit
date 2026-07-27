import {
  MediaData,
  MediaMetadata,
  MediaUsage,
  ResponseFormat,
  MediaGenerationStatus
} from '@/app/types/media';
import type { MediaTask, MediaSettings } from '@/app/hooks/createMediaStore';
import { VIDEO_POLLING_CONFIG, RETRY_CONFIG } from '@/app/config/mediaGeneration';

// Re-export for components that use ErrorResponse
export type { ErrorResponse } from '@/app/types/media';

// Same shape as the shared MediaSettings
export type VideoSettings = MediaSettings;

// Shared media task shape (includes lastRetryAt) plus video settings
export interface VideoTask extends MediaTask<VideoGenerationResult> {
  settings: VideoSettings;
}

// Local video types to avoid broken SDK imports
export interface VideoData extends MediaData {
  metadata?: VideoMetadata;
}

// VideoUsage is the same as MediaUsage - use MediaUsage directly
export type VideoUsage = MediaUsage;

export interface VideoMetadata extends MediaMetadata {
  fps?: number;
  codec?: string;
  audio_codec?: string;
  bitrate?: number;
}

export interface AsyncVideoGenerationResponse {
  task_id: string;
  status: string;
  progress: number;
  message?: string;
  estimated_time_to_completion?: number;
  created_at: string;
  updated_at: string;
  result?: VideoGenerationResult;
  error?: string;
}

// ErrorResponse is now imported from shared media types

export interface AsyncVideoGenerationRequest {
  prompt: string;
  model?: string;
  duration?: number;
  size?: string;
  fps?: number;
  style?: string;
  response_format?: ResponseFormat;
  user?: string;
  seed?: number;
  n?: number;
  webhook_url?: string;
  webhook_metadata?: Record<string, unknown>;
  webhook_headers?: Record<string, string>;
  timeout_seconds?: number;
}


export interface VideoGenerationResult {
  created: number;
  data: VideoData[];
  model?: string;
  usage?: VideoUsage;
}




export interface VideoModel {
  id: string;
  provider: string;
  displayName?: string;
  capabilities: {
    videoGeneration: boolean;
    maxDuration?: number;
    supportedResolutions?: string[];
    supportedFps?: number[];
    supportsCustomStyles?: boolean;
    supportsSeed?: boolean;
    maxVideos?: number;
  };
  parameters?: string; // JSON string of UI parameters from ModelSeries
}

export const VideoResolutions = {
  HD: '1280x720',
  FULL_HD: '1920x1080',
  VERTICAL_HD: '720x1280',
  VERTICAL_FULL_HD: '1080x1920',
  SQUARE: '720x720',
  CUSTOM_720_480: '720x480'
} as const;

export const VideoDefaults = {
  POLLING_INTERVAL_MS: VIDEO_POLLING_CONFIG.INTERVAL_MS,
  POLLING_TIMEOUT_MS: VIDEO_POLLING_CONFIG.TIMEOUT_MS,
  MAX_POLLING_INTERVAL_MS: VIDEO_POLLING_CONFIG.MAX_INTERVAL_MS,
  MAX_RETRY_COUNT: RETRY_CONFIG.MAX_COUNT,
  MIN_RETRY_DELAY_MS: RETRY_CONFIG.MIN_DELAY_MS,
  MAX_RETRY_DELAY_MS: RETRY_CONFIG.MAX_DELAY_MS
} as const;

// Helper functions for retry logic
export const calculateRetryDelay = (retryCount: number): number => {
  // Exponential backoff: 1s, 2s, 4s (capped at 10s)
  return Math.min(
    RETRY_CONFIG.MIN_DELAY_MS * Math.pow(RETRY_CONFIG.BACKOFF_MULTIPLIER, retryCount), 
    RETRY_CONFIG.MAX_DELAY_MS
  );
};

export const canRetry = (task: VideoTask): boolean => {
  return task.retryCount < RETRY_CONFIG.MAX_COUNT && 
         task.status === MediaGenerationStatus.Failed;
};

// Re-export the live store type so consumers (and test mocks) stay in sync
// with the real store shape defined next to the store itself.
export type { VideoStoreState } from '../hooks/useVideoStore';