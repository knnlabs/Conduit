/**
 * Enhanced error handling utilities for OpenAI-compatible error responses
 */

import { HttpError } from '@knn_labs/conduit-admin-client';
import { 
  OpenAIError, 
  OpenAIErrorResponse, 
  getErrorConfig,
  getErrorIconName,
  getErrorSeverity
} from '@/constants/errorMessages';

export interface AppError {
  status: number;
  code: string;
  message: string;
  title: string;
  isRecoverable: boolean;
  suggestions: string[];
  retryAfter?: number;
  severity: 'error' | 'warning' | 'info';
  iconName: string;
  originalError?: OpenAIError;
}

/**
 * Parse an API error response into a user-friendly AppError
 */
export function parseApiError(response: Response | undefined, body?: unknown): AppError {
  const status = response?.status ?? 500;
  
  // Try to extract OpenAI error format
  const openAIError = extractOpenAIError(body);
  
  // Get error configuration for this status code
  const config = getErrorConfig(status);
  
  // Extract retry-after header if present
  const retryAfterHeader = response?.headers?.get?.('Retry-After');
  const retryAfter = retryAfterHeader ? parseInt(retryAfterHeader, 10) : undefined;
  
  return {
    status,
    code: openAIError?.code ?? 'unknown_error',
    title: config.getTitle(),
    message: config.getMessage(openAIError),
    isRecoverable: config.isRecoverable,
    suggestions: config.getSuggestions(openAIError),
    retryAfter,
    severity: getErrorSeverity(status),
    iconName: getErrorIconName(status),
    originalError: openAIError,
  };
}

/**
 * Extract OpenAI error object from response body
 */
function extractOpenAIError(body: unknown): OpenAIError | undefined {
  if (!body || typeof body !== 'object') {
    return undefined;
  }
  
  // Check for OpenAI error format
  if ('error' in body) {
    const errorObj = (body as OpenAIErrorResponse).error;
    if (errorObj && typeof errorObj === 'object' && 'message' in errorObj) {
      return errorObj;
    }
  }
  
  return undefined;
}

/**
 * Parse an SDK error (HttpError) into an AppError
 */
export function parseSDKError(error: unknown): AppError {
  // Handle HttpError from SDK
  if (error instanceof HttpError) {
    const status = error.response?.status ?? 500;
    const body = error.response?.data;
    
    // Try to parse as OpenAI error
    const openAIError = extractOpenAIError(body);
    if (openAIError) {
      // Create a mock Response object for parseApiError
      const mockResponse = new Response(null, { 
        status,
        headers: new Headers()
      });
      return parseApiError(mockResponse, body);
    }
    
    // Fallback to basic error handling
    const config = getErrorConfig(status);
    return {
      status,
      code: 'sdk_error',
      title: config.getTitle(),
      message: error.message ?? config.getMessage(undefined),
      isRecoverable: config.isRecoverable,
      suggestions: config.getSuggestions(undefined),
      severity: getErrorSeverity(status),
      iconName: getErrorIconName(status),
    };
  }
  
  // Handle standard JavaScript errors
  if (error instanceof Error) {
    return {
      status: 500,
      code: 'client_error',
      title: 'Application Error',
      message: error.message,
      isRecoverable: false,
      suggestions: ['Refresh the page', 'Contact support if the issue persists'],
      severity: 'error',
      iconName: 'ExclamationCircleIcon',
    };
  }
  
  // Unknown error type
  return {
    status: 500,
    code: 'unknown_error',
    title: 'Unknown Error',
    message: 'An unexpected error occurred',
    isRecoverable: false,
    suggestions: ['Try again', 'Contact support'],
    severity: 'error',
    iconName: 'ExclamationCircleIcon',
  };
}

/**
 * Handle streaming SSE errors
 */
export function parseStreamingError(event: MessageEvent): AppError | null {
  try {
    const data = JSON.parse(event.data as string) as unknown;
    
    // Check if it's an error event
    if (data && typeof data === 'object' && 'error' in data) {
      const openAIError = extractOpenAIError(data);
      if (openAIError) {
        // Determine status code from error type
        const status = inferStatusFromError(openAIError);
        return parseApiError({ status } as Response, data);
      }
    }
  } catch {
    // Not a JSON error event
  }
  
  return null;
}

/**
 * Infer HTTP status code from OpenAI error object
 */
function inferStatusFromError(error: OpenAIError): number {
  // Map error codes to likely status codes
  const codeToStatus: Record<string, number> = {
    'model_not_found': 404,
    'invalid_request': 400,
    'invalid_parameter': 400,
    'missing_parameter': 400,
    'authentication_error': 401,
    'insufficient_balance': 402,
    'permission_denied': 403,
    'not_found': 404,
    'rate_limit_exceeded': 429,
    'server_error': 500,
    'service_unavailable': 503,
  };
  
  if (error.code && error.code in codeToStatus) {
    return codeToStatus[error.code];
  }
  
  // Map error types to status codes
  const typeToStatus: Record<string, number> = {
    'invalid_request_error': 400,
    'authentication_error': 401,
    'permission_error': 403,
    'not_found_error': 404,
    'rate_limit_error': 429,
    'server_error': 500,
  };
  
  if (error.type && error.type in typeToStatus) {
    return typeToStatus[error.type];
  }
  
  return 500; // Default to server error
}

/**
 * Determine if an error is retryable
 */
export function isRetryableError(error: AppError): boolean {
  // Retry on rate limits and server errors
  if (error.status === 429 || error.status >= 500) {
    return true;
  }
  
  // Retry on timeout errors
  if (error.status === 408 || error.status === 504) {
    return true;
  }
  
  return false;
}

/**
 * Calculate retry delay based on error
 */
export function getRetryDelay(error: AppError, attemptNumber: number): number {
  // Use retry-after if available
  if (error.retryAfter) {
    return error.retryAfter * 1000; // Convert to milliseconds
  }
  
  // Exponential backoff for other retryable errors
  const baseDelay = 1000; // 1 second
  const maxDelay = 30000; // 30 seconds
  const delay = Math.min(baseDelay * Math.pow(2, attemptNumber - 1), maxDelay);
  
  // Add jitter to prevent thundering herd
  const jitter = Math.random() * 200;
  
  return delay + jitter;
}

/**
 * Format error for display in console (development only)
 */
export function formatErrorForConsole(error: AppError): void {
  if (process.env.NODE_ENV !== 'development') {
    return;
  }
  
  console.warn(`🚨 ${error.title} (${error.status})`);
  console.warn('Message:', error.message);
  console.warn('Code:', error.code);
  if (error.suggestions.length > 0) {
    console.warn('Suggestions:', error.suggestions);
  }
  if (error.retryAfter) {
    console.warn('Retry after:', error.retryAfter, 'seconds');
  }
  if (error.originalError) {
    console.warn('Original error:', error.originalError);
  }
}

/**
 * Create a user-friendly error message for notifications
 */
export function getNotificationMessage(error: AppError): string {
  if (error.retryAfter) {
    return `${error.message} (retry in ${error.retryAfter}s)`;
  }
  return error.message;
}