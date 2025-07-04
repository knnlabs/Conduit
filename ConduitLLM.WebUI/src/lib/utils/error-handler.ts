import { notifications } from '@mantine/notifications';

/**
 * Global error handler for unhandled errors
 */
export function setupGlobalErrorHandler() {
  // Handle unhandled promise rejections
  if (typeof window !== 'undefined') {
    window.addEventListener('unhandledrejection', (event) => {
      console.error('Unhandled promise rejection:', event.reason);
      
      // Show notification for user-facing errors
      if (event.reason instanceof Error) {
        const error = event.reason;
        const isNetworkError = error.message.toLowerCase().includes('network') || 
                              error.message.toLowerCase().includes('fetch');
        const isAuthError = error.message.toLowerCase().includes('unauthorized') ||
                            error.message.toLowerCase().includes('authentication');

        if (isNetworkError) {
          notifications.show({
            title: 'Connection Error',
            message: 'Unable to connect to the server. Please check your connection.',
            color: 'red',
          });
        } else if (isAuthError) {
          notifications.show({
            title: 'Authentication Error',
            message: 'Your session may have expired. Please try logging in again.',
            color: 'red',
          });
        } else if (!error.message.includes('QueryErrorResetBoundary')) {
          // Don't show notifications for React Query boundary resets
          notifications.show({
            title: 'An error occurred',
            message: error.message || 'Something went wrong. Please try again.',
            color: 'red',
          });
        }
      }
      
      // Prevent the default browser error handling
      event.preventDefault();
    });

    // Handle uncaught errors
    window.addEventListener('error', (event) => {
      console.error('Uncaught error:', event.error);
      
      // Log to error tracking service in production
      if (process.env.NODE_ENV === 'production') {
        // TODO: Send to error tracking service
        console.error('Production error:', {
          message: event.message,
          source: event.filename,
          lineno: event.lineno,
          colno: event.colno,
          error: event.error,
        });
      }
    });
  }
}

/**
 * Error serializer for logging
 */
export function serializeError(error: unknown): Record<string, unknown> {
  if (error instanceof Error) {
    const { name, message, stack, ...rest } = error as Error & Record<string, unknown>;
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
export function isRecoverableError(error: unknown): boolean {
  if (!(error instanceof Error)) return false;
  
  const message = error.message.toLowerCase();
  
  // Network errors are usually recoverable
  if (message.includes('network') || message.includes('fetch')) {
    return true;
  }
  
  // Rate limit errors are recoverable after waiting
  if (message.includes('rate limit') || message.includes('too many requests')) {
    return true;
  }
  
  // Timeout errors are recoverable
  if (message.includes('timeout')) {
    return true;
  }
  
  return false;
}

/**
 * Format error message for display
 */
export function formatErrorMessage(error: unknown): string {
  if (error instanceof Error) {
    // Special handling for common errors
    const message = error.message.toLowerCase();
    
    if (message.includes('network')) {
      return 'Unable to connect to the server. Please check your internet connection.';
    }
    
    if (message.includes('unauthorized') || message.includes('401')) {
      return 'Your session has expired. Please log in again.';
    }
    
    if (message.includes('forbidden') || message.includes('403')) {
      return 'You do not have permission to perform this action.';
    }
    
    if (message.includes('not found') || message.includes('404')) {
      return 'The requested resource was not found.';
    }
    
    if (message.includes('rate limit')) {
      return 'Too many requests. Please try again in a few moments.';
    }
    
    if (message.includes('timeout')) {
      return 'The request took too long. Please try again.';
    }
    
    // Return the original message if no special handling
    return error.message;
  }
  
  return 'An unexpected error occurred. Please try again.';
}