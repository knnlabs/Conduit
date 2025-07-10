/**
 * Unified global error handler for unhandled errors and promise rejections
 * Consolidates error handling logic from multiple components
 */

import React from 'react';
import { ErrorClassifier } from './error-classifier';
import { logger } from './logging';
import { notifications } from '@mantine/notifications';

export interface GlobalErrorHandlerConfig {
  enableNotifications?: boolean;
  enableErrorReporting?: boolean;
  enableConsoleLogging?: boolean;
  excludePatterns?: RegExp[];
  onError?: (error: Error, source: 'unhandled' | 'promise') => void;
  onBeforeNotification?: (error: Error) => boolean; // return false to prevent notification
}

export class GlobalErrorHandler {
  private static instance: GlobalErrorHandler | null = null;
  private config: GlobalErrorHandlerConfig;
  private isInitialized = false;
  private errorCount = 0;
  private recentErrors = new Set<string>();
  private cleanupTimer: NodeJS.Timeout | null = null;

  private constructor(config: GlobalErrorHandlerConfig = {}) {
    this.config = {
      enableNotifications: true,
      enableErrorReporting: true,
      enableConsoleLogging: true,
      excludePatterns: [],
      ...config,
    };
  }

  static getInstance(config?: GlobalErrorHandlerConfig): GlobalErrorHandler {
    if (!GlobalErrorHandler.instance) {
      GlobalErrorHandler.instance = new GlobalErrorHandler(config);
    }
    return GlobalErrorHandler.instance;
  }

  initialize(): void {
    if (this.isInitialized) {
      return;
    }

    if (typeof window === 'undefined') {
      // Server-side rendering - skip initialization
      return;
    }

    // Handle unhandled JavaScript errors
    window.addEventListener('error', this.handleUnhandledError);

    // Handle unhandled promise rejections
    window.addEventListener('unhandledrejection', this.handleUnhandledRejection);

    this.isInitialized = true;
    logger.info('Global error handler initialized');

    // Clean up recent errors periodically
    this.cleanupTimer = setInterval(() => {
      this.recentErrors.clear();
    }, 60000); // Clear every minute
  }

  destroy(): void {
    if (!this.isInitialized) {
      return;
    }

    if (typeof window !== 'undefined') {
      window.removeEventListener('error', this.handleUnhandledError);
      window.removeEventListener('unhandledrejection', this.handleUnhandledRejection);
    }

    if (this.cleanupTimer) {
      clearInterval(this.cleanupTimer);
      this.cleanupTimer = null;
    }

    this.isInitialized = false;
    GlobalErrorHandler.instance = null;
    logger.info('Global error handler destroyed');
  }

  updateConfig(newConfig: Partial<GlobalErrorHandlerConfig>): void {
    this.config = { ...this.config, ...newConfig };
  }

  private handleUnhandledError = (event: ErrorEvent): void => {
    const error = event.error || new Error(event.message || 'Unknown error');
    
    // Add context to error
    if (event.filename) {
      (error as any).filename = event.filename;
      (error as any).lineno = event.lineno;
      (error as any).colno = event.colno;
    }

    this.processError(error, 'unhandled');
  };

  private handleUnhandledRejection = (event: PromiseRejectionEvent): void => {
    const error = event.reason instanceof Error 
      ? event.reason 
      : new Error(String(event.reason) || 'Unhandled promise rejection');

    this.processError(error, 'promise');
    
    // Prevent default browser error handling
    event.preventDefault();
  };

  private processError(error: Error, source: 'unhandled' | 'promise'): void {
    // Check if error should be excluded
    if (this.shouldExcludeError(error)) {
      return;
    }

    // Prevent duplicate error notifications
    const errorKey = `${error.name}:${error.message}`;
    if (this.recentErrors.has(errorKey)) {
      return;
    }
    this.recentErrors.add(errorKey);

    this.errorCount++;

    // Log error
    if (this.config.enableConsoleLogging) {
      this.logError(error, source);
    }

    // Call custom error handler
    this.config.onError?.(error, source);

    // Show notification
    if (this.config.enableNotifications && this.shouldShowNotification(error)) {
      this.showErrorNotification(error);
    }

    // Report error
    if (this.config.enableErrorReporting) {
      this.reportError(error, source);
    }
  }

  private shouldExcludeError(error: Error): boolean {
    const { excludePatterns = [] } = this.config;
    const errorMessage = error.message || '';
    const errorName = error.name || '';

    // Default exclusions
    const defaultExclusions = [
      /script error/i,
      /non-error promise rejection/i,
      /loading chunk \d+ failed/i,
      /network error/i,
      /fetch.*failed/i,
      /cancelled/i,
      /aborted/i,
      /non-error promise rejection/i,
    ];

    const allPatterns = [...defaultExclusions, ...excludePatterns];

    return allPatterns.some(pattern => 
      pattern.test(errorMessage) || pattern.test(errorName)
    );
  }

