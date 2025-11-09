/**
 * Central configuration for media generation features
 * Provides environment-variable based configuration with sensible defaults
 */

/**
 * Video generation polling configuration
 */
export const VIDEO_POLLING_CONFIG = {
  /** Interval between polling requests in milliseconds */
  INTERVAL_MS: Number(process.env.NEXT_PUBLIC_VIDEO_POLLING_INTERVAL_MS ?? 2000),
  
  /** Maximum time to poll before timeout in milliseconds */
  TIMEOUT_MS: Number(process.env.NEXT_PUBLIC_VIDEO_POLLING_TIMEOUT_MS ?? 600000), // 10 minutes
  
  /** Maximum polling interval for exponential backoff */
  MAX_INTERVAL_MS: Number(process.env.NEXT_PUBLIC_VIDEO_MAX_POLLING_INTERVAL_MS ?? 30000),
} as const;

/**
 * Retry configuration for failed requests
 */
export const RETRY_CONFIG = {
  /** Maximum number of retry attempts */
  MAX_COUNT: Number(process.env.NEXT_PUBLIC_MAX_RETRY_COUNT ?? 3),
  
  /** Minimum delay between retries in milliseconds */
  MIN_DELAY_MS: Number(process.env.NEXT_PUBLIC_MIN_RETRY_DELAY_MS ?? 1000),
  
  /** Maximum delay between retries in milliseconds */
  MAX_DELAY_MS: Number(process.env.NEXT_PUBLIC_MAX_RETRY_DELAY_MS ?? 10000),
  
  /** Exponential backoff multiplier */
  BACKOFF_MULTIPLIER: Number(process.env.NEXT_PUBLIC_RETRY_BACKOFF_MULTIPLIER ?? 2),
} as const;

/**
 * History and storage limits
 */
export const STORAGE_CONFIG = {
  /** Maximum number of items to keep in generation history */
  MAX_HISTORY_SIZE: Number(process.env.NEXT_PUBLIC_MAX_HISTORY_SIZE ?? 20),
  
  /** Number of items to show in history lists */
  IMAGE_HISTORY_LIMIT: Number(process.env.NEXT_PUBLIC_IMAGE_HISTORY_LIMIT ?? 10),
  
  /** Cache TTL for metadata in milliseconds */
  METADATA_CACHE_TTL_MS: Number(process.env.NEXT_PUBLIC_METADATA_CACHE_TTL_MS ?? 3600000), // 1 hour
} as const;

/**
 * Real-time connection configuration
 */
export const REALTIME_CONFIG = {
  /** Maximum SignalR connection errors before fallback */
  MAX_SIGNALR_ERRORS: Number(process.env.NEXT_PUBLIC_MAX_SIGNALR_ERRORS ?? 3),
  
  /** SignalR reconnection interval in milliseconds */
  SIGNALR_RECONNECT_INTERVAL_MS: Number(process.env.NEXT_PUBLIC_SIGNALR_RECONNECT_MS ?? 5000),
  
  /** SignalR connection timeout in milliseconds */
  SIGNALR_TIMEOUT_MS: Number(process.env.NEXT_PUBLIC_SIGNALR_TIMEOUT_MS ?? 30000),
} as const;

/**
 * UI configuration
 */
export const UI_CONFIG = {
  /** Grid columns configuration for different breakpoints */
  GALLERY_GRID_COLS: {
    BASE: Number(process.env.NEXT_PUBLIC_GALLERY_COLS_BASE ?? 1),
    SM: Number(process.env.NEXT_PUBLIC_GALLERY_COLS_SM ?? 2),
    MD: Number(process.env.NEXT_PUBLIC_GALLERY_COLS_MD ?? 2),
    LG: Number(process.env.NEXT_PUBLIC_GALLERY_COLS_LG ?? 3),
  },
  
  /** Icon sizes in pixels */
  ICON_SIZES: {
    SMALL: Number(process.env.NEXT_PUBLIC_ICON_SIZE_SMALL ?? 14),
    MEDIUM: Number(process.env.NEXT_PUBLIC_ICON_SIZE_MEDIUM ?? 16),
    LARGE: Number(process.env.NEXT_PUBLIC_ICON_SIZE_LARGE ?? 20),
  },
  
  /** Prompt input configuration */
  PROMPT_INPUT: {
    MAX_LENGTH: Number(process.env.NEXT_PUBLIC_PROMPT_MAX_LENGTH ?? 2000),
    MIN_ROWS: Number(process.env.NEXT_PUBLIC_PROMPT_MIN_ROWS ?? 3),
    MAX_ROWS: Number(process.env.NEXT_PUBLIC_PROMPT_MAX_ROWS ?? 8),
  },
  
  /** Animation durations in milliseconds */
  ANIMATIONS: {
    FADE_DURATION_MS: Number(process.env.NEXT_PUBLIC_FADE_DURATION_MS ?? 200),
    SLIDE_DURATION_MS: Number(process.env.NEXT_PUBLIC_SLIDE_DURATION_MS ?? 300),
  },
} as const;

/**
 * Image generation specific configuration
 */
