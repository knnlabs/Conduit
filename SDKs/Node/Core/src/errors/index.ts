/**
 * Core SDK specific error types
 * 
 * Re-exports common errors and adds SDK-specific error types
 */

// Re-export common error types
export {
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
  StreamError,
  InsufficientBalanceError,
  
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
  isInsufficientBalanceError,
  isSerializedConduitError,
  
  // Utility functions
  handleApiError,
  serializeError,
  deserializeError,
  getErrorMessage,
  getErrorStatusCode,
} from '@knn_labs/conduit-common';

// Export new specific error types
export { ModelNotFoundException, isModelNotFoundException } from './ModelNotFoundException';
export { InvalidRequestException, isInvalidRequestException } from './InvalidRequestException';
export { ServiceUnavailableException, isServiceUnavailableException } from './ServiceUnavailableException';
export { PayloadTooLargeException, isPayloadTooLargeException } from './PayloadTooLargeException';