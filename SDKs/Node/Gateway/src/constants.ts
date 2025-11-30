/**
 * Consolidated constants for Conduit Core SDK
 * Merged from: endpoints.ts, http.ts, streaming.ts, tasks.ts, validation.ts
 */

// Import shared HTTP constants from Common package
import {
  HTTP_HEADERS as COMMON_HTTP_HEADERS,
  CONTENT_TYPES as COMMON_CONTENT_TYPES,
  ERROR_CODES,
  HttpMethod
} from '@knn_labs/conduit-common';

// ============================================================================
// API Endpoints
// ============================================================================

/**
 * API endpoint constants for type-safe endpoint management.
 */
export const API_ENDPOINTS = {
  V1: {
    CHAT: {
      COMPLETIONS: '/v1/chat/completions',
    },
    IMAGES: {
      GENERATIONS: '/v1/images/generations',
      ASYNC_GENERATIONS: '/v1/images/generations/async',
      // Note: The following endpoints are not yet implemented in Gateway API
      EDITS: '/v1/images/edits', // Not implemented
      VARIATIONS: '/v1/images/variations', // Not implemented
      TASK_STATUS: (taskId: string) => `/v1/images/generations/${encodeURIComponent(taskId)}/status`,
      CANCEL_TASK: (taskId: string) => `/v1/images/generations/${encodeURIComponent(taskId)}`,
    },
    VIDEOS: {
      // Note: Synchronous video generation endpoint does not exist
      ASYNC_GENERATIONS: '/v1/videos/generations/async',
      TASK_STATUS: (taskId: string) => `/v1/videos/generations/tasks/${encodeURIComponent(taskId)}`,
      CANCEL_TASK: (taskId: string) => `/v1/videos/generations/${encodeURIComponent(taskId)}`,
    },
    AUDIO: {
      TRANSCRIPTIONS: '/v1/audio/transcriptions',
      TRANSLATIONS: '/v1/audio/translations',
      SPEECH: '/v1/audio/speech',
    },
    MODELS: {
      BASE: '/v1/models',
      BY_ID: (modelId: string) => `/v1/models/${encodeURIComponent(modelId)}`,
    },
    EMBEDDINGS: {
      BASE: '/v1/embeddings',
    },
    TASKS: {
      BASE: '/v1/tasks',
      BY_ID: (taskId: string) => `/v1/tasks/${encodeURIComponent(taskId)}`,
      CANCEL: (taskId: string) => `/v1/tasks/${encodeURIComponent(taskId)}/cancel`,
      POLL: (taskId: string) => `/v1/tasks/${encodeURIComponent(taskId)}/poll`,
    },
    BATCH: {
      // Note: No generic /v1/batch endpoint exists. Use specific batch endpoints:
      SPEND_UPDATES: '/v1/batch/spend-updates',
      VIRTUAL_KEY_UPDATES: '/v1/batch/virtual-key-updates',
      WEBHOOK_SENDS: '/v1/batch/webhook-sends',
      OPERATIONS: {
        BY_ID: (operationId: string) => `/v1/batch/operations/${encodeURIComponent(operationId)}`,
        CANCEL: (operationId: string) => `/v1/batch/operations/${encodeURIComponent(operationId)}/cancel`,
      },
    },
  },
  ROOT: {
    HEALTH: '/health',
    METRICS: '/metrics',
  },
} as const;

/**
 * Type-safe endpoint helper functions.
 */
export const EndpointHelpers = {
  /**
   * Get task status endpoint for any type of task.
   */
  getTaskStatusEndpoint: (taskType: 'images' | 'videos', taskId: string): string => {
    switch (taskType) {
      case 'images':
        return API_ENDPOINTS.V1.IMAGES.TASK_STATUS(taskId);
      case 'videos':
        return API_ENDPOINTS.V1.VIDEOS.TASK_STATUS(taskId);
      default:
        throw new Error(`Unsupported task type: ${taskType as string}`);
    }
  },

  /**
   * Get task cancellation endpoint for any type of task.
   */
  getCancelTaskEndpoint: (taskType: 'images' | 'videos', taskId: string): string => {
    switch (taskType) {
      case 'images':
        return API_ENDPOINTS.V1.IMAGES.CANCEL_TASK(taskId);
      case 'videos':
        return API_ENDPOINTS.V1.VIDEOS.CANCEL_TASK(taskId);
      default:
        throw new Error(`Unsupported task type: ${taskType as string}`);
    }
  },
} as const;

