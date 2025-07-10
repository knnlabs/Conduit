'use client';

import React, { Component, ErrorInfo, ReactNode } from 'react';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { ErrorClassifier } from '@/lib/utils/error-classifier';
import { logger } from '@/lib/utils/logging';
import { notifications } from '@mantine/notifications';
import { useQueryErrorResetBoundary } from '@tanstack/react-query';

export interface UnifiedErrorBoundaryProps {
  children: ReactNode;
  
  // Error boundary configuration
  fallback?: ReactNode;
  onError?: (error: Error, errorInfo: ErrorInfo) => void;
  
  // Display configuration
  variant?: 'inline' | 'card' | 'fullscreen';
  title?: string;
  showDetails?: boolean;
  showStackTrace?: boolean;
  
  // Specialized error handling
  errorType?: 'general' | 'query' | 'lazy' | 'async';
  context?: string;
  
  // Action handlers
  onRetry?: () => void;
  onReload?: () => void;
  onNavigateHome?: () => void;
  onLogin?: () => void;
  
  // Query-specific
  resetOnPropsChange?: boolean;
  queryErrorBoundary?: boolean;
  
  // Lazy loading specific
  moduleName?: string;
  
  // Development flags
  enableErrorReporting?: boolean;
  enableNotifications?: boolean;
  
  // Styling
  className?: string;
  testId?: string;
}

interface UnifiedErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
  errorCount: number;
  resetCount: number;
}

export class UnifiedErrorBoundary extends Component<
  UnifiedErrorBoundaryProps,
  UnifiedErrorBoundaryState
> {
  private previousProps: UnifiedErrorBoundaryProps;
  private resetTimeoutId: NodeJS.Timeout | null = null;

  constructor(props: UnifiedErrorBoundaryProps) {
    super(props);
    
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null,
      errorCount: 0,
      resetCount: 0,
    };
    
    this.previousProps = props;
  }

  static getDerivedStateFromError(error: Error): Partial<UnifiedErrorBoundaryState> {
    return {
      hasError: true,
      error,
      errorCount: 1,
    };
  }

  override componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    this.setState(prevState => ({
      errorInfo,
      errorCount: prevState.errorCount + 1,
    }));

    // Log error with context
    this.logError(error, errorInfo);

    // Call custom error handler
    this.props.onError?.(error, errorInfo);

    // Show notification if enabled
    if (this.props.enableNotifications !== false) {
      this.showErrorNotification(error);
    }

    // Report error if enabled
    if (this.props.enableErrorReporting !== false) {
      this.reportError(error, errorInfo);
    }
  }

  override componentDidUpdate(prevProps: UnifiedErrorBoundaryProps) {
    const { resetOnPropsChange } = this.props;
    const { hasError } = this.state;

    // Reset error boundary if props changed and resetOnPropsChange is enabled
    if (hasError && resetOnPropsChange && this.didPropsChange(prevProps)) {
      this.resetErrorBoundary();
    }

    this.previousProps = prevProps;
  }

  override componentWillUnmount() {
    if (this.resetTimeoutId) {
      clearTimeout(this.resetTimeoutId);
    }
  }

  private didPropsChange(prevProps: UnifiedErrorBoundaryProps): boolean {
    // Compare relevant props that might indicate a route change or data update
    return (
      prevProps.context !== this.props.context ||
      prevProps.errorType !== this.props.errorType ||
      prevProps.moduleName !== this.props.moduleName
    );
  }

  private logError(error: Error, errorInfo: ErrorInfo) {
    const context = this.props.context || 'UnifiedErrorBoundary';
    
    logger.error(`Error caught by ${context}`, {
      error: {
        name: error.name,
        message: error.message,
        stack: error.stack,
      },
      errorInfo: {
        componentStack: errorInfo.componentStack,
      },
      errorType: this.props.errorType,
      moduleName: this.props.moduleName,
      url: typeof window !== 'undefined' ? window.location.href : 'unknown',
      userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : 'unknown',
      timestamp: new Date().toISOString(),
      errorCount: this.state.errorCount,
    });
  }

  private showErrorNotification(error: Error) {
    const classification = ErrorClassifier.getClassification(error);
    
    notifications.show({
      title: 'Error Occurred',
      message: classification.displayMessage,
      color: classification.severity === 'critical' ? 'red' : 'orange',
      autoClose: classification.severity === 'critical' ? false : 5000,
    });
  }

  private reportError(error: Error, errorInfo: ErrorInfo) {
    // In production, this would send to error reporting service
    if (process.env.NODE_ENV === 'production') {
      // Example integration points:
      // - Sentry: Sentry.captureException(error, { contexts: { errorInfo } })
      // - LogRocket: LogRocket.captureException(error)
      // - Custom API: sendErrorReport(error, errorInfo, this.props.context)
    }
  }

  private resetErrorBoundary = () => {
    // Clear any existing timeout
    if (this.resetTimeoutId) {
      clearTimeout(this.resetTimeoutId);
    }

    this.setState(prevState => ({
      hasError: false,
      error: null,
      errorInfo: null,
      resetCount: prevState.resetCount + 1,
    }));

    // Call custom retry handler
    this.props.onRetry?.();
  };

  private handleRetry = () => {
    this.resetErrorBoundary();
  };

  private handleReload = () => {
    this.props.onReload?.();
    if (!this.props.onReload) {
      window.location.reload();
    }
  };

  private handleNavigateHome = () => {
    this.props.onNavigateHome?.();
    if (!this.props.onNavigateHome) {
      window.location.href = '/';
    }
  };

  private handleLogin = () => {
    this.props.onLogin?.();
    if (!this.props.onLogin) {
      window.location.href = '/login';
    }
  };

  private getErrorTitle(): string {
    if (this.props.title) {
      return this.props.title;
    }

    switch (this.props.errorType) {
      case 'query':
        return 'Data Loading Error';
      case 'lazy':
        return 'Module Loading Error';
      case 'async':
        return 'Async Operation Error';
      default:
        return 'Application Error';
    }
  }

  private getErrorMessage(): string {
    const { error } = this.state;
    const { errorType, moduleName } = this.props;

    if (!error) {
      return 'An unexpected error occurred';
    }

    // Handle lazy loading errors
    if (errorType === 'lazy') {
      const moduleText = moduleName ? ` (${moduleName})` : '';
      return `Failed to load module${moduleText}. Please try reloading the page.`;
    }

    // Handle query errors
    if (errorType === 'query') {
      const classification = ErrorClassifier.getClassification(error);
      return classification.displayMessage;
    }

    // Use error classifier for other types
    return ErrorClassifier.getDisplayMessage(error);
  }

  override render() {
    const { hasError, error, errorCount } = this.state;
    const {
      children,
      fallback,
      variant = 'card',
      showDetails = false,
      showStackTrace = false,
      className,
      testId,
    } = this.props;

    if (hasError && error) {
      // Custom fallback if provided
      if (fallback) {
        return fallback;
      }

      // Default error display
      return (
        <ErrorDisplay
          error={error}
          variant={variant}
          title={this.getErrorTitle()}
          showDetails={showDetails}
          showStackTrace={showStackTrace}
          onRetry={this.handleRetry}
          onReload={this.handleReload}
          onNavigateHome={this.handleNavigateHome}
          onLogin={this.handleLogin}
          className={className}
          testId={testId}
          actions={[
            {
              label: `Try Again ${errorCount > 1 ? `(${errorCount})` : ''}`,
              onClick: this.handleRetry,
              color: 'blue',
              variant: 'light',
            },
          ]}
        />
      );
    }

    return children;
  }
}

