import { NextResponse } from 'next/server';
import { logger } from '@/lib/utils/logging';
import { 
  ConduitError
} from '@knn_labs/conduit-admin-client';

// Map SDK errors to appropriate HTTP responses
export function handleSDKError(error: unknown): NextResponse {
  const errorInfo = {
    error: error instanceof Error ? error.message : 'Unknown error',
    type: error instanceof ConduitError ? error.statusCode : 'unknown',
    stack: error instanceof Error ? error.stack : undefined,
  };
  logger.error('SDK operation failed', errorInfo);

  // Handle ConduitError from the SDK
  if (error instanceof ConduitError) {
    // Use the error message and details from ConduitError
    let errorMessage = error.message || 'An error occurred';
    let errorDetails: Record<string, unknown> = {};
    
    // ConduitError has details and context properties
    if (error.details) {
      errorDetails = typeof error.details === 'object' ? error.details as Record<string, unknown> : { details: error.details };
    }
    if (error.context) {
      errorDetails = { ...errorDetails, ...error.context };
    }

    return NextResponse.json(
      {
        error: errorMessage,
        ...(process.env.NODE_ENV === 'development' && { 
          details: errorDetails,
          statusCode: error.statusCode
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

// Re-export ConduitError for convenience
export { ConduitError };
// For backward compatibility with code expecting HttpError
export { ConduitError as HttpError };