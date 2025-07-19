// Re-export only error types and utilities from the Common package
export {
  // Error classes
  ConduitError,
  AuthError,
  AuthenticationError,
  ValidationError,
  NotFoundError,
  RateLimitError,
  ServerError,
  NetworkError,
  TimeoutError,
  StreamError,
  
  // Type guards
  isConduitError,
  isAuthError,
  isValidationError,
  isNotFoundError,
  isRateLimitError,
  isNetworkError,
  isStreamError,
  isTimeoutError,
  isSerializedConduitError,
  
  // Utility functions
  serializeError,
  deserializeError,
  getErrorMessage,
  getErrorStatusCode,
  
  // For Core SDK specific use - removed unused imports
} from '@knn_labs/conduit-common';

// Note: All error utilities and types are re-exported from the common package above