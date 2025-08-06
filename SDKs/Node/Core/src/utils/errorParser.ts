/**
 * Error parsing utilities for OpenAI-compatible error responses
 */

import { 
  ConduitError,
  AuthError,
  AuthorizationError,
  ValidationError,
  NotFoundError,
  RateLimitError,
  ServerError,
  TimeoutError,
  InsufficientBalanceError
} from '@knn_labs/conduit-common';

import {
  ModelNotFoundException,
  InvalidRequestException,
  ServiceUnavailableException,
  PayloadTooLargeException
} from '../errors';

/**
 * OpenAI error response format
 */
export interface OpenAIErrorResponse {
  error: {
    message: string;
    type: string;
    code?: string;
    param?: string;
  };
}

/**
 * Parse an error response and return the appropriate ConduitError subclass
 */
export function parseErrorResponse(response: Response, body: unknown): ConduitError {
  const status = response.status;
  
  // Try to extract error details from the response body
  let errorData: OpenAIErrorResponse | null = null;
  
  if (body && typeof body === 'object' && 'error' in body) {
    errorData = body as OpenAIErrorResponse;
  }
  
  const error = errorData?.error;
  const message = error?.message ?? 'An error occurred';
  const code = error?.code;
  const param = error?.param;
  
  // Map status codes and error codes to specific error types
  switch (status) {
    case 400:
      // Check for specific error codes
      if (code === 'missing_parameter' || code === 'invalid_parameter' || code === 'invalid_request') {
        return new InvalidRequestException(message, code, param);
      }
      return new ValidationError(message, { code, param });
      
    case 401:
      return new AuthError(message, { code, param });
      
    case 402:
      return new InsufficientBalanceError(message, { code, param });
      
    case 403:
      return new AuthorizationError(message, { code, param });
      
    case 404:
      // Check for model_not_found specifically
      if (code === 'model_not_found' && param) {
        return new ModelNotFoundException(param, message);
      }
      return new NotFoundError(message, { code, param });
      
    case 408:
      return new TimeoutError(message, { code, param });
      
    case 413:
      return new PayloadTooLargeException(message);
      
    case 429: {
      const retryAfterHeader = response.headers.get('Retry-After');
      const retryAfter = retryAfterHeader ? parseInt(retryAfterHeader, 10) : undefined;
      return new RateLimitError(message, retryAfter, { code, param });
    }
      
    case 503: {
      // Extract service name from error details if available
      const serviceName = (body as Record<string, unknown>)?.service as string ?? undefined;
      const retryAfter = response.headers.get('Retry-After');
      return new ServiceUnavailableException(
        message, 
        serviceName, 
        retryAfter ? parseInt(retryAfter, 10) : undefined
      );
    }
      
    case 500:
    case 502:
    case 504:
      return new ServerError(message, { code, param, status });
      
    default:
      if (status >= 500) {
        return new ServerError(message, { code, param, status });
      }
      return new ConduitError(message, status, code ?? 'unknown_error', { param });
  }
}

/**
 * Determine if an error should be retried
 */
export function shouldRetry(error: ConduitError): boolean {
  // Don't retry client errors (except rate limits)
  if (error.statusCode >= 400 && error.statusCode < 500) {
    return error.statusCode === 429; // Only retry rate limits
  }
  
  // Retry server errors with exponential backoff
  if (error.statusCode >= 500) {
    return true;
  }
  
  // Retry network errors and timeouts
  return error.code === 'NETWORK_ERROR' || error.code === 'TIMEOUT_ERROR';
}

/**
 * Calculate retry delay based on error type and attempt number
 */
export function getRetryDelay(error: ConduitError, attempt: number): number {
  // Use retry-after header if available (for rate limits)
  if (error instanceof RateLimitError && error.retryAfter) {
    return error.retryAfter * 1000; // Convert to milliseconds
  }
  
  // Use retry-after for service unavailable
  if (error instanceof ServiceUnavailableException && error.retryAfterSeconds) {
    return error.retryAfterSeconds * 1000;
  }
  
  // Exponential backoff: 1s, 2s, 4s, 8s, 16s (max)
  const baseDelay = 1000;
  const maxDelay = 16000;
  const delay = Math.min(baseDelay * Math.pow(2, attempt - 1), maxDelay);
  
  // Add jitter to prevent thundering herd
  const jitter = Math.random() * 200;
  
  return delay + jitter;
}

/**
 * Create an error recovery suggestion based on error type
 */
export function getErrorRecoverySuggestion(error: ConduitError): string | undefined {
  if (error instanceof ModelNotFoundException) {
    return 'Check available models using the /v1/models endpoint';
  }
  
  if (error instanceof InvalidRequestException) {
    if (error.param) {
      return `Check the '${error.param}' parameter in your request`;
    }
    return 'Review your request parameters';
  }
  
  if (error instanceof PayloadTooLargeException) {
    return 'Reduce the size of your request payload';
  }
  
  if (error instanceof ServiceUnavailableException) {
    return 'The service is temporarily unavailable. Please try again later.';
  }
  
  if (error instanceof RateLimitError) {
    if (error.retryAfter) {
      return `Rate limit exceeded. Retry after ${error.retryAfter} seconds.`;
    }
    return 'Rate limit exceeded. Please slow down your requests.';
  }
  
  if (error instanceof InsufficientBalanceError) {
    return 'Your account balance is insufficient. Please add credits.';
  }
  
  return undefined;
}