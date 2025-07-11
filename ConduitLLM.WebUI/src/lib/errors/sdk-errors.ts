import { NextResponse } from 'next/server';
import { logger } from '@/lib/utils/logging';
import { 
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
  isConduitError,
  isAuthError,
  isValidationError,
  isNotFoundError,
  isRateLimitError,
  isNetworkError
} from '@knn_labs/conduit-admin-client';

// Map SDK errors to appropriate HTTP responses
export function handleSDKError(error: unknown): NextResponse {
  const errorInfo = {
    error: error instanceof Error ? error.message : 'Unknown error',
    type: error instanceof ConduitError ? error.code : 'unknown',
    context: error instanceof ConduitError ? error.context : undefined,
    stack: error instanceof Error ? error.stack : undefined,
  };
  logger.error('SDK operation failed', errorInfo);

  // Handle specific SDK error types
  if (isRateLimitError(error)) {
    return NextResponse.json(
      { 
        error: error.message || 'Rate limit exceeded',
        retryAfter: error.retryAfter 
      },
      { status: 429 }
    );
  }

  if (isAuthError(error)) {
    return NextResponse.json(
      { error: error.message || 'Authentication failed' },
      { status: 401 }
    );
  }

  if (error instanceof AuthorizationError) {
    return NextResponse.json(
      { error: error.message || 'Access denied' },
      { status: 403 }
    );
  }

  if (isValidationError(error)) {
    return NextResponse.json(
      { 
        error: error.message || 'Validation failed',
        field: error.field,
        details: error.details
      },
      { status: 400 }
    );
  }

  if (isNotFoundError(error)) {
    return NextResponse.json(
      { error: error.message || 'Resource not found' },
      { status: 404 }
    );
  }

  if (error instanceof ConflictError) {
    return NextResponse.json(
      { error: error.message || 'Resource conflict' },
      { status: 409 }
    );
  }

  if (isNetworkError(error)) {
    return NextResponse.json(
      { error: error.message || 'Network error occurred' },
      { status: 503 }
    );
  }

  if (error instanceof TimeoutError) {
    return NextResponse.json(
      { error: error.message || 'Request timed out' },
      { status: 504 }
    );
  }

  if (error instanceof ServerError) {
    return NextResponse.json(
      { error: error.message || 'Internal server error' },
      { status: 500 }
    );
  }

  if (isConduitError(error)) {
    return NextResponse.json(
      { 
        error: error.message,
        code: error.code,
        ...(process.env.NODE_ENV === 'development' && { 
          context: error.context,
          details: error.details
        })
      },
      { status: error.statusCode || 500 }
    );
  }

  // Handle non-SDK errors (e.g., network errors from fetch)
  const errorCode = error && typeof error === 'object' && 'code' in error ? error.code : null;
  if (errorCode === 'ECONNREFUSED' || errorCode === 'ENOTFOUND') {
    return NextResponse.json(
      { error: 'Service temporarily unavailable' },
      { status: 503 }
    );
  }

  if (errorCode === 'ETIMEDOUT') {
    return NextResponse.json(
      { error: 'Request timed out' },
      { status: 504 }
    );
  }

  // Unknown error
  console.error('Unexpected error:', error);
  return NextResponse.json(
    { error: 'Internal server error' },
    { status: 500 }
  );
}

// Legacy alias for backward compatibility
export const mapSDKErrorToResponse = handleSDKError;

// Re-export SDK error types for convenience
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
  isConduitError,
  isAuthError,
  isValidationError,
  isNotFoundError,
  isRateLimitError,
  isNetworkError
};