/**
 * Video generation models and types for the Conduit Core API
 */

/**
 * Request for generating a video from a text prompt
 */
export interface VideoGenerationRequest {
  /** The text prompt that describes what video to generate */
  prompt: string;
  
  /** The model to use for video generation (e.g., "minimax-video") */
  model?: string;
  
  /** The duration of the video in seconds. Defaults to 5 seconds */
  duration?: number;
  
  /** The size/resolution of the video (e.g., "1920x1080", "1280x720") */
  size?: string;
  
  /** Frames per second for the video. Common values: 24, 30, 60 */
  fps?: number;
  
  /** The style or aesthetic of the video generation */
  style?: string;
  
  /** The format in which the generated video is returned. Options: "url" (default) or "b64_json" */
  response_format?: 'url' | 'b64_json';
  
  /** A unique identifier representing your end-user */
  user?: string;
  
  /** Optional seed for deterministic generation */
  seed?: number;
  
  /** The number of videos to generate. Defaults to 1 */
  n?: number;
}

/**
 * Response from a video generation request
 */
export interface VideoGenerationResponse {
  /** Unix timestamp of when the response was created */
  created: number;
  
  /** List of generated video data */
  data: VideoData[];
  
  /** The model used for generation */
  model?: string;
  
  /** Usage information if available */
  usage?: VideoUsage;
}

/**
 * A single generated video
 */
export interface VideoData {
  /** The URL of the generated video, if response_format is "url" */
  url?: string;
  
  /** The base64-encoded video data, if response_format is "b64_json" */
  b64_json?: string;
  
  /** The revised prompt that was used for generation */
  revised_prompt?: string;
  
  /** Additional metadata about the generated video */
  metadata?: VideoMetadata;
}

/**
 * Usage statistics for video generation
 */
export interface VideoUsage {
  /** The number of prompt tokens used */
  prompt_tokens: number;
  
  /** The total number of tokens used */
  total_tokens: number;
  
  /** The duration processed in seconds */
  duration_seconds?: number;
  
  /** The total processing time in seconds */
  processing_time_seconds?: number;
}

/**
 * Metadata about a generated video
 */
export interface VideoMetadata {
  /** The actual duration of the generated video in seconds */
  duration?: number;
  
  /** The resolution of the generated video */
  resolution?: string;
  
  /** The frames per second of the generated video */
  fps?: number;
  
  /** The file size in bytes */
  file_size_bytes?: number;
  
  /** The video format/codec */
  format?: string;
  
  /** The seed used for generation, if any */
  seed?: number;
}

/**
 * Request for async video generation
 */
export interface AsyncVideoGenerationRequest extends VideoGenerationRequest {
  /** The webhook URL to receive the result when generation is complete */
  webhook_url?: string;
  
  /** Additional metadata to include with the webhook callback */
  webhook_metadata?: Record<string, any>;
  
  /** The timeout for the generation task in seconds */
  timeout_seconds?: number;
}

/**
 * Response from an async video generation request
 */
export interface AsyncVideoGenerationResponse {
  /** The unique task identifier */
  task_id: string;
  
  /** The current status of the task */
  status: VideoTaskStatus;
  
  /** The progress percentage (0-100) */
  progress: number;
  
  /** An optional progress message */
  message?: string;
  
  /** The estimated time to completion in seconds */
  estimated_time_to_completion?: number;
  
  /** When the task was created */
  created_at: string;
  
  /** When the task was last updated */
  updated_at: string;
  
  /** The generation result, available when status is Completed */
  result?: VideoGenerationResponse;
  
  /** Error information if the task failed */
  error?: string;
}

/**
 * The status of an async video generation task
 */
export enum VideoTaskStatus {
  /** Task is waiting to be processed */
  Pending = 'Pending',
  
  /** Task is currently being processed */
  Running = 'Running',
  
  /** Task completed successfully */
  Completed = 'Completed',
  
  /** Task failed with an error */
  Failed = 'Failed',
  
  /** Task was cancelled */
  Cancelled = 'Cancelled',
  
  /** Task timed out */
  TimedOut = 'TimedOut'
}

/**
 * Options for polling video task status
 */
export interface VideoTaskPollingOptions {
  /** The polling interval in milliseconds */
  intervalMs?: number;
  
  /** The maximum polling timeout in milliseconds */
  timeoutMs?: number;
  
  /** Whether to use exponential backoff for polling intervals */
  useExponentialBackoff?: boolean;
  
  /** The maximum interval between polls in milliseconds when using exponential backoff */
  maxIntervalMs?: number;
}

/**
 * Common video models supported by Conduit
 */
export const VideoModels = {
  /** MiniMax video generation model */
  MINIMAX_VIDEO: 'minimax-video',
  
  /** Default video model */
  DEFAULT: 'minimax-video'
} as const;

/**
 * Common video resolutions
 */
