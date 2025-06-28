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
      EDITS: '/v1/images/edits',
      VARIATIONS: '/v1/images/variations',
      TASK_STATUS: (taskId: string) => `/v1/images/generations/${encodeURIComponent(taskId)}/status`,
      CANCEL_TASK: (taskId: string) => `/v1/images/generations/${encodeURIComponent(taskId)}`,
    },
    VIDEOS: {
      GENERATIONS: '/v1/videos/generations',
      ASYNC_GENERATIONS: '/v1/videos/generations/async',
      TASK_STATUS: (taskId: string) => `/v1/videos/generations/${encodeURIComponent(taskId)}/status`,
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
    TASKS: {
      BASE: '/v1/tasks',
      BY_ID: (taskId: string) => `/v1/tasks/${encodeURIComponent(taskId)}`,
      CANCEL: (taskId: string) => `/v1/tasks/${encodeURIComponent(taskId)}/cancel`,
      CLEANUP: '/v1/tasks/cleanup',
    },
    BATCH: {
      BASE: '/v1/batch',
      BY_ID: (batchId: string) => `/v1/batch/${encodeURIComponent(batchId)}`,
      CANCEL: (batchId: string) => `/v1/batch/${encodeURIComponent(batchId)}/cancel`,
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
        throw new Error(`Unsupported task type: ${taskType}`);
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
        throw new Error(`Unsupported task type: ${taskType}`);
    }
  },
} as const;