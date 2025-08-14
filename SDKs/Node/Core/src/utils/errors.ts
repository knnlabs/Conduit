// Re-export error types and utilities from the Common package
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
  
  // Utility functions
  handleApiError,
  serializeError,
  deserializeError,
  getErrorMessage,
  getErrorStatusCode,
  
  // For Core SDK specific use - removed unused imports
} from '@knn_labs/conduit-common';

// Core SDK specific error types not in Common package
import { ConduitError } from '@knn_labs/conduit-common';

/**
 * Error thrown when a request fails due to insufficient balance
 */
export class InsufficientBalanceError extends ConduitError {
  constructor(message: string = 'Insufficient balance', details?: Record<string, unknown>) {
    super(message, 402, 'INSUFFICIENT_BALANCE', details);
    this.name = 'InsufficientBalanceError';
  }
}

/**
 * Type guard to check if an error is an InsufficientBalanceError
 */
export function isInsufficientBalanceError(error: unknown): error is InsufficientBalanceError {
  return error instanceof InsufficientBalanceError || 
         (error instanceof Error && error.name === 'InsufficientBalanceError') ||
         (typeof error === 'object' && error !== null && 
          'statusCode' in error && (error as { statusCode: number }).statusCode === 402);
}