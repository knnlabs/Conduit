import { NextResponse } from 'next/server';
import { logger } from '@/lib/utils/logging';
import { 
  HttpError
} from '@knn_labs/conduit-admin-client';

// Map SDK errors to appropriate HTTP responses
export function handleSDKError(error: unknown): NextResponse {
  const errorInfo = {
    error: error instanceof Error ? error.message : 'Unknown error',
    type: error instanceof HttpError ? error.response?.status : 'unknown',
    stack: error instanceof Error ? error.stack : undefined,
  };
  logger.error('SDK operation failed', errorInfo);

  // Handle HttpError from the SDK
  if (error instanceof HttpError) {
    // Parse the error details if they exist
    let errorMessage = error.message || 'An error occurred';
    let errorDetails: any = {};
    
    try {
      // Try to parse the response data if it exists
      if (error.response?.data && typeof error.response.data === 'string') {
        const parsed = JSON.parse(error.response.data);
        errorMessage = parsed.message || parsed.error || errorMessage;
        errorDetails = parsed;
      } else if (error.response?.data && typeof error.response.data === 'object') {
        const data = error.response.data as any;
        errorMessage = data.message || data.error || errorMessage;
        errorDetails = data;
      }
    } catch (e) {
      // If parsing fails, use the original message
    }

    return NextResponse.json(
      {
        error: errorMessage,
        ...(process.env.NODE_ENV === 'development' && { 
          details: errorDetails,
          statusCode: error.response?.status
        })
      },
      { status: error.response?.status || 500 }
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
  HttpError
};