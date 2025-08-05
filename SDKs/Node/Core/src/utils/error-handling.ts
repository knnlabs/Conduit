/**
 * Enhanced error handling utilities for Conduit Core SDK
 * Provides automatic error detection and user-friendly error messages
 */

import { 
  ConduitError, 
  InsufficientBalanceError, 
  AuthError, 
  ValidationError, 
  RateLimitError, 
  ServerError,
  NetworkError,
  isInsufficientBalanceError,
  isAuthError,
  isValidationError,
  isRateLimitError,
  handleApiError 
} from './errors';

/**
 * Enhanced fetch wrapper that automatically handles Conduit API errors
 * and provides user-friendly error messages
 */
export async function conduitFetch(
  url: string, 
  options: RequestInit = {},
  context?: { operation?: string }
): Promise<Response> {
  try {
    const response = await fetch(url, options);
    
    if (!response.ok) {
      // Let the SDK's handleApiError function process this
      // This will throw the appropriate ConduitError subclass
      const errorBody = await response.text();
      let errorData: unknown;
      
      try {
        errorData = JSON.parse(errorBody);
      } catch {
        errorData = { error: response.statusText };
      }
      
      // Create a mock HTTP error that handleApiError can process
      const httpError = {
        response: {
          status: response.status,
          data: errorData,
          headers: Object.fromEntries(response.headers.entries())
        },
        message: response.statusText,
        request: { url, method: options.method ?? 'GET' }
      };
      
      handleApiError(httpError, url, options.method);
    }
    
    return response;
  } catch (error) {
    // If it's already a ConduitError, just re-throw it
    if (error instanceof ConduitError) {
      throw error;
    }
    
    // Handle network errors
    if (error instanceof TypeError && error.message.includes('fetch')) {
      throw new NetworkError(`Network error: Unable to connect to ${url}`, { 
        originalError: error,
        url,
        operation: context?.operation 
      });
    }
    
    // Handle other unexpected errors
    throw new ConduitError(
      error instanceof Error ? error.message : 'Unknown error occurred',
      500,
      'UNKNOWN_ERROR',
      { originalError: error, url, operation: context?.operation }
    );
  }
}

/**
 * User-friendly error messages for different error types
 */
export function getErrorDisplayMessage(error: unknown, context?: string): string {
  if (isInsufficientBalanceError(error)) {
    return `💳 Insufficient balance to ${context || 'complete this request'}. Please add credits to your account.`;
  }
  
  if (isAuthError(error)) {
    return '🔐 Authentication failed. Please check your API key or login status.';
  }
  
  if (isValidationError(error)) {
    return `⚠️ Invalid request: ${error.message}`;
  }
  
  if (isRateLimitError(error)) {
    const retryAfter = error.retryAfter ? ` Try again in ${error.retryAfter} seconds.` : '';
    return `🐌 Rate limit exceeded.${retryAfter}`;
  }
  
  if (error instanceof ServerError) {
    return `🔧 Server error occurred. ${context ? `Failed to ${context}.` : ''} Please try again later.`;
  }
  
  if (error instanceof NetworkError) {
    return `🌐 Network error. Please check your internet connection and try again.`;
  }
  
  if (error instanceof ConduitError) {
    return error.message;
  }
  
  if (error instanceof Error) {
    return error.message;
  }
  
  return 'An unexpected error occurred. Please try again.';
}

/**
 * Get the appropriate toast notification color for an error
 */
export function getErrorToastColor(error: unknown): 'red' | 'yellow' | 'orange' {
  if (isInsufficientBalanceError(error)) {
    return 'orange'; // Payment-related, not necessarily a "failure"
  }
  
  if (isValidationError(error) || isRateLimitError(error)) {
    return 'yellow'; // User can fix these
  }
  
  return 'red'; // System errors, auth failures, etc.
}

/**
 * Enhanced error handler that provides context-aware error handling
 */
