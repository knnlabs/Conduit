import { NextResponse } from 'next/server';
import { logger } from '@/lib/utils/logging';
import { HttpError } from '@knn_labs/conduit-admin-client';
import { 
  getErrorStatusCode, 
  getErrorMessage, 
  getCombinedErrorDetails, 
  isHttpError 
} from '@/lib/utils/error-utils';

// Map SDK errors to appropriate HTTP responses
export function handleSDKError(error: unknown): NextResponse {
  const errorMessage = getErrorMessage(error);
  const statusCode = getErrorStatusCode(error);
  const errorType = statusCode ? String(statusCode) : 'unknown';
  const errorStack = error instanceof Error ? error.stack : undefined;
  
  const errorInfo = {
    error: errorMessage,
    type: errorType,
    stack: errorStack,
  };
  logger.error('SDK operation failed', errorInfo);

  // Handle HttpError from the SDK
  if (isHttpError(error)) {
    const errorDetails = getCombinedErrorDetails(error);
    const responseStatusCode = getErrorStatusCode(error) ?? 500;

    return NextResponse.json(
      {
        error: errorMessage,
        ...(process.env.NODE_ENV === 'development' && { 
          details: errorDetails,
          statusCode: responseStatusCode
        })
      },
      { status: responseStatusCode }
    );
  }

  // Handle non-SDK errors (e.g., network errors from fetch)
  const errorCode = error && typeof error === 'object' && 'code' in error 
    ? (error as { code: string }).code 
    : null;
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

// Re-export HttpError for convenience
export { HttpError };
// For backward compatibility with code expecting ConduitError
export { HttpError as ConduitError };