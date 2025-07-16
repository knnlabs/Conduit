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
  
  // For Core SDK specific use
  createErrorFromResponse,
  type ErrorResponseFormat
} from '@knn_labs/conduit-common';

// Import error classes from Common package for use in legacy function
import { ConduitError, createErrorFromResponse, type ErrorResponseFormat } from '@knn_labs/conduit-common';

// Legacy compatibility - keep the old import style for createErrorFromResponse
import type { ErrorResponse } from '../models/common';

// Re-export createErrorFromResponse with the old signature for backward compatibility
export function createErrorFromResponseLegacy(response: ErrorResponse, statusCode?: number): ConduitError {
  const errorResponseFormat: ErrorResponseFormat = {
    error: {
      message: response.error.message,
      type: response.error.type,
      code: response.error.code || undefined,
      param: response.error.param || undefined
    }
  };
  return createErrorFromResponse(errorResponseFormat, statusCode);
}