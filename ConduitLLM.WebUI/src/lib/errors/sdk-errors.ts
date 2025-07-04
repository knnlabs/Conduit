import { NextResponse } from 'next/server';
import { logger } from '@/lib/utils/logging';

// SDK Error Types
export enum SDKErrorType {
  NETWORK = 'NETWORK',
  AUTHENTICATION = 'AUTHENTICATION',
  AUTHORIZATION = 'AUTHORIZATION',
  VALIDATION = 'VALIDATION',
  RATE_LIMIT = 'RATE_LIMIT',
  NOT_FOUND = 'NOT_FOUND',
  CONFLICT = 'CONFLICT',
  SERVER_ERROR = 'SERVER_ERROR',
  TIMEOUT = 'TIMEOUT',
  UNKNOWN = 'UNKNOWN',
}

// SDK Error class with enhanced context
export class SDKError extends Error {
  constructor(
    public type: SDKErrorType,
    message: string,
    public statusCode: number,
    public context?: unknown,
    public originalError?: unknown
  ) {
    super(message);
    this.name = 'SDKError';
  }
}

// Map SDK errors to appropriate HTTP responses
export function mapSDKErrorToResponse(error: unknown): NextResponse {
  const errorInfo = {
    error: error instanceof Error ? error.message : 'Unknown error',
    type: error && typeof error === 'object' && 'type' in error ? error.type : 'unknown',
    context: error && typeof error === 'object' && 'context' in error ? error.context : undefined,
    stack: error instanceof Error ? error.stack : undefined,
  };
  logger.error('SDK operation failed', errorInfo);

  // Handle SDK-specific errors
  if (error instanceof SDKError) {
    return NextResponse.json(
      {
        error: {
          type: error.type,
          message: error.message,
          ...(process.env.NODE_ENV === 'development' && { context: error.context }),
        },
      },
      { status: error.statusCode }
    );
  }

  // Handle network errors
  const errorCode = error && typeof error === 'object' && 'code' in error ? error.code : null;
  if (errorCode === 'ECONNREFUSED' || errorCode === 'ENOTFOUND') {
    return NextResponse.json(
      {
        error: {
          type: SDKErrorType.NETWORK,
          message: 'Service temporarily unavailable. Please try again later.',
        },
      },
      { status: 503 }
    );
  }

  // Handle timeout errors
  const errorMessage = error instanceof Error ? error.message : '';
  if (errorCode === 'ETIMEDOUT' || errorMessage.includes('timeout')) {
    return NextResponse.json(
      {
        error: {
          type: SDKErrorType.TIMEOUT,
          message: 'Request timed out. Please try again.',
        },
      },
      { status: 504 }
    );
  }

  // Handle Conduit API errors (from SDK)
  const response = error && typeof error === 'object' && 'response' in error ? error.response : null;
  if (response && typeof response === 'object') {
    const status = 'status' in response ? (response.status as number) || 500 : 500;
    const data = 'data' in response ? response.data || {} : {};

    switch (status) {
      case 400:
        return NextResponse.json(
          {
            error: {
              type: SDKErrorType.VALIDATION,
              message: data.error || 'Invalid request parameters',
              details: data.details,
            },
          },
          { status: 400 }
        );

      case 401:
        return NextResponse.json(
          {
            error: {
              type: SDKErrorType.AUTHENTICATION,
              message: 'Authentication failed. Please check your credentials.',
            },
          },
          { status: 401 }
        );

      case 403:
        return NextResponse.json(
          {
            error: {
              type: SDKErrorType.AUTHORIZATION,
              message: 'You do not have permission to perform this action.',
            },
          },
          { status: 403 }
        );

      case 404:
        return NextResponse.json(
          {
            error: {
              type: SDKErrorType.NOT_FOUND,
              message: data.error || 'Resource not found',
            },
          },
          { status: 404 }
        );

      case 409:
        return NextResponse.json(
          {
            error: {
              type: SDKErrorType.CONFLICT,
              message: data.error || 'Resource conflict',
            },
          },
          { status: 409 }
        );

      case 429:
        return NextResponse.json(
          {
            error: {
              type: SDKErrorType.RATE_LIMIT,
              message: 'Rate limit exceeded. Please slow down your requests.',
              retryAfter: error.response.headers?.['retry-after'],
            },
          },
          { status: 429 }
        );

      default:
        if (status >= 500) {
          return NextResponse.json(
            {
              error: {
                type: SDKErrorType.SERVER_ERROR,
                message: 'An internal error occurred. Please try again later.',
              },
            },
            { status: 500 }
          );
        }
    }
  }

  // Default error response
  return NextResponse.json(
    {
      error: {
        type: SDKErrorType.UNKNOWN,
        message: 'An unexpected error occurred',
        ...(process.env.NODE_ENV === 'development' && { 
          originalError: error instanceof Error ? error.message : 'Unknown error',
          stack: error instanceof Error ? error.stack : undefined,
        }),
      },
    },
    { status: 500 }
  );
}