// ============================================================================
// HTTP Configuration
// ============================================================================

// Re-export for backward compatibility
export {
  ERROR_CODES,
  HttpMethod
};

// Re-export HTTP_HEADERS with Core SDK specific overrides
export const HTTP_HEADERS = {
  ...COMMON_HTTP_HEADERS,
  RETRY_AFTER: 'retry-after', // Core SDK uses lowercase
} as const;

// Re-export CONTENT_TYPES with Core SDK specific additions
export const CONTENT_TYPES = {
  ...COMMON_CONTENT_TYPES,
  TEXT_STREAM: 'text/plain; charset=utf-8', // Core SDK specific
} as const;

export type ErrorCode = typeof ERROR_CODES[keyof typeof ERROR_CODES];

/**
 * Client information constants.
 */
export const CLIENT_INFO = {
  NAME: '@conduit/core',
  VERSION: '0.1.0', // Could be imported from package.json
  USER_AGENT: '@conduit/core/0.1.0',
} as const;

// ============================================================================
// Streaming (SSE)
// ============================================================================

/**
 * Server-Sent Events (SSE) and streaming constants.
 */
export const STREAM_CONSTANTS = {
  DONE_MARKER: '[DONE]',
  DATA_PREFIX: 'data: ',
  SSE_COMMENT_PREFIX: ':',
  EVENT_PREFIX: 'event: ',
  ID_PREFIX: 'id: ',
  RETRY_PREFIX: 'retry: ',
} as const;

/**
 * SSE field names.
 */
export const SSE_FIELDS = {
  DATA: 'data',
  EVENT: 'event',
  ID: 'id',
  RETRY: 'retry',
} as const;

/**
 * Stream event types.
 */
export const STREAM_EVENTS = {
  MESSAGE: 'message',
  ERROR: 'error',
  DONE: 'done',
  CHUNK: 'chunk',
} as const;

export type StreamEvent = typeof STREAM_EVENTS[keyof typeof STREAM_EVENTS];

/**
 * Streaming helper utilities.
 */
export const StreamingHelpers = {
  /**
   * Check if a line indicates the stream is done.
   */
  isDoneMarker: (data: string): boolean =>
    data === STREAM_CONSTANTS.DONE_MARKER,

  /**
   * Check if a line is an SSE data line.
   */
  isDataLine: (line: string): boolean =>
    line.startsWith(STREAM_CONSTANTS.DATA_PREFIX),

  /**
   * Check if a line is an SSE comment.
   */
  isCommentLine: (line: string): boolean =>
    line.startsWith(STREAM_CONSTANTS.SSE_COMMENT_PREFIX),

  /**
   * Extract data from an SSE data line.
   */
  extractData: (line: string): string => {
    if (!StreamingHelpers.isDataLine(line)) {
      throw new Error('Line is not a data line');
    }
    return line.slice(STREAM_CONSTANTS.DATA_PREFIX.length);
  },

  /**
   * Check if a line is an SSE event line.
   */
  isEventLine: (line: string): boolean =>
    line.startsWith(STREAM_CONSTANTS.EVENT_PREFIX),

  /**
   * Extract event type from an SSE event line.
   */
  extractEvent: (line: string): string => {
    if (!StreamingHelpers.isEventLine(line)) {
      throw new Error('Line is not an event line');
    }
    return line.slice(STREAM_CONSTANTS.EVENT_PREFIX.length);
  },

  /**
   * Parse an SSE line into its components.
   */
  parseSseLine: (line: string): { field: string; value: string } | null => {
    const colonIndex = line.indexOf(':');
    if (colonIndex === -1) {
      return null;
    }

    const field = line.slice(0, colonIndex);
    let value = line.slice(colonIndex + 1);

    // Remove leading space if present
    if (value.startsWith(' ')) {
      value = value.slice(1);
    }

    return { field, value };
  },
} as const;

