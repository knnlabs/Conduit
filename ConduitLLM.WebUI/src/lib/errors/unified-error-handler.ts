import { NextResponse } from 'next/server';
import { logger } from '@/lib/utils/logging';
import { notifications } from '@mantine/notifications';

/**
 * Unified error classification system
 * Consolidates all error types from BackendErrorHandler, SDKError, and ErrorClassifier
 */
export enum ErrorType {
  // Network & Connection
  NETWORK_ERROR = 'NETWORK_ERROR',
  CONNECTION_FAILED = 'CONNECTION_FAILED',
  TIMEOUT = 'TIMEOUT',
  
  // Authentication & Authorization  
  AUTHENTICATION_FAILED = 'AUTHENTICATION_FAILED',
  AUTHORIZATION_FAILED = 'AUTHORIZATION_FAILED',
  SESSION_EXPIRED = 'SESSION_EXPIRED',
  
  // Client Errors
  VALIDATION_ERROR = 'VALIDATION_ERROR',
  INVALID_REQUEST = 'INVALID_REQUEST',
  NOT_FOUND = 'NOT_FOUND',
  CONFLICT = 'CONFLICT',
  
  // Server Errors
  INTERNAL_ERROR = 'INTERNAL_ERROR',
  SERVICE_UNAVAILABLE = 'SERVICE_UNAVAILABLE',
  
  // Rate Limiting
  RATE_LIMITED = 'RATE_LIMITED',
  
  // Unknown
  UNKNOWN = 'UNKNOWN',
}

export interface ErrorContext {
  type: ErrorType;
  message: string;
  statusCode?: number;
  retryable: boolean;
  retryAfter?: number; // seconds
  severity: 'low' | 'medium' | 'high' | 'critical';
  userMessage: string; // User-friendly message
  actionableMessage: string; // What the user can do
  originalError?: unknown;
  context?: unknown;
}

/**
 * Enhanced SDK Error class
 */
export class UnifiedError extends Error {
  public readonly errorContext: ErrorContext;

  constructor(
    type: ErrorType,
    message: string,
    options: Partial<Omit<ErrorContext, 'type' | 'message'>> = {}
  ) {
    super(message);
    this.name = 'UnifiedError';
    
    this.errorContext = {
      type,
      message,
      statusCode: options.statusCode || this.getDefaultStatusCode(type),
      retryable: options.retryable ?? this.getDefaultRetryable(type),
      retryAfter: options.retryAfter || this.getDefaultRetryAfter(type),
      severity: options.severity || this.getDefaultSeverity(type),
      userMessage: options.userMessage || this.getDefaultUserMessage(type, message),
      actionableMessage: options.actionableMessage || this.getDefaultActionableMessage(type),
      originalError: options.originalError,
      context: options.context,
    };
  }

  private getDefaultStatusCode(type: ErrorType): number {
    const statusMap: Record<ErrorType, number> = {
      [ErrorType.NETWORK_ERROR]: 0,
      [ErrorType.CONNECTION_FAILED]: 0,
      [ErrorType.TIMEOUT]: 408,
      [ErrorType.AUTHENTICATION_FAILED]: 401,
      [ErrorType.AUTHORIZATION_FAILED]: 403,
      [ErrorType.SESSION_EXPIRED]: 401,
      [ErrorType.VALIDATION_ERROR]: 400,
      [ErrorType.INVALID_REQUEST]: 400,
      [ErrorType.NOT_FOUND]: 404,
      [ErrorType.CONFLICT]: 409,
      [ErrorType.INTERNAL_ERROR]: 500,
      [ErrorType.SERVICE_UNAVAILABLE]: 503,
      [ErrorType.RATE_LIMITED]: 429,
      [ErrorType.UNKNOWN]: 500,
    };
    return statusMap[type];
  }

  private getDefaultRetryable(type: ErrorType): boolean {
    const retryableTypes = [
      ErrorType.NETWORK_ERROR,
      ErrorType.CONNECTION_FAILED,
      ErrorType.TIMEOUT,
      ErrorType.SERVICE_UNAVAILABLE,
      ErrorType.RATE_LIMITED,
    ];
    return retryableTypes.includes(type);
  }

