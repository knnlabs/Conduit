import { 
  MediaData, 
  MediaMetadata, 
  MediaUsage,
  RetryHistoryEntry,
  ResponseFormat 
} from '@/app/types/media';

// Re-export for components that use ErrorResponse
export type { ErrorResponse } from '@/app/types/media';

export interface VideoSettings {
  model: string;
  [key: string]: unknown; // Allow additional properties
}

export interface VideoTask {
  id: string;
  prompt: string;
  status: 'pending' | 'running' | 'completed' | 'failed' | 'cancelled' | 'timedout';
  progress: number;
  message?: string;
  estimatedTimeToCompletion?: number;
  createdAt: string;
  updatedAt: string;
  result?: VideoGenerationResult;
  error?: string;
  settings: VideoSettings;
  retryCount: number;
  lastRetryAt?: string;
  retryHistory: Array<RetryHistoryEntry>;
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
  POLLING_INTERVAL_MS: 2000,
  POLLING_TIMEOUT_MS: 600000,
  MAX_POLLING_INTERVAL_MS: 30000,
  MAX_RETRY_COUNT: 3,
  MIN_RETRY_DELAY_MS: 1000,
  MAX_RETRY_DELAY_MS: 10000
} as const;

// Helper functions for retry logic
export const calculateRetryDelay = (retryCount: number): number => {
  // Exponential backoff: 1s, 2s, 4s (capped at 10s)
  return Math.min(
    VideoDefaults.MIN_RETRY_DELAY_MS * Math.pow(2, retryCount), 
    VideoDefaults.MAX_RETRY_DELAY_MS
  );
};

export const canRetry = (task: VideoTask): boolean => {
  return task.retryCount < VideoDefaults.MAX_RETRY_COUNT && 
         ['failed', 'timedout'].includes(task.status);
};

export interface VideoStoreState {
  // UI State
  error: string | null;
  
  // Settings
  settings: VideoSettings;
  
  // Tasks
  currentTask: VideoTask | null;
  taskHistory: VideoTask[];
  
  // Actions
  updateSettings: (updates: Partial<VideoSettings>) => void;
  setError: (error: string | null) => void;
  addTask: (task: VideoTask) => void;
  updateTask: (taskId: string, updates: Partial<VideoTask>) => void;
  removeTask: (taskId: string) => void;
  clearHistory: () => void;
}