// ============================================================================
// Task Management
// ============================================================================

/**
 * Task status constants for async operations.
 */
export const TASK_STATUS = {
  PENDING: 'pending',
  RUNNING: 'running',
  COMPLETED: 'completed',
  FAILED: 'failed',
  CANCELLED: 'cancelled',
  TIMEDOUT: 'timedout',
} as const;

export type TaskStatus = typeof TASK_STATUS[keyof typeof TASK_STATUS];

/**
 * Task status helper utilities.
 */
export const TaskStatusHelpers = {
  /**
   * Check if a task status indicates the task is finished (terminal state).
   */
  isTerminal: (status: string): boolean => {
    const terminalStatuses: readonly string[] = [TASK_STATUS.COMPLETED, TASK_STATUS.FAILED, TASK_STATUS.CANCELLED, TASK_STATUS.TIMEDOUT];
    return terminalStatuses.includes(status);
  },

  /**
   * Check if a task status indicates the task is still active.
   */
  isActive: (status: string): boolean => {
    const activeStatuses: readonly string[] = [TASK_STATUS.PENDING, TASK_STATUS.RUNNING];
    return activeStatuses.includes(status);
  },

  /**
   * Check if a task status indicates success.
   */
  isSuccessful: (status: string): boolean =>
    status === TASK_STATUS.COMPLETED,

  /**
   * Check if a task status indicates failure.
   */
  isFailed: (status: string): boolean => {
    const failedStatuses: readonly string[] = [TASK_STATUS.FAILED, TASK_STATUS.CANCELLED, TASK_STATUS.TIMEDOUT];
    return failedStatuses.includes(status);
  },

  /**
   * Get all terminal status values.
   */
  getTerminalStatuses: (): readonly TaskStatus[] =>
    [TASK_STATUS.COMPLETED, TASK_STATUS.FAILED, TASK_STATUS.CANCELLED, TASK_STATUS.TIMEDOUT],

  /**
   * Get all active status values.
   */
  getActiveStatuses: (): readonly TaskStatus[] =>
    [TASK_STATUS.PENDING, TASK_STATUS.RUNNING],
} as const;

/**
 * Polling configuration constants.
 */
export const POLLING_CONFIG = {
  DEFAULT_INTERVAL: 2000,     // 2 seconds
  DEFAULT_TIMEOUT: 600000,    // 10 minutes
  MAX_INTERVAL: 30000,        // 30 seconds
  MIN_INTERVAL: 500,          // 0.5 seconds
  BACKOFF_FACTOR: 1.5,        // Exponential backoff multiplier
} as const;

/**
 * Task type constants.
 */
export const TASK_TYPES = {
  IMAGE_GENERATION: 'image_generation',
  VIDEO_GENERATION: 'video_generation',
  AUDIO_TRANSCRIPTION: 'audio_transcription',
  AUDIO_TRANSLATION: 'audio_translation',
  BATCH_OPERATION: 'batch_operation',
} as const;

export type TaskType = typeof TASK_TYPES[keyof typeof TASK_TYPES];

// ============================================================================
// Validation Constants
// ============================================================================

/**
 * Chat message role constants.
 */
export const CHAT_ROLES = {
  SYSTEM: 'system',
  USER: 'user',
  ASSISTANT: 'assistant',
  TOOL: 'tool',
} as const;

export type ChatRole = typeof CHAT_ROLES[keyof typeof CHAT_ROLES];

/**
 * Chat role validation helpers.
 */