// Wrap SDK operations with consistent error handling
export async function withSDKErrorHandling<T>(
  operation: () => Promise<T>,
  context: string
): Promise<T> {
  try {
    return await operation();
  } catch (error: unknown) {
    logger.error(`SDK operation failed: ${context}`, { error });

    // Enhance error with context
    const response = error && typeof error === 'object' && 'response' in error ? error.response : null;
    if (response && typeof response === 'object' && 'status' in response) {
      const status = response.status as number;
      const data = 'data' in response ? response.data : null;
      const dataError = data && typeof data === 'object' && 'error' in data ? data.error : null;
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      
      throw new SDKError(
        getErrorType(status),
        dataError || errorMessage,
        status,
        { operation: context },
        error
      );
    }

    // Network or timeout errors
    const errorCode = error && typeof error === 'object' && 'code' in error ? error.code : null;
    if (errorCode === 'ECONNREFUSED' || errorCode === 'ENOTFOUND') {
      throw new SDKError(
        SDKErrorType.NETWORK,
        'Service unavailable',
        503,
        { operation: context },
        error
      );
    }

    if (errorCode === 'ETIMEDOUT') {
      throw new SDKError(
        SDKErrorType.TIMEOUT,
        'Operation timed out',
        504,
        { operation: context },
        error
      );
    }

    // Re-throw SDK errors
    if (error instanceof SDKError) {
      throw error;
    }

    // Unknown errors
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    throw new SDKError(
      SDKErrorType.UNKNOWN,
      errorMessage,
      500,
      { operation: context },
      error
    );
  }
}

// Helper to determine error type from status code
function getErrorType(status: number): SDKErrorType {
  switch (status) {
    case 400: return SDKErrorType.VALIDATION;
    case 401: return SDKErrorType.AUTHENTICATION;
    case 403: return SDKErrorType.AUTHORIZATION;
    case 404: return SDKErrorType.NOT_FOUND;
    case 409: return SDKErrorType.CONFLICT;
    case 429: return SDKErrorType.RATE_LIMIT;
    default:
      if (status >= 500) return SDKErrorType.SERVER_ERROR;
      return SDKErrorType.UNKNOWN;
  }
}

// Create user-friendly error messages
export function getUserFriendlyError(error: SDKError): string {
  switch (error.type) {
    case SDKErrorType.NETWORK:
      return 'Unable to connect to the service. Please check your connection and try again.';
    case SDKErrorType.AUTHENTICATION:
      return 'Your session has expired. Please log in again.';
    case SDKErrorType.AUTHORIZATION:
      return 'You don\'t have permission to perform this action.';
    case SDKErrorType.VALIDATION:
      return error.message || 'Please check your input and try again.';
    case SDKErrorType.RATE_LIMIT:
      return 'You\'re making requests too quickly. Please slow down.';
    case SDKErrorType.NOT_FOUND:
      return 'The requested resource was not found.';
    case SDKErrorType.CONFLICT:
      return 'This action conflicts with existing data.';
    case SDKErrorType.SERVER_ERROR:
      return 'Something went wrong on our end. Please try again later.';
    case SDKErrorType.TIMEOUT:
      return 'The request took too long. Please try again.';
    default:
      return 'An unexpected error occurred. Please try again.';
  }
}

// Retry logic for transient failures
export async function withRetry<T>(
  operation: () => Promise<T>,
  options: {
    maxRetries?: number;
    retryDelay?: number;
    shouldRetry?: (error: unknown) => boolean;
  } = {}
): Promise<T> {
  const { 
    maxRetries = 3, 
    retryDelay = 1000,
    shouldRetry = (error: unknown) => {
      // Retry on network errors and 5xx errors
      const errorCode = error && typeof error === 'object' && 'code' in error ? error.code : null;
      if (errorCode === 'ECONNREFUSED' || errorCode === 'ETIMEDOUT') return true;
      
      const response = error && typeof error === 'object' && 'response' in error ? error.response : null;
      const status = response && typeof response === 'object' && 'status' in response ? response.status as number : null;
      if (status && status >= 500) return true;
      
      return false;
    }
  } = options;

  let lastError: unknown;
  
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await operation();
    } catch (error: unknown) {
      lastError = error;
      
      if (!shouldRetry(error) || i === maxRetries - 1) {
        throw error;
      }
      
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      logger.warn(`Retrying operation (attempt ${i + 2}/${maxRetries})`, {
        error: errorMessage,
      });
      
      // Exponential backoff
      await new Promise(resolve => setTimeout(resolve, retryDelay * Math.pow(2, i)));
    }
  }
  
  throw lastError;
}