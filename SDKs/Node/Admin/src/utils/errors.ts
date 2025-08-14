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
  serializeError,
  deserializeError,
  getErrorMessage,
  getErrorStatusCode,
  handleApiError,
  createErrorFromResponse,
  
  // Types
  type ErrorResponseFormat
} from '@knn_labs/conduit-common';