export interface ErrorHandlerOptions {
  /** Context describing what operation failed */
  operation?: string;
  /** Whether to show a toast notification (if toast system is available) */
  showToast?: boolean;
  /** Custom toast notification function */
  onToast?: (message: string, options: { color: string; title: string }) => void;
  /** Whether to log the error to console */
  logError?: boolean;
  /** Custom error logger */
  onLog?: (error: unknown, context?: string) => void;
}

/**
 * Comprehensive error handler that can be used across the WebUI
 */
export function handleConduitError(error: unknown, options: ErrorHandlerOptions = {}): string {
  const {
    operation,
    showToast = false,
    onToast,
    logError = true,
    onLog
  } = options;
  
  // Log the error
  if (logError) {
    if (onLog) {
      onLog(error, operation);
    } else {
      console.error(`Error${operation ? ` during ${operation}` : ''}:`, error);
    }
  }
  
  // Get user-friendly message
  const displayMessage = getErrorDisplayMessage(error, operation);
  
  // Show toast notification if requested
  if (showToast && onToast) {
    const color = getErrorToastColor(error);
    const title = isInsufficientBalanceError(error) 
      ? 'Insufficient Balance'
      : isAuthError(error)
      ? 'Authentication Error'
      : isValidationError(error)
      ? 'Validation Error'
      : isRateLimitError(error)
      ? 'Rate Limit Exceeded'
      : 'Error';
    
    onToast(displayMessage, { color, title });
  }
  
  return displayMessage;
}

/**
 * React hook-friendly error handler that integrates with Mantine notifications
 */
export function createToastErrorHandler(showNotification: (options: {
  title: string;
  message: string;
  color: string;
}) => void) {
  return (error: unknown, operation?: string) => {
    return handleConduitError(error, {
      operation,
      showToast: true,
      onToast: (message, { color, title }) => {
        showNotification({ title, message, color });
      }
    });
  };
}

/**
 * Specific error handling for different API operations
 */
export const ErrorHandlers = {
  chatCompletion: (error: unknown) => handleConduitError(error, { 
    operation: 'send chat message',
    logError: true 
  }),
  
  imageGeneration: (error: unknown) => handleConduitError(error, { 
    operation: 'generate images',
    logError: true 
  }),
  
  videoGeneration: (error: unknown) => handleConduitError(error, { 
    operation: 'generate video',
    logError: true 
  }),
  
  audioTranscription: (error: unknown) => handleConduitError(error, { 
    operation: 'transcribe audio',
    logError: true 
  }),
  
  embeddings: (error: unknown) => handleConduitError(error, { 
    operation: 'generate embeddings',
    logError: true 
  }),
  
  modelList: (error: unknown) => handleConduitError(error, { 
    operation: 'load models',
    logError: true 
  })
} as const;

/**
 * Type guard to check if an error should trigger a balance notification
 */
export function shouldShowBalanceWarning(error: unknown): boolean {
  return isInsufficientBalanceError(error);
}

/**
 * Type guard to check if an error is retryable
 */
export function isRetryableError(error: unknown): boolean {
  // Network errors and temporary server errors are retryable
  return error instanceof NetworkError || 
         (error instanceof ServerError && error.statusCode >= 502);
}

/**
 * Get suggested actions for different error types
 */
export function getErrorActions(error: unknown): string[] {
  if (isInsufficientBalanceError(error)) {
    return [
      'Add credits to your account',
      'Check your current balance',
      'Contact support if you believe this is an error'
    ];
  }
  
  if (isAuthError(error)) {
    return [
      'Check your API key configuration',
      'Verify you are logged in',
      'Try refreshing the page'
    ];
  }
  
  if (isRateLimitError(error)) {
    return [
      'Wait a moment before trying again',
      'Consider upgrading your plan for higher limits',
      'Reduce the frequency of your requests'
    ];
  }
  
  if (error instanceof NetworkError) {
    return [
      'Check your internet connection',
      'Try refreshing the page',
      'Contact support if the problem persists'
    ];
  }
  
  return ['Try again in a moment', 'Contact support if the problem persists'];
}