/**
 * UI-specific error classification using SDK error types
 * This provides display logic for errors in the UI while using SDK error types as the base
 */

import { getErrorStatusCode, getErrorMessage, isHttpError } from './error-utils';

export type ErrorType = 'network' | 'auth' | 'timeout' | 'validation' | 'permission' | 'notFound' | 'server' | 'payment' | 'generic';
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
  static classify(error: unknown): ErrorType {
    // Check for HTTP errors first
    if (isHttpError(error)) {
      const status = getErrorStatusCode(error) ?? 0;
      if (status === 401) return 'auth';
      if (status === 402) return 'payment';
      if (status === 403) return 'permission';
      if (status === 404) return 'notFound';
      if (status === 408 || status === 504) return 'timeout';
      if (status >= 400 && status < 500) return 'validation';
      if (status >= 500) return 'server';
    }
    
    // Check for error message patterns
    const errorMessage = getErrorMessage(error).toLowerCase();
    const errorName = error instanceof Error ? error.name.toLowerCase() : '';
    
    if (errorMessage.includes('unauthorized') || errorMessage.includes('401') || errorName.includes('auth')) {
      return 'auth';
    }
    
    if (errorMessage.includes('payment required') || errorMessage.includes('402') || 
        errorMessage.includes('insufficient') || errorMessage.includes('balance') ||
        errorMessage.includes('credits') || errorMessage.includes('quota')) {
      return 'payment';
    }
    
    if (errorMessage.includes('forbidden') || errorMessage.includes('403') || errorMessage.includes('permission')) {
      return 'permission';
    }
    
    if (errorMessage.includes('not found') || errorMessage.includes('404')) {
      return 'notFound';
    }
    
    if (errorMessage.includes('timeout') || errorMessage.includes('408') || errorMessage.includes('504') || errorName.includes('timeout')) {
      return 'timeout';
    }
    
    if (errorMessage.includes('validation') || errorMessage.includes('invalid') || errorMessage.includes('400')) {
      return 'validation';
    }
    
    if (errorMessage.includes('network') || errorMessage.includes('fetch') || errorMessage.includes('connection')) {
      return 'network';
    }
    
    if (errorMessage.includes('server error') || errorMessage.includes('500') || errorMessage.includes('503')) {
      return 'server';
    }
    
    return 'generic';
  }

  /**
   * Determines if an error can be recovered from
   */
  static isRecoverable(error: unknown): boolean {
    const classification = this.classify(error);
    const recoverableTypes: ErrorType[] = ['network', 'timeout', 'server'];
    return recoverableTypes.includes(classification);
  }

  /**
   * Gets a user-friendly display message for an error
   */
  static getDisplayMessage(error: unknown, fallback = 'An unexpected error occurred'): string {
    // If it's an HttpError with a message, use that first
    if (isHttpError(error)) {
      const message = getErrorMessage(error);
      if (message) {
        return message;
      }
    }
    
    const classification = this.classify(error);
    const originalMessage = getErrorMessage(error);
    
    const messageMap: Record<ErrorType, string> = {
      network: 'Unable to connect to the server. Please check your internet connection.',
      auth: 'Authentication failed. Please log in again.',
      timeout: 'The request timed out. Please try again.',
      validation: this.formatValidationMessage(error, originalMessage),
      permission: 'You do not have permission to perform this action.',
      notFound: 'The requested resource was not found.',
      server: 'Server error occurred. Please try again later.',
      payment: 'Insufficient credits or API key balance. Please check your provider configuration and ensure you have sufficient credits.',
      generic: originalMessage || fallback,
    };
    
    return messageMap[classification];
  }

  /**
   * Determines the suggested recovery action for an error
   */
  static getRecoveryAction(error: unknown): RecoveryAction {
    const classification = this.classify(error);
    
    const actionMap: Record<ErrorType, RecoveryAction> = {
      network: 'retry',
      auth: 'login',
      timeout: 'retry',
      validation: 'none',
      permission: 'navigate',
      notFound: 'navigate',
      server: 'retry',
      payment: 'navigate',
      generic: 'reload',
    };
    
    return actionMap[classification];
  }

  /**
   * Gets the severity level of an error
   */
  static getSeverity(error: unknown): 'low' | 'medium' | 'high' | 'critical' {
    const classification = this.classify(error);
    
    const severityMap: Record<ErrorType, 'low' | 'medium' | 'high' | 'critical'> = {
      network: 'medium',
      auth: 'high',
      timeout: 'medium',
      validation: 'low',
      permission: 'high',
      notFound: 'medium',
      server: 'high',
      payment: 'high',
      generic: 'medium',
    };
    
    return severityMap[classification];
  }

  /**
   * Gets a complete classification for an error
   */
  static getClassification(error: unknown): ErrorClassification {
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
   * Formats validation error messages for better readability
   */
  private static formatValidationMessage(error: unknown, fallbackMessage: string): string {
    // If it's an HttpError with validation details, format it nicely
    if (isHttpError(error)) {
      const message = getErrorMessage(error);
      if (message) {
        return message;
      }
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
export function classifyError(error: unknown): ErrorClassification {
  return ErrorClassifier.getClassification(error);
}

/**
 * Utility function to check if an error is recoverable
 */
export function isRecoverableError(error: unknown): boolean {
  return ErrorClassifier.isRecoverable(error);
}

/**
 * Utility function to get user-friendly error message
 */
export function getUserFriendlyErrorMessage(error: unknown, fallback?: string): string {
  return ErrorClassifier.getDisplayMessage(error, fallback);
}