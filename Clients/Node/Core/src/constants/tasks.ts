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
  isTerminal: (status: string): boolean => 
    [TASK_STATUS.COMPLETED, TASK_STATUS.FAILED, TASK_STATUS.CANCELLED, TASK_STATUS.TIMEDOUT]
      .includes(status as TaskStatus),
  
  /**
   * Check if a task status indicates the task is still active.
   */
  isActive: (status: string): boolean => 
    [TASK_STATUS.PENDING, TASK_STATUS.RUNNING].includes(status as TaskStatus),

  /**
   * Check if a task status indicates success.
   */
  isSuccessful: (status: string): boolean => 
    status === TASK_STATUS.COMPLETED,

  /**
   * Check if a task status indicates failure.
   */
  isFailed: (status: string): boolean => 
    [TASK_STATUS.FAILED, TASK_STATUS.CANCELLED, TASK_STATUS.TIMEDOUT].includes(status as TaskStatus),

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