  private getDefaultRetryAfter(type: ErrorType): number {
    const retryMap: Record<ErrorType, number> = {
      [ErrorType.NETWORK_ERROR]: 5,
      [ErrorType.CONNECTION_FAILED]: 5,
      [ErrorType.TIMEOUT]: 10,
      [ErrorType.SERVICE_UNAVAILABLE]: 30,
      [ErrorType.RATE_LIMITED]: 60,
      [ErrorType.INTERNAL_ERROR]: 15,
      [ErrorType.AUTHENTICATION_FAILED]: 0,
      [ErrorType.AUTHORIZATION_FAILED]: 0,
      [ErrorType.SESSION_EXPIRED]: 0,
      [ErrorType.VALIDATION_ERROR]: 0,
      [ErrorType.INVALID_REQUEST]: 0,
      [ErrorType.NOT_FOUND]: 0,
      [ErrorType.CONFLICT]: 0,
      [ErrorType.UNKNOWN]: 5,
    };
    return retryMap[type];
  }

  private getDefaultSeverity(type: ErrorType): 'low' | 'medium' | 'high' | 'critical' {
    const severityMap: Record<ErrorType, 'low' | 'medium' | 'high' | 'critical'> = {
      [ErrorType.NETWORK_ERROR]: 'medium',
      [ErrorType.CONNECTION_FAILED]: 'high',
      [ErrorType.TIMEOUT]: 'medium',
      [ErrorType.AUTHENTICATION_FAILED]: 'high',
      [ErrorType.AUTHORIZATION_FAILED]: 'medium',
      [ErrorType.SESSION_EXPIRED]: 'medium',
      [ErrorType.VALIDATION_ERROR]: 'low',
      [ErrorType.INVALID_REQUEST]: 'low',
      [ErrorType.NOT_FOUND]: 'low',
      [ErrorType.CONFLICT]: 'medium',
      [ErrorType.INTERNAL_ERROR]: 'critical',
      [ErrorType.SERVICE_UNAVAILABLE]: 'high',
      [ErrorType.RATE_LIMITED]: 'medium',
      [ErrorType.UNKNOWN]: 'medium',
    };
    return severityMap[type];
  }

  private getDefaultUserMessage(type: ErrorType, originalMessage: string): string {
    const messageMap: Record<ErrorType, string> = {
      [ErrorType.NETWORK_ERROR]: 'Unable to connect to the server. Please check your internet connection.',
      [ErrorType.CONNECTION_FAILED]: 'Connection lost. Trying to reconnect...',
      [ErrorType.TIMEOUT]: 'The request timed out. The service may be experiencing high load.',
      [ErrorType.AUTHENTICATION_FAILED]: 'Authentication failed. Please log in again.',
      [ErrorType.AUTHORIZATION_FAILED]: 'You do not have permission to perform this action.',
      [ErrorType.SESSION_EXPIRED]: 'Your session has expired. Please log in again.',
      [ErrorType.VALIDATION_ERROR]: originalMessage || 'Invalid input. Please check your data and try again.',
      [ErrorType.INVALID_REQUEST]: originalMessage || 'Invalid request. Please check your input.',
      [ErrorType.NOT_FOUND]: 'The requested resource was not found.',
      [ErrorType.CONFLICT]: 'This action conflicts with current data. Please refresh and try again.',
      [ErrorType.INTERNAL_ERROR]: 'An unexpected error occurred. Our team has been notified.',
      [ErrorType.SERVICE_UNAVAILABLE]: 'Service temporarily unavailable. Please try again in a few moments.',
      [ErrorType.RATE_LIMITED]: 'Too many requests. Please wait a moment before trying again.',
      [ErrorType.UNKNOWN]: originalMessage || 'An unexpected error occurred. Please try again.',
    };
    return messageMap[type];
  }

