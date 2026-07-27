// Re-export only error types and utilities from the Common package
export {
  // Error classes
  ConduitError,
  AuthError,
  AuthenticationError,
  AuthorizationError,
  ValidationError,
  NotFoundError,
  ConflictError,
  RateLimitError,
  ServerError,
  NetworkError,
  TimeoutError,
  NotImplementedError,
  StreamError,

  // Type guards
  isConduitError,
  isAuthError,
  isAuthorizationError,
  isValidationError,
  isNotFoundError,
  isConflictError,
  isRateLimitError,
  isNetworkError,
  isStreamError,
  isTimeoutError,
  isSerializedConduitError,
  isHttpError,
  isHttpNetworkError,
  isErrorLike,

  // Utility functions
  serializeConduitError,
  deserializeError,
  getErrorMessage,
  getErrorStatusCode,
  throwApiError,
  createErrorFromResponse,

  // Types
  type ErrorResponseFormat
} from '@/lib/conduit-common';
