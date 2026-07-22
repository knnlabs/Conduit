import { notify } from '@/lib/notifications';
import {
  getErrorDisplayMessage,
  isNetworkError,
  isAuthError,
  isRateLimitError,
  ConduitError
} from '@/lib/gateway-api';
import { reportError } from './logging';

/**
 * Global error handler for unhandled errors
 */
export function setupGlobalErrorHandler(): () => void {
  // Handle unhandled promise rejections
  if (typeof window !== 'undefined') {
    const handleUnhandledRejection = (event: PromiseRejectionEvent) => {
      const reason = event.reason as unknown; // Browser rejection reason can be any type
      reportError(reason, 'unhandled-promise-rejection', { eventType: event.type });
      
      // Show notification for user-facing errors
      if (event.reason instanceof Error) {
        const error = event.reason;
        
        // Use SDK's type checking instead of string matching
        if (isNetworkError(error)) {
          notify.error(getErrorDisplayMessage(error) || 'Unable to connect to the server. Please check your connection.');
        } else if (isAuthError(error)) {
          notify.error(getErrorDisplayMessage(error) || 'Your session may have expired. Please try logging in again.');
        } else if (!error.message.includes('QueryErrorResetBoundary')) {
          // Don't show notifications for React Query boundary resets
          // Use SDK's error message formatting for all ConduitErrors
          const message = error instanceof ConduitError 
            ? getErrorDisplayMessage(error) 
            : (error.message || 'Something went wrong. Please try again.');
          
          notify.error(message);
        }
      }
      
      // Prevent the default browser error handling
      event.preventDefault();
    };

    // Handle uncaught errors
    const handleError = (event: ErrorEvent) => {
      const errorObj = event.error as unknown; // Browser error object can be any type
      reportError(
        errorObj instanceof Error ? errorObj : new Error(event.message),
        'uncaught-browser-error',
        {
          source: event.filename,
          line: event.lineno,
          column: event.colno,
        },
      );
    };

    window.addEventListener('unhandledrejection', handleUnhandledRejection);
    window.addEventListener('error', handleError);

    return () => {
      window.removeEventListener('unhandledrejection', handleUnhandledRejection);
      window.removeEventListener('error', handleError);
    };
  }

  return () => undefined;
}

/**
 * Error serializer for logging
 */
export function serializeError(error: unknown): Record<string, unknown> { // Generic error serializer for any error type
  if (error instanceof Error) {
    const { name, message, stack, ...rest } = error as Error & Record<string, unknown>; // Capture custom error properties
    return {
      name,
      message,
      stack,
      ...rest, // Include any custom properties
    };
  }
  
  return {
    type: typeof error,
    value: String(error),
  };
}

/**
 * Check if an error is recoverable
 */
export function isRecoverableError(error: unknown): boolean { // Check if any error type is recoverable
  if (!(error instanceof Error)) return false;
  
  // Use SDK's type checking for proper error classification
  if (isNetworkError(error)) {
    return true;
  }
  
  // Rate limit errors are recoverable after waiting
  if (isRateLimitError(error)) {
    return true;
  }
  
  // Timeout errors are recoverable
  if (error instanceof Error && error.message.toLowerCase().includes('timeout')) {
    return true;
  }
  
  return false;
}

/**
 * Format error message for display
 */
export function formatErrorMessage(error: unknown): string { // Format any error type for user display
  // Use SDK's error formatting for all ConduitErrors
  if (error instanceof ConduitError) {
    return getErrorDisplayMessage(error);
  }
  
  if (error instanceof Error) {
    // For non-Conduit errors, return the message as-is
    return error.message;
  }
  
  return 'An unexpected error occurred. Please try again.';
}