export const IMAGE_CONFIG = {
  /** Default values for image generation */
  DEFAULTS: {
    SIZE: process.env.NEXT_PUBLIC_DEFAULT_IMAGE_SIZE ?? '1024x1024',
    STYLE: process.env.NEXT_PUBLIC_DEFAULT_IMAGE_STYLE ?? 'vivid',
    QUALITY: process.env.NEXT_PUBLIC_DEFAULT_IMAGE_QUALITY ?? 'standard',
    N: Number(process.env.NEXT_PUBLIC_DEFAULT_IMAGE_COUNT ?? 1),
  },
  
  /** Maximum simultaneous image generations */
  MAX_CONCURRENT_GENERATIONS: Number(process.env.NEXT_PUBLIC_MAX_CONCURRENT_IMAGES ?? 4),
} as const;

/**
 * Video generation specific configuration
 */
export const VIDEO_CONFIG = {
  /** Default values for video generation */
  DEFAULTS: {
    DURATION_SECONDS: Number(process.env.NEXT_PUBLIC_DEFAULT_VIDEO_DURATION ?? 5),
    FPS: Number(process.env.NEXT_PUBLIC_DEFAULT_VIDEO_FPS ?? 24),
    RESOLUTION: process.env.NEXT_PUBLIC_DEFAULT_VIDEO_RESOLUTION ?? '1280x720',
  },
  
  /** Maximum video duration in seconds */
  MAX_DURATION_SECONDS: Number(process.env.NEXT_PUBLIC_MAX_VIDEO_DURATION ?? 60),
  
  /** Maximum file size in bytes */
  MAX_FILE_SIZE_BYTES: Number(process.env.NEXT_PUBLIC_MAX_VIDEO_SIZE_BYTES ?? 104857600), // 100MB
} as const;

/**
 * Media processing configuration
 */
export const PROCESSING_CONFIG = {
  /** Worker pool size for metadata extraction */
  METADATA_WORKER_POOL_SIZE: Number(process.env.NEXT_PUBLIC_METADATA_WORKERS ?? 2),
  
  /** Maximum time to wait for metadata extraction */
  METADATA_EXTRACTION_TIMEOUT_MS: Number(process.env.NEXT_PUBLIC_METADATA_TIMEOUT_MS ?? 5000),
  
  /** Batch size for bulk operations */
  BATCH_SIZE: Number(process.env.NEXT_PUBLIC_BATCH_SIZE ?? 10),
} as const;

/**
 * Development configuration
 */
export const DEV_CONFIG = {
  /** Enable debug logging */
  DEBUG_ENABLED: process.env.NEXT_PUBLIC_DEBUG === 'true',
  
  /** Mock delay for simulated operations in milliseconds */
  MOCK_DELAY_MS: Number(process.env.NEXT_PUBLIC_MOCK_DELAY_MS ?? 1000),
  
  /** Enable performance monitoring */
  PERF_MONITORING: process.env.NEXT_PUBLIC_PERF_MONITORING === 'true',
} as const;

/**
 * Validation helpers
 */
export const validateConfig = (): void => {
  // Validate polling configuration
  if (VIDEO_POLLING_CONFIG.INTERVAL_MS <= 0) {
    console.error('Invalid VIDEO_POLLING_INTERVAL_MS: must be positive');
  }
  
  if (VIDEO_POLLING_CONFIG.TIMEOUT_MS <= VIDEO_POLLING_CONFIG.INTERVAL_MS) {
    console.error('Invalid VIDEO_POLLING_TIMEOUT_MS: must be greater than INTERVAL_MS');
  }
  
  // Validate retry configuration
  if (RETRY_CONFIG.MAX_COUNT < 0) {
    console.error('Invalid MAX_RETRY_COUNT: must be non-negative');
  }
  
  if (RETRY_CONFIG.MIN_DELAY_MS > RETRY_CONFIG.MAX_DELAY_MS) {
    console.error('Invalid retry delays: MIN_DELAY_MS must be less than MAX_DELAY_MS');
  }
  
  // Validate storage configuration
  if (STORAGE_CONFIG.MAX_HISTORY_SIZE <= 0) {
    console.error('Invalid MAX_HISTORY_SIZE: must be positive');
  }
  
  // Validate UI configuration
  if (UI_CONFIG.PROMPT_INPUT.MAX_LENGTH <= 0) {
    console.error('Invalid PROMPT_MAX_LENGTH: must be positive');
  }
  
  // Log configuration in development
  if (DEV_CONFIG.DEBUG_ENABLED) {
    console.warn('Media Generation Configuration:', {
      VIDEO_POLLING_CONFIG,
      RETRY_CONFIG,
      STORAGE_CONFIG,
      REALTIME_CONFIG,
      UI_CONFIG,
      IMAGE_CONFIG,
      VIDEO_CONFIG,
      PROCESSING_CONFIG,
    });
  }
};

// Run validation in development
if (process.env.NODE_ENV === 'development') {
  validateConfig();
}

/**
 * Export all configurations as a single object for convenience
 */
export const MEDIA_GENERATION_CONFIG = {
  video: VIDEO_CONFIG,
  image: IMAGE_CONFIG,
  polling: VIDEO_POLLING_CONFIG,
  retry: RETRY_CONFIG,
  storage: STORAGE_CONFIG,
  realtime: REALTIME_CONFIG,
  ui: UI_CONFIG,
  processing: PROCESSING_CONFIG,
  dev: DEV_CONFIG,
} as const;

export default MEDIA_GENERATION_CONFIG;