// Import shared HTTP constants from Common package
import { 
  HTTP_HEADERS as COMMON_HTTP_HEADERS,
  CONTENT_TYPES as COMMON_CONTENT_TYPES,
  HTTP_STATUS,
  ERROR_CODES,
  TIMEOUTS,
  RETRY_CONFIG,
  HttpMethod
} from '@knn_labs/conduit-common';

// Re-export for backward compatibility
export { 
  HTTP_STATUS, 
  ERROR_CODES, 
  TIMEOUTS, 
  RETRY_CONFIG,
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

// Re-export types from other modules to satisfy imports in constants/index.ts
export type { TaskStatus, TaskType } from './tasks';
export type { ChatRole, ImageResponseFormat, ImageQuality, ImageStyle, ImageSize } from './validation';
export type { StreamEvent } from './streaming';

/**
 * Client information constants.
 */
export const CLIENT_INFO = {
  NAME: '@conduit/core',
  VERSION: '0.1.0', // Could be imported from package.json
  USER_AGENT: '@conduit/core/0.1.0',
} as const;