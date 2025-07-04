import { safeLog } from './logging';
import { notifications } from '@mantine/notifications';

export function setupGlobalErrorHandlers() {
  // Handle unhandled promise rejections
  if (typeof window !== 'undefined') {
    window.addEventListener('unhandledrejection', (event) => {
      safeLog('Unhandled promise rejection', {
        reason: event.reason,
        promise: event.promise,
      });

      // Show user notification for critical errors
      if (event.reason?.message) {
        notifications.show({
          title: 'Unexpected Error',
          message: 'An unexpected error occurred. Please refresh the page.',
          color: 'red',
        });
      }

      // Prevent default browser error handling
      event.preventDefault();
    });

    // Handle global errors
    window.addEventListener('error', (event) => {
      safeLog('Global error', {
        message: event.message,
        filename: event.filename,
        lineno: event.lineno,
        colno: event.colno,
        error: event.error,
      });

      // Don't show notification for script errors from external sources
      if (!event.filename?.includes(window.location.host)) {
        return;
      }

      notifications.show({
        title: 'Application Error',
        message: 'Something went wrong. Please try again.',
        color: 'red',
      });
    });
  }
}

export function reportError(error: Error, context?: Record<string, unknown>) {
  // Log to console in development
  safeLog('Error reported', {
    error: {
      message: error.message,
      stack: error.stack,
      name: error.name,
    },
    context,
  });

  // In production, this would send to an error tracking service
  // Example: Sentry, LogRocket, etc.
  if (process.env.NODE_ENV === 'production') {
    // TODO: Implement error reporting service integration
    // sendToErrorService(error, context);
  }
}