export const VideoResolutions = {
  /** 720p resolution (1280x720) */
  HD: '1280x720',
  
  /** 1080p resolution (1920x1080) */
  FULL_HD: '1920x1080',
  
  /** Vertical 720p (720x1280) */
  VERTICAL_HD: '720x1280',
  
  /** Vertical 1080p (1080x1920) */
  VERTICAL_FULL_HD: '1080x1920',
  
  /** Square format (720x720) */
  SQUARE: '720x720'
} as const;

/**
 * Video response formats
 */
export const VideoResponseFormats = {
  /** Return video as URL (default) */
  URL: 'url',
  
  /** Return video as base64-encoded JSON */
  BASE64_JSON: 'b64_json'
} as const;

/**
 * Default values for video generation
 */
export const VideoDefaults = {
  /** Default duration in seconds */
  DURATION: 5,
  
  /** Default resolution */
  RESOLUTION: VideoResolutions.HD,
  
  /** Default frames per second */
  FPS: 30,
  
  /** Default response format */
  RESPONSE_FORMAT: VideoResponseFormats.URL,
  
  /** Default polling interval in milliseconds */
  POLLING_INTERVAL_MS: 2000,
  
  /** Default polling timeout in milliseconds */
  POLLING_TIMEOUT_MS: 600000, // 10 minutes
  
  /** Default maximum polling interval in milliseconds */
  MAX_POLLING_INTERVAL_MS: 30000 // 30 seconds
} as const;

/**
 * Capabilities of a video generation model
 */
export interface VideoModelCapabilities {
  /** Maximum duration in seconds */
  maxDuration: number;
  
  /** Supported resolutions */
  supportedResolutions: string[];
  
  /** Supported FPS values */
  supportedFps: number[];
  
  /** Whether the model supports custom styles */
  supportsCustomStyles: boolean;
  
  /** Whether the model supports seed-based generation */
  supportsSeed: boolean;
  
  /** Maximum number of videos that can be generated in one request */
  maxVideos: number;
}

/**
 * Gets the capabilities for a specific video model
 */
export function getVideoModelCapabilities(model: string): VideoModelCapabilities {
  const modelLower = model.toLowerCase();
  
  switch (modelLower) {
    case 'minimax-video':
    case 'minimax-video-01':
      return {
        maxDuration: 6,
        supportedResolutions: [
          VideoResolutions.HD,
          VideoResolutions.FULL_HD,
          VideoResolutions.VERTICAL_HD,
          VideoResolutions.VERTICAL_FULL_HD,
          '720x480'
        ],
        supportedFps: [24, 30],
        supportsCustomStyles: true,
        supportsSeed: true,
        maxVideos: 1
      };
    
    default:
      return {
        maxDuration: 60,
        supportedResolutions: [
          VideoResolutions.HD,
          VideoResolutions.FULL_HD,
          VideoResolutions.SQUARE
        ],
        supportedFps: [24, 30, 60],
        supportsCustomStyles: true,
        supportsSeed: true,
        maxVideos: 10
      };
  }
}

/**
 * Validates a video generation request
 */
export function validateVideoGenerationRequest(request: VideoGenerationRequest): void {
  if (!request.prompt || request.prompt.trim().length === 0) {
    throw new Error('Prompt is required');
  }
  
  if (request.n !== undefined && (request.n <= 0 || request.n > 10)) {
    throw new Error('Number of videos must be between 1 and 10');
  }
  
  if (request.duration !== undefined && (request.duration <= 0 || request.duration > 60)) {
    throw new Error('Duration must be between 1 and 60 seconds');
  }
  
  if (request.fps !== undefined && (request.fps <= 0 || request.fps > 120)) {
    throw new Error('FPS must be between 1 and 120');
  }
  
  if (request.response_format && 
      request.response_format !== VideoResponseFormats.URL && 
      request.response_format !== VideoResponseFormats.BASE64_JSON) {
    throw new Error(`Response format must be '${VideoResponseFormats.URL}' or '${VideoResponseFormats.BASE64_JSON}'`);
  }
}

/**
 * Validates an async video generation request
 */
export function validateAsyncVideoGenerationRequest(request: AsyncVideoGenerationRequest): void {
  // First validate the base video generation request
  validateVideoGenerationRequest(request);
  
  // Additional validation for async-specific fields
  if (request.timeout_seconds !== undefined && 
      (request.timeout_seconds <= 0 || request.timeout_seconds > 3600)) {
    throw new Error('Timeout must be between 1 and 3600 seconds');
  }
  
  if (request.webhook_url && !isValidUrl(request.webhook_url)) {
    throw new Error('WebhookUrl must be a valid HTTP or HTTPS URL');
  }
}

/**
 * Helper function to validate URLs
 */
function isValidUrl(url: string): boolean {
  try {
    const parsedUrl = new URL(url);
    return parsedUrl.protocol === 'http:' || parsedUrl.protocol === 'https:';
  } catch {
    return false;
  }
}