  private getDefaultActionableMessage(type: ErrorType): string {
    const actionMap: Record<ErrorType, string> = {
      [ErrorType.NETWORK_ERROR]: 'Check your internet connection and try again.',
      [ErrorType.CONNECTION_FAILED]: 'Check your internet connection and try again.',
      [ErrorType.TIMEOUT]: 'Try reducing the complexity of your request or try again later.',
      [ErrorType.AUTHENTICATION_FAILED]: 'Click here to log in again.',
      [ErrorType.AUTHORIZATION_FAILED]: 'Contact your administrator for access.',
      [ErrorType.SESSION_EXPIRED]: 'Click here to log in again.',
      [ErrorType.VALIDATION_ERROR]: 'Please correct the highlighted fields and try again.',
      [ErrorType.INVALID_REQUEST]: 'Please check your input and try again.',
      [ErrorType.NOT_FOUND]: 'Please verify the URL or navigate back to the main page.',
      [ErrorType.CONFLICT]: 'Please refresh the page and try again.',
      [ErrorType.INTERNAL_ERROR]: 'If this problem persists, please contact support.',
      [ErrorType.SERVICE_UNAVAILABLE]: 'The service is being updated. Please try again shortly.',
      [ErrorType.RATE_LIMITED]: 'Please wait before making another request.',
      [ErrorType.UNKNOWN]: 'If this problem persists, please contact support.',
    };
    return actionMap[type];
  }
}

/**
 * Error classification from unknown error objects
 */
export class ErrorClassifier {
  static classifyError(error: unknown): UnifiedError {
    // If already a UnifiedError, return as-is
    if (error instanceof UnifiedError) {
      return error;
    }

    const errorObj = error as any;
    const message = this.getErrorMessage(error);
    const statusCode = errorObj?.status || errorObj?.statusCode;

    // Classify by HTTP status code
    if (statusCode) {
      switch (statusCode) {
        case 401:
          return new UnifiedError(ErrorType.AUTHENTICATION_FAILED, message, { statusCode });
        case 403:
          return new UnifiedError(ErrorType.AUTHORIZATION_FAILED, message, { statusCode });
        case 404:
          return new UnifiedError(ErrorType.NOT_FOUND, message, { statusCode });
        case 408:
          return new UnifiedError(ErrorType.TIMEOUT, message, { statusCode });
        case 409:
          return new UnifiedError(ErrorType.CONFLICT, message, { statusCode });
        case 429:
          return new UnifiedError(ErrorType.RATE_LIMITED, message, { statusCode });
        case 500:
        case 502:
        case 503:
          return new UnifiedError(ErrorType.SERVICE_UNAVAILABLE, message, { statusCode });
        case 504:
          return new UnifiedError(ErrorType.TIMEOUT, message, { statusCode });
        default:
          if (statusCode >= 400 && statusCode < 500) {
            return new UnifiedError(ErrorType.INVALID_REQUEST, message, { statusCode });
          }
          return new UnifiedError(ErrorType.INTERNAL_ERROR, message, { statusCode });
      }
    }

    // Classify by error properties
    const lowerMessage = message.toLowerCase();
    const errorName = errorObj?.name?.toLowerCase() || '';
    const errorCode = errorObj?.code;

    // Network errors
    if (errorName === 'typeerror' && lowerMessage.includes('fetch')) {
      return new UnifiedError(ErrorType.CONNECTION_FAILED, message, { originalError: error });
    }

    if (errorCode === 'ECONNREFUSED' || errorCode === 'ENOTFOUND' || errorCode === 'ENETUNREACH') {
      return new UnifiedError(ErrorType.NETWORK_ERROR, message, { originalError: error });
    }

    if (errorName === 'aborterror' || lowerMessage.includes('timeout')) {
      return new UnifiedError(ErrorType.TIMEOUT, message, { originalError: error });
    }

    // Authentication/Authorization
    if (lowerMessage.includes('unauthorized') || lowerMessage.includes('authentication')) {
      return new UnifiedError(ErrorType.AUTHENTICATION_FAILED, message, { originalError: error });
    }

    if (lowerMessage.includes('forbidden') || lowerMessage.includes('permission')) {
      return new UnifiedError(ErrorType.AUTHORIZATION_FAILED, message, { originalError: error });
    }

    if (lowerMessage.includes('session') && lowerMessage.includes('expired')) {
      return new UnifiedError(ErrorType.SESSION_EXPIRED, message, { originalError: error });
    }

    // Rate limiting
    if (lowerMessage.includes('rate limit') || lowerMessage.includes('too many requests')) {
      return new UnifiedError(ErrorType.RATE_LIMITED, message, { originalError: error });
    }

    // Validation
    if (lowerMessage.includes('validation') || lowerMessage.includes('invalid')) {
      return new UnifiedError(ErrorType.VALIDATION_ERROR, message, { originalError: error });
    }

    // Not found
    if (lowerMessage.includes('not found')) {
      return new UnifiedError(ErrorType.NOT_FOUND, message, { originalError: error });
    }

    // Default to unknown
    return new UnifiedError(ErrorType.UNKNOWN, message, { originalError: error });
  }

