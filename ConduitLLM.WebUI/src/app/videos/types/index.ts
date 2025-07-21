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
  status: 'pending' | 'running' | 'completed' | 'failed' | 'cancelled';
  progress: number;
  message?: string;
  estimatedTimeToCompletion?: number;
  createdAt: string;
  updatedAt: string;
  result?: VideoGenerationResult;
  error?: string;
  settings: VideoSettings;
}

export interface VideoGenerationResult {
  created: number;
  data: VideoData[];
  model?: string;
  usage?: VideoUsage;
}

export interface VideoData {
  url?: string;
  b64Json?: string;
  revisedPrompt?: string;
  metadata?: VideoMetadata;
}

export interface VideoUsage {
  promptTokens: number;
  totalTokens: number;
  durationSeconds?: number;
  processingTimeSeconds?: number;
}

export interface VideoMetadata {
  duration?: number;
  resolution?: string;
  fps?: number;
  fileSizeBytes?: number;
  format?: string;
  codec?: string;
  audioCodec?: string;
  bitrate?: number;
  mimeType?: string;
  seed?: number;
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
  MAX_POLLING_INTERVAL_MS: 30000
} as const;

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