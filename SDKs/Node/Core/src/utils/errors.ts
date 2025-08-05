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
  InsufficientBalanceError,
  RateLimitError,
  ServerError,
  NetworkError,
  TimeoutError,
  StreamError,
  
  // Type guards
  isConduitError,
  isAuthError,
  isAuthorizationError,
  isValidationError,
  isNotFoundError,
  isConflictError,
  isInsufficientBalanceError,
  isRateLimitError,
  isNetworkError,
  isStreamError,
  isTimeoutError,
  isSerializedConduitError,
  
  // Utility functions
  handleApiError,
  serializeError,
  deserializeError,
  getErrorMessage,
  getErrorStatusCode,
  
  // For Core SDK specific use - removed unused imports
} from '@knn_labs/conduit-common';

// Note: All error utilities and types are re-exported from the common package above