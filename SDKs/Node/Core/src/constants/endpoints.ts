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
      // Note: The following endpoints are not yet implemented in Core API
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