// Hook for using with React Query
export function useUnifiedErrorBoundary() {
  const { reset } = useQueryErrorResetBoundary();
  return { reset };
}

// Higher-order component for wrapping components
export function withUnifiedErrorBoundary<P extends object>(
  Component: React.ComponentType<P>,
  errorBoundaryProps?: Omit<UnifiedErrorBoundaryProps, 'children'>
) {
  return function WrappedComponent(props: P) {
    return (
      <UnifiedErrorBoundary {...errorBoundaryProps}>
        <Component {...props} />
      </UnifiedErrorBoundary>
    );
  };
}

// Specialized error boundary components for common use cases
export function QueryErrorBoundary({ 
  children, 
  ...props 
}: Omit<UnifiedErrorBoundaryProps, 'errorType'>) {
  return (
    <UnifiedErrorBoundary 
      {...props}
      errorType="query"
      queryErrorBoundary={true}
      resetOnPropsChange={true}
    >
      {children}
    </UnifiedErrorBoundary>
  );
}

export function LazyErrorBoundary({ 
  children, 
  moduleName,
  ...props 
}: Omit<UnifiedErrorBoundaryProps, 'errorType'>) {
  return (
    <UnifiedErrorBoundary 
      {...props}
      errorType="lazy"
      moduleName={moduleName}
      variant="fullscreen"
    >
      {children}
    </UnifiedErrorBoundary>
  );
}

export function AsyncErrorBoundary({ 
  children, 
  ...props 
}: Omit<UnifiedErrorBoundaryProps, 'errorType'>) {
  return (
    <UnifiedErrorBoundary 
      {...props}
      errorType="async"
      resetOnPropsChange={true}
    >
      {children}
    </UnifiedErrorBoundary>
  );
}

// Hook for throwing errors in functional components
export function useAsyncError() {
  const [, setError] = React.useState();
  
  return React.useCallback((error: Error) => {
    setError(() => {
      throw error;
    });
  }, []);
}