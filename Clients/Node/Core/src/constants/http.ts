/**
 * HTTP method constants for type-safe method specification.
 */
export const HTTP_METHODS = {
  GET: 'GET',
  POST: 'POST',
  PUT: 'PUT',
  DELETE: 'DELETE',
  PATCH: 'PATCH',
} as const;

export type HttpMethod = typeof HTTP_METHODS[keyof typeof HTTP_METHODS];

/**
 * HTTP header name constants.
 */
export const HTTP_HEADERS = {
  CONTENT_TYPE: 'Content-Type',
  AUTHORIZATION: 'Authorization',
  X_API_KEY: 'X-API-Key',
  USER_AGENT: 'User-Agent',
  X_CORRELATION_ID: 'X-Correlation-Id',
  RETRY_AFTER: 'retry-after',
  ACCEPT: 'Accept',
  CACHE_CONTROL: 'Cache-Control',
} as const;

/**
 * Content type constants.
 */
export const CONTENT_TYPES = {
  JSON: 'application/json',
  FORM_DATA: 'multipart/form-data',
  FORM_URLENCODED: 'application/x-www-form-urlencoded',
  TEXT_PLAIN: 'text/plain',
  TEXT_STREAM: 'text/plain; charset=utf-8',
} as const;

/**
 * HTTP status code constants for common cases.
 */
export const HTTP_STATUS = {
  OK: 200,
  CREATED: 201,
  NO_CONTENT: 204,
  BAD_REQUEST: 400,
  UNAUTHORIZED: 401,
  FORBIDDEN: 403,
  NOT_FOUND: 404,
  CONFLICT: 409,
  TOO_MANY_REQUESTS: 429,
  INTERNAL_SERVER_ERROR: 500,
  BAD_GATEWAY: 502,
  SERVICE_UNAVAILABLE: 503,
  GATEWAY_TIMEOUT: 504,
} as const;

/**
 * Network error code constants.
 */
export const ERROR_CODES = {
  CONNECTION_ABORTED: 'ECONNABORTED',
  TIMEOUT: 'ETIMEDOUT',
  CONNECTION_RESET: 'ECONNRESET',
  NETWORK_UNREACHABLE: 'ENETUNREACH',
  CONNECTION_REFUSED: 'ECONNREFUSED',
  HOST_NOT_FOUND: 'ENOTFOUND',
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

/**
 * Timeout constants in milliseconds.
 */
export const TIMEOUTS = {
  DEFAULT_REQUEST: 60000, // 60 seconds
  SHORT_REQUEST: 10000,   // 10 seconds
  LONG_REQUEST: 300000,   // 5 minutes
  STREAMING: 0,           // No timeout for streams
} as const;

/**
 * Retry configuration constants.
 */
export const RETRY_CONFIG = {
  DEFAULT_MAX_RETRIES: 3,
  INITIAL_DELAY: 1000,
  MAX_DELAY: 30000,
  BACKOFF_FACTOR: 2,
} as const;