export const ChatRoleHelpers = {
  /**
   * Get all valid chat roles.
   */
  getAllRoles: (): readonly ChatRole[] => Object.values(CHAT_ROLES),

  /**
   * Check if a role is valid.
   */
  isValidRole: (role: string): role is ChatRole =>
    Object.values(CHAT_ROLES).includes(role as ChatRole),

  /**
   * Check if a role requires a tool call ID.
   */
  requiresToolCallId: (role: string): boolean =>
    role === CHAT_ROLES.TOOL,

  /**
   * Validate a role and throw an error if invalid.
   */
  validateRole: (role: string): asserts role is ChatRole => {
    if (!ChatRoleHelpers.isValidRole(role)) {
      throw new Error(`Invalid chat role: ${role}. Valid roles are: ${Object.values(CHAT_ROLES).join(', ')}`);
    }
  },
} as const;

/**
 * Image response format constants.
 */
export const IMAGE_RESPONSE_FORMATS = {
  URL: 'url',
  BASE64_JSON: 'b64_json',
} as const;

export type ImageResponseFormat = typeof IMAGE_RESPONSE_FORMATS[keyof typeof IMAGE_RESPONSE_FORMATS];

/**
 * Image quality constants.
 */
export const IMAGE_QUALITY = {
  STANDARD: 'standard',
  HD: 'hd',
} as const;

export type ImageQuality = typeof IMAGE_QUALITY[keyof typeof IMAGE_QUALITY];

/**
 * Image style constants.
 */
export const IMAGE_STYLE = {
  VIVID: 'vivid',
  NATURAL: 'natural',
} as const;

export type ImageStyle = typeof IMAGE_STYLE[keyof typeof IMAGE_STYLE];

/**
 * Image size constants.
 */
export const IMAGE_SIZES = {
  SMALL: '256x256',
  MEDIUM: '512x512',
  LARGE: '1024x1024',
  WIDE: '1792x1024',
  TALL: '1024x1792',
} as const;

export type ImageSize = typeof IMAGE_SIZES[keyof typeof IMAGE_SIZES];

/**
 * Timeout configuration constants.
 * These values are used to configure request timeouts for different types of operations.
 */
export const TIMEOUT_CONFIG = {
  /** Default timeout for general API requests (60 seconds) */
  DEFAULT: 60000,
  /** Timeout for image generation requests (5 minutes) - image providers like Replicate can take several minutes */
  IMAGE_GENERATION: 300000,
  /** Timeout for video generation requests (10 minutes) - video generation typically takes longer */
  VIDEO_GENERATION: 600000,
  /** Timeout for audio operations (5 minutes) */
  AUDIO: 300000,
  /** Timeout for streaming requests (10 minutes) */
  STREAMING: 600000,
} as const;

export type TimeoutConfigKey = keyof typeof TIMEOUT_CONFIG;

/**
 * Image validation helpers.
 */
export const ImageValidationHelpers = {
  /**
   * Check if response format is valid.
   */
  isValidResponseFormat: (format: string): format is ImageResponseFormat =>
    Object.values(IMAGE_RESPONSE_FORMATS).includes(format as ImageResponseFormat),

  /**
   * Check if quality is valid.
   */
  isValidQuality: (quality: string): quality is ImageQuality =>
    Object.values(IMAGE_QUALITY).includes(quality as ImageQuality),

  /**
   * Check if style is valid.
   */
  isValidStyle: (style: string): style is ImageStyle =>
    Object.values(IMAGE_STYLE).includes(style as ImageStyle),

  /**
   * Check if size is valid.
   */
  isValidSize: (size: string): size is ImageSize =>
    Object.values(IMAGE_SIZES).includes(size as ImageSize),

  /**
   * Get all valid response formats.
   */
  getAllResponseFormats: (): readonly ImageResponseFormat[] => Object.values(IMAGE_RESPONSE_FORMATS),

  /**
   * Get all valid qualities.
   */
  getAllQualities: (): readonly ImageQuality[] => Object.values(IMAGE_QUALITY),

  /**
   * Get all valid styles.
   */
  getAllStyles: (): readonly ImageStyle[] => Object.values(IMAGE_STYLE),

  /**
   * Get all valid sizes.
   */
  getAllSizes: (): readonly ImageSize[] => Object.values(IMAGE_SIZES),
} as const;
