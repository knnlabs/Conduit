/**
 * Common HTTP constants shared across all SDKs
 */

/**
 * HTTP headers used across SDKs
 */
export const HTTP_HEADERS = {
  CONTENT_TYPE: 'Content-Type',
  AUTHORIZATION: 'Authorization',
  X_API_KEY: 'X-API-Key',
  USER_AGENT: 'User-Agent',
  X_CORRELATION_ID: 'X-Correlation-Id',
  RETRY_AFTER: 'Retry-After',
  ACCEPT: 'Accept',
  CACHE_CONTROL: 'Cache-Control'
} as const;

export type HttpHeader = typeof HTTP_HEADERS[keyof typeof HTTP_HEADERS];

/**
 * Content types
 */
export const CONTENT_TYPES = {
  JSON: 'application/json',
  FORM_DATA: 'multipart/form-data',
  FORM_URLENCODED: 'application/x-www-form-urlencoded',
  TEXT_PLAIN: 'text/plain',
  TEXT_STREAM: 'text/event-stream'
} as const;

export type ContentType = typeof CONTENT_TYPES[keyof typeof CONTENT_TYPES];

/**
 * HTTP status codes
 */
export const HTTP_STATUS = {
  // 2xx Success
  OK: 200,
  CREATED: 201,
  NO_CONTENT: 204,
  
  // 4xx Client Errors
  BAD_REQUEST: 400,
  UNAUTHORIZED: 401,
  FORBIDDEN: 403,
  NOT_FOUND: 404,
  CONFLICT: 409,
  TOO_MANY_REQUESTS: 429,
  RATE_LIMITED: 429, // Alias for Core SDK compatibility
  
  // 5xx Server Errors
  INTERNAL_SERVER_ERROR: 500,
  INTERNAL_ERROR: 500, // Alias for Admin SDK compatibility
  BAD_GATEWAY: 502,
  SERVICE_UNAVAILABLE: 503,
  GATEWAY_TIMEOUT: 504
} as const;

export type HttpStatusCode = typeof HTTP_STATUS[keyof typeof HTTP_STATUS];

/**
 * Error codes for network errors
 */
export const ERROR_CODES = {
  CONNECTION_ABORTED: 'ECONNABORTED',
  TIMEOUT: 'ETIMEDOUT',
  CONNECTION_RESET: 'ECONNRESET',
  NETWORK_UNREACHABLE: 'ENETUNREACH',
  CONNECTION_REFUSED: 'ECONNREFUSED',
  HOST_NOT_FOUND: 'ENOTFOUND'
} as const;

export type ErrorCode = typeof ERROR_CODES[keyof typeof ERROR_CODES];

/**
 * Default timeout values in milliseconds
 */
export const TIMEOUTS = {
  DEFAULT_REQUEST: 60000, // 60 seconds
  SHORT_REQUEST: 10000,   // 10 seconds
  LONG_REQUEST: 300000,   // 5 minutes
  STREAMING: 0            // No timeout for streaming
} as const;

export type TimeoutValue = typeof TIMEOUTS[keyof typeof TIMEOUTS];

/**
 * Retry configuration defaults
 */
export const RETRY_CONFIG = {
  DEFAULT_MAX_RETRIES: 3,
  INITIAL_DELAY: 1000,    // 1 second
  MAX_DELAY: 30000,       // 30 seconds
  BACKOFF_FACTOR: 2
} as const;

export type RetryConfigValue = typeof RETRY_CONFIG[keyof typeof RETRY_CONFIG];