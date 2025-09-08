/**
 * Shared type definitions for media generation features (images, videos, etc.)
 */

/**
 * Standard error response structure from API endpoints
 */
export interface ErrorResponse {
  error: {
    message: string;
    type: string;
    param?: string | null;
    code?: string | null;
  };
}

/**
 * Common status types for async generation tasks
 */
export type GenerationStatus = 
  | 'idle' 
  | 'pending' 
  | 'running' 
  | 'generating'
  | 'completed' 
  | 'failed' 
  | 'cancelled' 
  | 'timedout'
  | 'error';

/**
 * Common metadata interface for generated media
 */
export interface MediaMetadata {
  duration?: number;
  resolution?: string;
  width?: number;
  height?: number;
  fps?: number;
  file_size_bytes?: number;
  sizeBytes?: number;
  format?: string;
  codec?: string;
  audio_codec?: string;
  bitrate?: number;
  mime_type?: string;
  seed?: number;
}

/**
 * Common usage tracking for API calls
 */
export interface MediaUsage {
  prompt_tokens: number;
  total_tokens: number;
  duration_seconds?: number;
  processing_time_seconds?: number;
}

/**
 * Common data structure for generated media
 */
export interface MediaData {
  url?: string;
  b64_json?: string;
  revised_prompt?: string;
}

/**
 * Retry history entry for failed tasks
 */
export interface RetryHistoryEntry {
  attemptNumber: number;
  timestamp: string;
  error: string;
}

/**
 * Common response format options
 */
export type ResponseFormat = 'url' | 'b64_json';

/**
 * Common quality settings
 */
export type Quality = 'standard' | 'hd';

/**
 * Common style settings
 */
export type Style = 'vivid' | 'natural';