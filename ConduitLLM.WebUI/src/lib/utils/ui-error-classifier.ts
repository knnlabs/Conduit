/**
 * UI-specific error classification using SDK error types
 * This provides display logic for errors in the UI while using SDK error types as the base
 */

import {
  ConduitError,
  AuthError,
  AuthorizationError,
  ValidationError,
  NotFoundError,
  ConflictError,
  RateLimitError,
  ServerError,
  NetworkError,
  TimeoutError,
  isConduitError,
  isAuthError,
  isValidationError,
  isNotFoundError,
  isRateLimitError,
  isNetworkError,
} from '@knn_labs/conduit-admin-client';

export type ErrorType = 'network' | 'auth' | 'timeout' | 'validation' | 'permission' | 'notFound' | 'server' | 'generic';
export type RecoveryAction = 'retry' | 'login' | 'reload' | 'navigate' | 'none';

export interface ErrorClassification {
  type: ErrorType;
  isRecoverable: boolean;
  displayMessage: string;
  recoveryAction: RecoveryAction;
  severity: 'low' | 'medium' | 'high' | 'critical';
}

export class ErrorClassifier {
  /**
   * Classifies an error into a specific type based on its properties
   */
  static classify(error: Error | unknown): ErrorType {
    // Check SDK error types first
    if (isNetworkError(error)) {
      return 'network';
    }
    
    if (isAuthError(error) || error instanceof AuthError) {
      return 'auth';
    }
    
    if (error instanceof AuthorizationError) {
      return 'permission';
    }
    
    if (error instanceof TimeoutError) {
      return 'timeout';
    }
    
    if (isValidationError(error)) {
      return 'validation';
    }
    
    if (isNotFoundError(error)) {
      return 'notFound';
    }
    
    if (error instanceof ServerError) {
      return 'server';
    }
    
    // Check for non-SDK error patterns
    const errorMessage = this.getErrorMessage(error).toLowerCase();
    const errorName = error instanceof Error ? error.name.toLowerCase() : '';
    
    if (errorMessage.includes('timeout') || errorName.includes('timeout')) {
      return 'timeout';
    }
    
    if (errorMessage.includes('network') || errorMessage.includes('fetch')) {
      return 'network';
    }
    
    if (errorMessage.includes('server error') || errorMessage.includes('500')) {
      return 'server';
    }
    
    return 'generic';
  }

  /**
   * Determines if an error can be recovered from
   */
  static isRecoverable(error: Error | unknown): boolean {
    const classification = this.classify(error);
    const recoverableTypes: ErrorType[] = ['network', 'timeout', 'server'];
    return recoverableTypes.includes(classification);
  }

  /**
   * Gets a user-friendly display message for an error
   */
  static getDisplayMessage(error: Error | unknown, fallback = 'An unexpected error occurred'): string {
    // If it's a ConduitError with a message, use that first
    if (isConduitError(error) && error.message) {
      return error.message;
    }
    
    const classification = this.classify(error);
    const originalMessage = this.getErrorMessage(error);
    
    const messageMap: Record<ErrorType, string> = {
      network: 'Unable to connect to the server. Please check your internet connection.',
      auth: 'Authentication failed. Please log in again.',
      timeout: 'The request timed out. Please try again.',
      validation: this.formatValidationMessage(error, originalMessage),
      permission: 'You do not have permission to perform this action.',
      notFound: 'The requested resource was not found.',
      server: 'Server error occurred. Please try again later.',
      generic: originalMessage || fallback,
    };
    
    return messageMap[classification];
  }

  /**
   * Determines the suggested recovery action for an error
   */
  static getRecoveryAction(error: Error | unknown): RecoveryAction {
    const classification = this.classify(error);
    
    const actionMap: Record<ErrorType, RecoveryAction> = {
      network: 'retry',
      auth: 'login',
      timeout: 'retry',
      validation: 'none',
      permission: 'navigate',
      notFound: 'navigate',
      server: 'retry',
      generic: 'reload',
    };
    
    return actionMap[classification];
  }

  /**
   * Gets the severity level of an error
   */
  static getSeverity(error: Error | unknown): 'low' | 'medium' | 'high' | 'critical' {
    const classification = this.classify(error);
    
    const severityMap: Record<ErrorType, 'low' | 'medium' | 'high' | 'critical'> = {
      network: 'medium',
      auth: 'high',
      timeout: 'medium',
      validation: 'low',
      permission: 'high',
      notFound: 'medium',
      server: 'high',
      generic: 'medium',
    };
    
    return severityMap[classification];
  }

  /**
   * Gets a complete classification for an error
   */
  static getClassification(error: Error | unknown): ErrorClassification {
    const type = this.classify(error);
    
    return {
      type,
      isRecoverable: this.isRecoverable(error),
      displayMessage: this.getDisplayMessage(error),
      recoveryAction: this.getRecoveryAction(error),
      severity: this.getSeverity(error),
    };
  }

  /**
   * Safely extracts error message from various error types
   */
  private static getErrorMessage(error: Error | unknown): string {
    if (error instanceof Error) {
      return error.message;
    }
    
    if (typeof error === 'string') {
      return error;
    }
    
    if (error && typeof error === 'object' && 'message' in error) {
      return String((error as { message: unknown }).message);
    }
    
    return 'Unknown error';
  }

  /**
   * Formats validation error messages for better readability
   */
  private static formatValidationMessage(error: unknown, fallbackMessage: string): string {
    // If it's a ValidationError with a field, format it nicely
    if (isValidationError(error) && error.field) {
      return `Validation failed for field: ${error.field}. ${error.message}`;
    }
    
    // If the message is already user-friendly, return as-is
    if (fallbackMessage.length < 100 && !fallbackMessage.includes('Error:')) {
      return fallbackMessage;
    }
    
    return 'Please check your input and try again.';
  }
}

/**
 * Utility function for quick error classification
 */
export function classifyError(error: Error | unknown): ErrorClassification {
  return ErrorClassifier.getClassification(error);
}

/**
 * Utility function to check if an error is recoverable
 */
export function isRecoverableError(error: Error | unknown): boolean {
  return ErrorClassifier.isRecoverable(error);
}

/**
 * Utility function to get user-friendly error message
 */
export function getErrorMessage(error: Error | unknown, fallback?: string): string {
  return ErrorClassifier.getDisplayMessage(error, fallback);
}