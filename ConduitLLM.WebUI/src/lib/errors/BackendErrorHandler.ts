export enum BackendErrorType {
  CONNECTION_FAILED = 'CONNECTION_FAILED',
  SERVICE_UNAVAILABLE = 'SERVICE_UNAVAILABLE',
  AUTHENTICATION_FAILED = 'AUTHENTICATION_FAILED',
  RATE_LIMITED = 'RATE_LIMITED',
  INVALID_REQUEST = 'INVALID_REQUEST',
  INTERNAL_ERROR = 'INTERNAL_ERROR',
  TIMEOUT = 'TIMEOUT',
}

export interface BackendError {
  type: BackendErrorType;
  message: string;
  retryable: boolean;
  retryAfter?: number; // seconds
  details?: any;
}

export class BackendErrorHandler {
  static classifyError(error: any): BackendError {
    // Network/Connection errors
    if (error.name === 'TypeError' && error.message?.includes('fetch')) {
      return {
        type: BackendErrorType.CONNECTION_FAILED,
        message: 'Unable to connect to the server. Please check your internet connection.',
        retryable: true,
        retryAfter: 5,
      };
    }

    // HTTP status-based errors
    if (error.status) {
      switch (error.status) {
        case 401:
          return {
            type: BackendErrorType.AUTHENTICATION_FAILED,
            message: 'Authentication failed. Please log in again.',
            retryable: false,
          };

        case 403:
          return {
            type: BackendErrorType.AUTHENTICATION_FAILED,
            message: 'Access denied. Please check your permissions.',
            retryable: false,
          };

        case 429:
          return {
            type: BackendErrorType.RATE_LIMITED,
            message: 'Too many requests. Please wait before trying again.',
            retryable: true,
            retryAfter: 60,
          };

        case 500:
        case 502:
        case 503:
          return {
            type: BackendErrorType.SERVICE_UNAVAILABLE,
            message: 'The service is temporarily unavailable. Please try again later.',
            retryable: true,
            retryAfter: 30,
          };

        case 504:
          return {
            type: BackendErrorType.TIMEOUT,
            message: 'The request timed out. The service may be experiencing high load.',
            retryable: true,
            retryAfter: 15,
          };

        case 400:
          return {
            type: BackendErrorType.INVALID_REQUEST,
            message: error.message || 'Invalid request. Please check your input.',
            retryable: false,
          };

        default:
          return {
            type: BackendErrorType.INTERNAL_ERROR,
            message: error.message || 'An unexpected error occurred.',
            retryable: false,
          };
      }
    }

    // Timeout errors
    if (error.name === 'AbortError' || error.message?.includes('timeout')) {
      return {
        type: BackendErrorType.TIMEOUT,
        message: 'The request timed out. Please try again.',
        retryable: true,
        retryAfter: 10,
      };
    }

    // Default fallback
    return {
      type: BackendErrorType.INTERNAL_ERROR,
      message: error.message || 'An unexpected error occurred.',
      retryable: false,
      details: error,
    };
  }

  static shouldRetry(error: BackendError, attemptCount: number, maxRetries: number = 3): boolean {
    return error.retryable && attemptCount < maxRetries;
  }

  static getRetryDelay(error: BackendError, attemptCount: number): number {
    const baseDelay = error.retryAfter || 5;
    // Exponential backoff with jitter
    const delay = baseDelay * Math.pow(2, attemptCount - 1);
    const jitter = Math.random() * 0.1 * delay;
    return (delay + jitter) * 1000; // Convert to milliseconds
  }

  static getUserFriendlyMessage(error: BackendError): string {
    switch (error.type) {
      case BackendErrorType.CONNECTION_FAILED:
        return 'Connection lost. Trying to reconnect...';
      
      case BackendErrorType.SERVICE_UNAVAILABLE:
        return 'Service temporarily unavailable. Please try again in a few moments.';
      
      case BackendErrorType.AUTHENTICATION_FAILED:
        return 'Please log in again to continue.';
      
      case BackendErrorType.RATE_LIMITED:
        return 'Please wait a moment before making another request.';
      
      case BackendErrorType.TIMEOUT:
        return 'Request timed out. The service may be busy.';
      
      default:
        return error.message;
    }
  }

  static getActionableMessage(error: BackendError): string {
    switch (error.type) {
      case BackendErrorType.CONNECTION_FAILED:
        return 'Check your internet connection and try again.';
      
      case BackendErrorType.SERVICE_UNAVAILABLE:
        return 'The service is being updated. Please try again shortly.';
      
      case BackendErrorType.AUTHENTICATION_FAILED:
        return 'Click here to log in again.';
      
      case BackendErrorType.RATE_LIMITED:
        return `Please wait ${error.retryAfter || 60} seconds before trying again.`;
      
      case BackendErrorType.TIMEOUT:
        return 'Try reducing the complexity of your request or try again later.';
      
      default:
        return 'If this problem persists, please contact support.';
    }
  }

  static getRetryConfig(maxRetries: number = 3) {
    return {
      retry: (failureCount: number, error: any) => {
        const backendError = error.type ? error : BackendErrorHandler.classifyError(error);
        return BackendErrorHandler.shouldRetry(backendError, failureCount, maxRetries);
      },
      retryDelay: (attemptIndex: number, error: any) => {
        const backendError = error.type ? error : BackendErrorHandler.classifyError(error);
        return BackendErrorHandler.getRetryDelay(backendError, attemptIndex);
      },
    };
  }
}