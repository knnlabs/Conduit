/**
 * SSE Error Handler for OpenAI-compatible error responses
 */

import type { OpenAIErrorResponse } from '@/constants/errorMessages';
import { parseApiError, type AppError } from '@/utils/errorHandling';

/**
 * Check if SSE data contains an OpenAI error
 */
export function isOpenAIError(data: unknown): data is OpenAIErrorResponse {
  if (!data || typeof data !== 'object') {
    return false;
  }
  
  const obj = data as Record<string, unknown>;
  
  // Check for OpenAI error format
  if ('error' in obj && obj.error && typeof obj.error === 'object') {
    const error = obj.error as Record<string, unknown>;
    return 'message' in error && typeof error.message === 'string';
  }
  
  return false;
}

/**
 * Parse SSE error data into AppError
 */
export function parseSSEError(data: unknown): AppError | null {
  if (!isOpenAIError(data)) {
    return null;
  }
  
  // Create a mock response with appropriate status code
  const error = data.error;
  let status = 500;
  
  // Infer status from error code or type
  if (error.code === 'model_not_found') {
    status = 404;
  } else if (error.code === 'invalid_request' || error.code === 'invalid_parameter') {
    status = 400;
  } else if (error.code === 'authentication_error') {
    status = 401;
  } else if (error.code === 'insufficient_balance') {
    status = 402;
  } else if (error.code === 'permission_denied') {
    status = 403;
  } else if (error.code === 'rate_limit_exceeded') {
    status = 429;
  } else if (error.type === 'invalid_request_error') {
    status = 400;
  } else if (error.type === 'authentication_error') {
    status = 401;
  } else if (error.type === 'permission_error') {
    status = 403;
  } else if (error.type === 'not_found_error') {
    status = 404;
  } else if (error.type === 'rate_limit_error') {
    status = 429;
  }
  
  // Create mock response
  const mockResponse = {
    status,
    headers: new Headers(),
  } as Response;
  
  return parseApiError(mockResponse, data as unknown);
}

/**
 * Enhanced SSE event processor that detects errors
 */
export interface ProcessedSSEEvent {
  type: 'content' | 'error' | 'metrics' | 'done';
  data?: unknown;
  error?: AppError;
}

export function processSSEEvent(eventData: unknown): ProcessedSSEEvent {
  // Check for [DONE] marker
  if (eventData === '[DONE]') {
    return { type: 'done' };
  }
  
  // Check for OpenAI error
  const error = parseSSEError(eventData);
  if (error) {
    return { type: 'error', error };
  }
  
  // Check for metrics event (if data has metrics field)
  if (eventData && typeof eventData === 'object' && 'metrics' in eventData) {
    return { type: 'metrics', data: eventData };
  }
  
  // Regular content event
  return { type: 'content', data: eventData };
}

/**
 * Handle SSE connection errors
 */
export function handleSSEConnectionError(error: unknown): AppError {
  // Network errors
  if (error instanceof TypeError && error.message.includes('fetch')) {
    return {
      status: 503,
      code: 'network_error',
      title: 'Connection Failed',
      message: 'Unable to connect to the server. Please check your network connection.',
      isRecoverable: true,
      suggestions: [
        'Check your internet connection',
        'Try refreshing the page',
        'Check if the service is online',
      ],
      severity: 'error',
      iconName: 'ServerIcon',
    };
  }
  
  // Abort errors
  if (error instanceof DOMException && error.name === 'AbortError') {
    return {
      status: 499,
      code: 'request_cancelled',
      title: 'Request Cancelled',
      message: 'The request was cancelled.',
      isRecoverable: false,
      suggestions: [],
      severity: 'info',
      iconName: 'ExclamationCircleIcon',
    };
  }
  
  // Generic error
  return {
    status: 500,
    code: 'streaming_error',
    title: 'Streaming Error',
    message: error instanceof Error ? error.message : 'An error occurred during streaming',
    isRecoverable: true,
    suggestions: [
      'Try again',
      'Refresh the page if the issue persists',
    ],
    severity: 'error',
    iconName: 'ExclamationCircleIcon',
  };
}