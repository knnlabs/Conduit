/**
 * Unified error classification and handling utilities
 * Consolidates error detection logic from multiple components
 */

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
    const errorMessage = this.getErrorMessage(error).toLowerCase();
    const errorName = error instanceof Error ? error.name.toLowerCase() : '';
    
    // Network errors
    if (this.isNetworkError(errorMessage, errorName)) {
      return 'network';
    }
    
    // Authentication errors
    if (this.isAuthError(errorMessage, errorName)) {
      return 'auth';
    }
    
    // Timeout errors
    if (this.isTimeoutError(errorMessage, errorName)) {
      return 'timeout';
    }
    
    // Validation errors
    if (this.isValidationError(errorMessage, errorName)) {
      return 'validation';
    }
    
    // Permission errors
    if (this.isPermissionError(errorMessage, errorName)) {
      return 'permission';
    }
    
    // Not found errors
    if (this.isNotFoundError(errorMessage, errorName)) {
      return 'notFound';
    }
    
    // Server errors
    if (this.isServerError(errorMessage, errorName)) {
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
    const classification = this.classify(error);
    const originalMessage = this.getErrorMessage(error);
    
    const messageMap: Record<ErrorType, string> = {
      network: 'Unable to connect to the server. Please check your internet connection.',
      auth: 'Authentication failed. Please log in again.',
      timeout: 'The request timed out. Please try again.',
      validation: this.formatValidationMessage(originalMessage),
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
   * Detects network-related errors
   */
  private static isNetworkError(message: string, name: string): boolean {
    const networkIndicators = [
      'network', 'fetch', 'connection', 'offline', 'internet',
      'disconnected', 'unreachable', 'refused', 'dns', 'cors'
    ];
    
    const networkNames = ['networkerror', 'fetcherror'];
    
    return networkIndicators.some(indicator => message.includes(indicator)) ||
           networkNames.some(networkName => name.includes(networkName));
  }

  /**
   * Detects authentication-related errors
   */
  private static isAuthError(message: string, name: string): boolean {
    const authIndicators = [
      'unauthorized', 'authentication', 'auth', 'login', 'credential',
      'token', 'forbidden', '401', '403', 'access denied'
    ];
    
    const authNames = ['autherror', 'authenticationerror'];
    
    return authIndicators.some(indicator => message.includes(indicator)) ||
           authNames.some(authName => name.includes(authName));
  }

  /**
   * Detects timeout-related errors
   */
  private static isTimeoutError(message: string, name: string): boolean {
    const timeoutIndicators = [
      'timeout', 'timed out', 'time out', 'aborted', 'abort'
    ];
    
    const timeoutNames = ['timeouterror', 'aborterror'];
    
    return timeoutIndicators.some(indicator => message.includes(indicator)) ||
           timeoutNames.some(timeoutName => name.includes(timeoutName));
  }

  /**
   * Detects validation-related errors
   */
  private static isValidationError(message: string, name: string): boolean {
    const validationIndicators = [
      'validation', 'invalid', 'required', 'format', 'pattern',
      'must be', 'should be', 'expected', '400'
    ];
    
    const validationNames = ['validationerror', 'validationerror'];
    
    return validationIndicators.some(indicator => message.includes(indicator)) ||
           validationNames.some(validationName => name.includes(validationName));
  }

  /**
   * Detects permission-related errors
   */
  private static isPermissionError(message: string, name: string): boolean {
    const permissionIndicators = [
      'permission', 'forbidden', 'not allowed', 'unauthorized',
      'access denied', 'insufficient', '403'
    ];
    
    const permissionNames = ['permissionerror', 'forbiddenerror'];
    
    return permissionIndicators.some(indicator => message.includes(indicator)) ||
           permissionNames.some(permissionName => name.includes(permissionName));
  }

  /**
   * Detects not found errors
   */
  private static isNotFoundError(message: string, name: string): boolean {
    const notFoundIndicators = [
      'not found', 'does not exist', 'missing', '404'
    ];
    
    const notFoundNames = ['notfounderror'];
    
    return notFoundIndicators.some(indicator => message.includes(indicator)) ||
           notFoundNames.some(notFoundName => name.includes(notFoundName));
  }

  /**
   * Detects server-related errors
   */
  private static isServerError(message: string, name: string): boolean {
    const serverIndicators = [
      'internal server error', 'server error', 'service unavailable',
      '500', '502', '503', '504', 'gateway', 'backend'
    ];
    
    const serverNames = ['servererror', 'internalerror'];
    
    return serverIndicators.some(indicator => message.includes(indicator)) ||
           serverNames.some(serverName => name.includes(serverName));
  }

  /**
   * Formats validation error messages for better readability
   */
  private static formatValidationMessage(message: string): string {
    // If the message is already user-friendly, return as-is
    if (message.length < 100 && !message.includes('Error:')) {
      return message;
    }
    
    // Extract useful parts from validation error messages
    const patterns = [
      /validation error:?\s*(.+)/i,
      /invalid input:?\s*(.+)/i,
      /required field:?\s*(.+)/i,
      /(.+)\s+is required/i,
      /(.+)\s+must be/i,
    ];
    
    for (const pattern of patterns) {
      const match = message.match(pattern);
      if (match && match[1]) {
        return match[1].trim();
      }
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