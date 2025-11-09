import { notifications } from '@mantine/notifications';
import { 
  getErrorDisplayMessage,
  isNetworkError,
  isAuthError,
  isRateLimitError,
  ConduitError
} from '@knn_labs/conduit-core-client';

/**
 * Global error handler for unhandled errors
 */
export function setupGlobalErrorHandler() {
  // Handle unhandled promise rejections
  if (typeof window !== 'undefined') {
    window.addEventListener('unhandledrejection', (event) => {
      const reason = event.reason as unknown; // Browser rejection reason can be any type
      const reasonMessage = reason instanceof Error ? reason.message : undefined;
      const reasonStack = reason instanceof Error ? reason.stack : undefined;
      
      console.error('Unhandled promise rejection:', {
        reason,
        message: reasonMessage,
        stack: reasonStack,
        timestamp: new Date().toISOString(),
        url: window.location.href,
        type: event.type,
        promise: event.promise,
      });
      
      // Show notification for user-facing errors
      if (event.reason instanceof Error) {
        const error = event.reason;
        
        // Use SDK's type checking instead of string matching
        if (isNetworkError(error)) {
          notifications.show({
            title: 'Connection Error',
            message: getErrorDisplayMessage(error) || 'Unable to connect to the server. Please check your connection.',
            color: 'red',
          });
        } else if (isAuthError(error)) {
          notifications.show({
            title: 'Authentication Error',
            message: getErrorDisplayMessage(error) || 'Your session may have expired. Please try logging in again.',
            color: 'red',
          });
        } else if (!error.message.includes('QueryErrorResetBoundary')) {
          // Don't show notifications for React Query boundary resets
          // Use SDK's error message formatting for all ConduitErrors
          const message = error instanceof ConduitError 
            ? getErrorDisplayMessage(error) 
            : (error.message || 'Something went wrong. Please try again.');
          
          notifications.show({
            title: 'An error occurred',
            message,
            color: 'red',
          });
        }
      }
      
      // Prevent the default browser error handling
      event.preventDefault();
    });

    // Handle uncaught errors
    window.addEventListener('error', (event) => {
      const errorObj = event.error as unknown; // Browser error object can be any type
      const errorStack = errorObj instanceof Error ? errorObj.stack : undefined;
      
      console.error('Uncaught error:', {
        message: event.message,
        filename: event.filename,
        lineno: event.lineno,
        colno: event.colno,
        error: errorObj,
        stack: errorStack,
        timestamp: new Date().toISOString(),
        url: window.location.href,
      });
      
      // Log to error tracking service in production
      if (process.env.NODE_ENV === 'production') {
        // TODO: Send to error tracking service
        console.error('Production error:', {
          message: event.message,
          source: event.filename,
          lineno: event.lineno,
          colno: event.colno,
          error: errorObj,
        });
      }
    });
  }
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