  private shouldShowNotification(error: Error): boolean {
    // Check custom filter
    if (this.config.onBeforeNotification) {
      return this.config.onBeforeNotification(error);
    }

    // Don't show notifications for external script errors
    if ((error as any).filename && typeof window !== 'undefined' && 
        !(error as any).filename.includes(window.location.host)) {
      return false;
    }

    // Don't show notifications for low-severity errors
    const classification = ErrorClassifier.getClassification(error);
    return classification.severity !== 'low';
  }

  private logError(error: Error, source: 'unhandled' | 'promise'): void {
    const context = source === 'unhandled' ? 'Global Unhandled Error' : 'Global Promise Rejection';
    
    logger.error(context, {
      error: {
        name: error.name,
        message: error.message,
        stack: error.stack,
        filename: (error as any).filename,
        lineno: (error as any).lineno,
        colno: (error as any).colno,
      },
      source,
      errorCount: this.errorCount,
      url: typeof window !== 'undefined' ? window.location.href : 'unknown',
      userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : 'unknown',
      timestamp: new Date().toISOString(),
    });
  }

  private showErrorNotification(error: Error): void {
    const classification = ErrorClassifier.getClassification(error);
    
    notifications.show({
      title: 'Unexpected Error',
      message: classification.displayMessage,
      color: classification.severity === 'critical' ? 'red' : 'orange',
      autoClose: classification.severity === 'critical' ? false : 5000,
    });
  }

  private reportError(error: Error, source: 'unhandled' | 'promise'): void {
    // In production, this would send to error reporting service
    if (process.env.NODE_ENV === 'production') {
      // Example integration points:
      // - Sentry: Sentry.captureException(error, { tags: { source } })
      // - LogRocket: LogRocket.captureException(error)
      // - Custom API: sendErrorReport(error, { source, errorCount: this.errorCount })
    }
  }

  // Public methods for manual error handling
  public handleError(error: Error, context?: string): void {
    const contextualError = context 
      ? Object.assign(error, { context })
      : error;
    
    this.processError(contextualError, 'unhandled');
  }

  public getErrorCount(): number {
    return this.errorCount;
  }

  public resetErrorCount(): void {
    this.errorCount = 0;
  }

  public clearRecentErrors(): void {
    this.recentErrors.clear();
  }
}

// Convenience functions for easy usage
export function initializeGlobalErrorHandler(config?: GlobalErrorHandlerConfig): void {
  const handler = GlobalErrorHandler.getInstance(config);
  handler.initialize();
}

export function destroyGlobalErrorHandler(): void {
  const handler = GlobalErrorHandler.getInstance();
  handler.destroy();
}

export function handleGlobalError(error: Error, context?: string): void {
  const handler = GlobalErrorHandler.getInstance();
  handler.handleError(error, context);
}

export function getGlobalErrorCount(): number {
  const handler = GlobalErrorHandler.getInstance();
  return handler.getErrorCount();
}

// Legacy compatibility functions
export function setupGlobalErrorHandlers(): void {
  initializeGlobalErrorHandler();
}

export function reportError(error: Error, context?: Record<string, unknown>): void {
  logger.error('Error reported', {
    error: {
      message: error.message,
      stack: error.stack,
      name: error.name,
    },
    context,
  });

  // Use global error handler
  handleGlobalError(error, context ? JSON.stringify(context) : undefined);
}

// React component for easy initialization
export function GlobalErrorHandlerInitializer({ 
  config,
  children 
}: {
  config?: GlobalErrorHandlerConfig;
  children: React.ReactNode;
}) {
  React.useEffect(() => {
    initializeGlobalErrorHandler(config);
    
    return () => {
      destroyGlobalErrorHandler();
    };
  }, [config]);

  return React.createElement(React.Fragment, null, children);
}

// Hook for functional components
export function useGlobalErrorHandler(config?: GlobalErrorHandlerConfig) {
  React.useEffect(() => {
    const handler = GlobalErrorHandler.getInstance(config);
    if (config) {
      handler.updateConfig(config);
    }
    handler.initialize();
    
    return () => {
      handler.destroy();
    };
  }, [config]);

  return {
    handleError: (error: Error, context?: string) => {
      const handler = GlobalErrorHandler.getInstance();
      handler.handleError(error, context);
    },
    getErrorCount: () => {
      const handler = GlobalErrorHandler.getInstance();
      return handler.getErrorCount();
    },
    resetErrorCount: () => {
      const handler = GlobalErrorHandler.getInstance();
      handler.resetErrorCount();
    },
  };
}