  private static getErrorMessage(error: unknown): string {
    if (error instanceof Error) {
      return error.message;
    }
    if (typeof error === 'string') {
      return error;
    }
    if (error && typeof error === 'object') {
      const errorObj = error as any;
      return errorObj.message || errorObj.error || String(error);
    }
    return 'An unexpected error occurred';
  }
}

/**
 * Retry logic utility
 */
export class RetryManager {
  static shouldRetry(error: UnifiedError, attemptCount: number, maxRetries: number = 3): boolean {
    return error.errorContext.retryable && attemptCount < maxRetries;
  }

  static getRetryDelay(error: UnifiedError, attemptCount: number): number {
    const baseDelay = error.errorContext.retryAfter || 5;
    // Exponential backoff with jitter
    const delay = baseDelay * Math.pow(2, attemptCount - 1);
    const jitter = Math.random() * 0.1 * delay;
    return (delay + jitter) * 1000; // Convert to milliseconds
  }

  static getRetryConfig(maxRetries: number = 3) {
    return {
      retry: (failureCount: number, error: unknown) => {
        const unifiedError = ErrorClassifier.classifyError(error);
        return RetryManager.shouldRetry(unifiedError, failureCount, maxRetries);
      },
      retryDelay: (attemptIndex: number, error: unknown) => {
        const unifiedError = ErrorClassifier.classifyError(error);
        return RetryManager.getRetryDelay(unifiedError, attemptIndex);
      },
    };
  }
}

/**
 * Response mapping for API routes
 */
export function mapErrorToResponse(error: unknown): NextResponse {
  const unifiedError = ErrorClassifier.classifyError(error);
  const { errorContext } = unifiedError;

  // Log the error
  logger.error('Request failed', {
    type: errorContext.type,
    message: errorContext.message,
    statusCode: errorContext.statusCode,
    severity: errorContext.severity,
    context: errorContext.context,
    stack: unifiedError.stack,
  });

  // Create response
  return NextResponse.json(
    {
      error: {
        type: errorContext.type,
        message: errorContext.userMessage,
        retryable: errorContext.retryable,
        retryAfter: errorContext.retryAfter,
        ...(process.env.NODE_ENV === 'development' && {
          details: {
            originalMessage: errorContext.message,
            context: errorContext.context,
            stack: unifiedError.stack,
          },
        }),
      },
    },
    { status: errorContext.statusCode || 500 }
  );
}

/**
 * Global error handler for unhandled errors
 */
export class GlobalErrorHandler {
  private static instance: GlobalErrorHandler | null = null;
  private isInitialized = false;
  private recentErrors = new Set<string>();

  static getInstance(): GlobalErrorHandler {
    if (!GlobalErrorHandler.instance) {
      GlobalErrorHandler.instance = new GlobalErrorHandler();
    }
    return GlobalErrorHandler.instance;
  }

  initialize(): void {
    if (this.isInitialized || typeof window === 'undefined') {
      return;
    }

    window.addEventListener('error', this.handleUnhandledError);
    window.addEventListener('unhandledrejection', this.handleUnhandledRejection);
    this.isInitialized = true;
  }

