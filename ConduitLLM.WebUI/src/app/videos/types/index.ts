export interface VideoSettings {
  model: string;
  duration: number;
  size: string;
  fps: number;
  style?: string;
  responseFormat: 'url' | 'b64_json';
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
  retryHistory: Array<{
    attemptNumber: number;
    timestamp: string;
    error: string;
  }>;
}

// Local video types to avoid broken SDK imports
export interface VideoData {
  url?: string;
  b64_json?: string;
  revised_prompt?: string;
  metadata?: VideoMetadata;
}

export interface VideoUsage {
  prompt_tokens: number;
  total_tokens: number;
  duration_seconds?: number;
  processing_time_seconds?: number;
}

export interface VideoMetadata {
  duration?: number;
  resolution?: string;
  fps?: number;
  file_size_bytes?: number;
  format?: string;
  codec?: string;
  audio_codec?: string;
  bitrate?: number;
  mime_type?: string;
  seed?: number;
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

export interface ErrorResponse {
  error: {
    message: string;
    type: string;
    param?: string | null;
    code?: string | null;
  };
}

export interface AsyncVideoGenerationRequest {
  prompt: string;
  model?: string;
  duration?: number;
  size?: string;
  fps?: number;
  style?: string;
  response_format?: 'url' | 'b64_json';
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
  DURATION: 5,
  FPS: 30,
  RESOLUTION: VideoResolutions.HD,
  RESPONSE_FORMAT: 'url' as const,
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
  settingsVisible: boolean;
  error: string | null;
  
  // Settings
  settings: VideoSettings;
  
  // Tasks
  currentTask: VideoTask | null;
  taskHistory: VideoTask[];
  
  // Actions
  toggleSettings: () => void;
  updateSettings: (updates: Partial<VideoSettings>) => void;
  setError: (error: string | null) => void;
  addTask: (task: VideoTask) => void;
  updateTask: (taskId: string, updates: Partial<VideoTask>) => void;
  removeTask: (taskId: string) => void;
  clearHistory: () => void;
}