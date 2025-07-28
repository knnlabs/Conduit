/**
 * Common constants shared across all Conduit SDKs
 */

/**
 * API version constants
 */
export const API_VERSION = 'v1';
export const API_PREFIX = '/api';

/**
 * Default pagination settings
 */
export const PAGINATION = {
  DEFAULT_PAGE_SIZE: 20,
  MAX_PAGE_SIZE: 100,
  DEFAULT_PAGE: 1,
} as const;

/**
 * Cache TTL values in seconds
 */
export const CACHE_TTL = {
  SHORT: 60,         // 1 minute
  MEDIUM: 300,       // 5 minutes
  LONG: 3600,        // 1 hour
  VERY_LONG: 86400,  // 24 hours
} as const;

/**
 * Task status constants
 */
export const TASK_STATUS = {
  PENDING: 'pending',
  PROCESSING: 'processing',
  COMPLETED: 'completed',
  FAILED: 'failed',
  CANCELLED: 'cancelled',
  TIMEOUT: 'timeout',
} as const;

export type TaskStatus = typeof TASK_STATUS[keyof typeof TASK_STATUS];

/**
 * Task polling configuration
 */
export const POLLING_CONFIG = {
  DEFAULT_INTERVAL: 1000,    // 1 second
  MAX_INTERVAL: 30000,       // 30 seconds
  DEFAULT_TIMEOUT: 300000,   // 5 minutes
  BACKOFF_FACTOR: 1.5,
} as const;

/**
 * Budget duration types
 */
export const BUDGET_DURATION = {
  TOTAL: 'Total',
  DAILY: 'Daily',
  WEEKLY: 'Weekly',
  MONTHLY: 'Monthly',
} as const;

export type BudgetDuration = typeof BUDGET_DURATION[keyof typeof BUDGET_DURATION];

/**
 * Filter types for IP filtering
 */
export const FILTER_TYPE = {
  ALLOW: 'whitelist',
  DENY: 'blacklist',
} as const;

export type FilterType = typeof FILTER_TYPE[keyof typeof FILTER_TYPE];

/**
 * Filter modes
 */
export const FILTER_MODE = {
  PERMISSIVE: 'permissive',
  RESTRICTIVE: 'restrictive',
} as const;

export type FilterMode = typeof FILTER_MODE[keyof typeof FILTER_MODE];

/**
 * Chat message roles
 */
export const CHAT_ROLES = {
  SYSTEM: 'system',
  USER: 'user',
  ASSISTANT: 'assistant',
  FUNCTION: 'function',
  TOOL: 'tool',
} as const;

export type ChatRole = typeof CHAT_ROLES[keyof typeof CHAT_ROLES];

/**
 * Image response formats
 */
export const IMAGE_RESPONSE_FORMATS = {
  URL: 'url',
  B64_JSON: 'b64_json',
} as const;

export type ImageResponseFormat = typeof IMAGE_RESPONSE_FORMATS[keyof typeof IMAGE_RESPONSE_FORMATS];

/**
 * Video response formats
 */
export const VIDEO_RESPONSE_FORMATS = {
  URL: 'url',
  B64_JSON: 'b64_json',
} as const;

export type VideoResponseFormat = typeof VIDEO_RESPONSE_FORMATS[keyof typeof VIDEO_RESPONSE_FORMATS];

/**
 * Common date formats
 */
export const DATE_FORMATS = {
  API_DATETIME: 'YYYY-MM-DDTHH:mm:ss[Z]',
  API_DATE: 'YYYY-MM-DD',
  DISPLAY_DATETIME: 'MMM D, YYYY [at] h:mm A',
  DISPLAY_DATE: 'MMM D, YYYY',
} as const;

/**
 * Streaming constants
 */
export const STREAM_CONSTANTS = {
  DEFAULT_BUFFER_SIZE: 64 * 1024, // 64KB
  DEFAULT_TIMEOUT: 60000,         // 60 seconds
  CHUNK_DELIMITER: '\n\n',
  DATA_PREFIX: 'data: ',
  EVENT_PREFIX: 'event: ',
  DONE_MESSAGE: '[DONE]',
} as const;

/**
 * Client identification
 */
export const CLIENT_INFO = {
  CORE_NAME: '@conduit/core',
  ADMIN_NAME: '@conduit/admin',
  VERSION: '0.2.0',
} as const;

/**
 * Health status values
 */
export const HEALTH_STATUS = {
  HEALTHY: 'healthy',
  DEGRADED: 'degraded',
  UNHEALTHY: 'unhealthy',
} as const;

export type HealthStatus = typeof HEALTH_STATUS[keyof typeof HEALTH_STATUS];

/**
 * Common regex patterns
 */
export const PATTERNS = {
  API_KEY: /^sk-[a-zA-Z0-9]{32,}$/,
  EMAIL: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
  URL: /^https?:\/\/.+$/,
  ISO_DATE: /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{3})?Z?$/,
} as const;