  private handleUnhandledError = (event: ErrorEvent) => {
    const error = event.error || new Error(event.message);
    this.processError(error, 'unhandled');
  };

  private handleUnhandledRejection = (event: PromiseRejectionEvent) => {
    const error = event.reason instanceof Error ? event.reason : new Error(String(event.reason));
    this.processError(error, 'promise');
    event.preventDefault(); // Prevent default browser handling
  };

  private processError(error: Error, source: 'unhandled' | 'promise') {
    const unifiedError = ErrorClassifier.classifyError(error);
    const errorKey = `${unifiedError.errorContext.type}:${unifiedError.message}`;

    // Prevent duplicate notifications
    if (this.recentErrors.has(errorKey)) {
      return;
    }

    this.recentErrors.add(errorKey);
    setTimeout(() => this.recentErrors.delete(errorKey), 5000);

    // Log the error
    logger.error(`Global ${source} error`, {
      type: unifiedError.errorContext.type,
      message: unifiedError.message,
      severity: unifiedError.errorContext.severity,
      stack: unifiedError.stack,
    });

    // Show user notification for high-severity errors
    if (unifiedError.errorContext.severity === 'high' || unifiedError.errorContext.severity === 'critical') {
      this.showNotification(unifiedError);
    }
  }

  private showNotification(error: UnifiedError) {
    const { errorContext } = error;
    
    // Skip certain error types that are noisy
    if (errorContext.message.includes('QueryErrorResetBoundary')) {
      return;
    }

    const color = errorContext.severity === 'critical' ? 'red' : 'orange';
    
    notifications.show({
      title: this.getNotificationTitle(errorContext.type),
      message: errorContext.userMessage,
      color,
      autoClose: errorContext.severity === 'critical' ? false : 5000,
    });
  }

  private getNotificationTitle(type: ErrorType): string {
    const titleMap: Record<ErrorType, string> = {
      [ErrorType.NETWORK_ERROR]: 'Network Error',
      [ErrorType.CONNECTION_FAILED]: 'Connection Error',
      [ErrorType.TIMEOUT]: 'Request Timeout',
      [ErrorType.AUTHENTICATION_FAILED]: 'Authentication Error',
      [ErrorType.AUTHORIZATION_FAILED]: 'Permission Error',
      [ErrorType.SESSION_EXPIRED]: 'Session Expired',
      [ErrorType.VALIDATION_ERROR]: 'Validation Error',
      [ErrorType.INVALID_REQUEST]: 'Invalid Request',
      [ErrorType.NOT_FOUND]: 'Not Found',
      [ErrorType.CONFLICT]: 'Conflict Error',
      [ErrorType.INTERNAL_ERROR]: 'System Error',
      [ErrorType.SERVICE_UNAVAILABLE]: 'Service Unavailable',
      [ErrorType.RATE_LIMITED]: 'Rate Limited',
      [ErrorType.UNKNOWN]: 'Unexpected Error',
    };
    return titleMap[type];
  }

  destroy(): void {
    if (typeof window !== 'undefined') {
      window.removeEventListener('error', this.handleUnhandledError);
      window.removeEventListener('unhandledrejection', this.handleUnhandledRejection);
    }
    this.isInitialized = false;
    this.recentErrors.clear();
  }
}

/**
 * Error boundary wrapper for React components
 */
export function withSDKErrorHandling<T>(
  operation: () => Promise<T>,
  context?: string
): Promise<T> {
  return operation().catch((error) => {
    const unifiedError = ErrorClassifier.classifyError(error);
    
    // Add context if provided
    if (context) {
      unifiedError.errorContext.context = {
        ...(unifiedError.errorContext.context || {}),
        operation: context,
      };
    }

    throw unifiedError;
  });
}

// Re-export for backwards compatibility
export { mapErrorToResponse as mapSDKErrorToResponse };
export { UnifiedError as SDKError };
export { ErrorType as SDKErrorType };