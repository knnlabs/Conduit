'use client';

import { Component, ErrorInfo, ReactNode } from 'react';
import { Container, Title, Text, Button, Alert, Stack, Code, Collapse } from '@mantine/core';
import { IconAlertTriangle, IconRefresh, IconBug } from '@tabler/icons-react';
import { useState } from 'react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
  onError?: (error: Error, errorInfo: ErrorInfo) => void;
}

interface State {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null, errorInfo: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return {
      hasError: true,
      error,
      errorInfo: null,
    };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('ErrorBoundary caught an error:', error, errorInfo);
    
    this.setState({
      error,
      errorInfo,
    });

    // Call custom error handler if provided
    this.props.onError?.(error, errorInfo);
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null, errorInfo: null });
  };

  handleReload = () => {
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      // Custom fallback UI if provided
      if (this.props.fallback) {
        return this.props.fallback;
      }

      // Default error UI
      return <ErrorFallback 
        error={this.state.error} 
        errorInfo={this.state.errorInfo}
        onRetry={this.handleRetry}
        onReload={this.handleReload}
      />;
    }

    return this.props.children;
  }
}

interface ErrorFallbackProps {
  error: Error | null;
  errorInfo: ErrorInfo | null;
  onRetry: () => void;
  onReload: () => void;
}

function ErrorFallback({ error, errorInfo, onRetry, onReload }: ErrorFallbackProps) {
  const [showDetails, setShowDetails] = useState(false);

  return (
    <Container size="sm" py="xl">
      <Stack gap="lg" align="center">
        <IconAlertTriangle size={64} color="red" />
        
        <div style={{ textAlign: 'center' }}>
          <Title order={2} mb="sm">Something went wrong</Title>
          <Text c="dimmed" size="lg">
            An unexpected error occurred. We apologize for the inconvenience.
          </Text>
        </div>

        <Alert 
          icon={<IconBug size={16} />} 
          title="Error Details" 
          color="red" 
          variant="light"
          style={{ width: '100%' }}
        >
          <Text size="sm" mb="xs">
            {error?.message || 'Unknown error occurred'}
          </Text>
          
          <Button 
            variant="subtle" 
            size="xs" 
            onClick={() => setShowDetails(!showDetails)}
          >
            {showDetails ? 'Hide' : 'Show'} Technical Details
          </Button>
          
          <Collapse in={showDetails}>
            <Stack gap="xs" mt="md">
              {error?.stack && (
                <div>
                  <Text size="xs" fw={500} mb={4}>Stack Trace:</Text>
                  <Code block style={{ fontSize: '10px', maxHeight: '200px', overflow: 'auto' }}>
                    {error.stack}
                  </Code>
                </div>
              )}
              
              {errorInfo?.componentStack && (
                <div>
                  <Text size="xs" fw={500} mb={4}>Component Stack:</Text>
                  <Code block style={{ fontSize: '10px', maxHeight: '200px', overflow: 'auto' }}>
                    {errorInfo.componentStack}
                  </Code>
                </div>
              )}
            </Stack>
          </Collapse>
        </Alert>

        <Stack gap="sm" style={{ width: '100%', maxWidth: '300px' }}>
          <Button 
            leftSection={<IconRefresh size={16} />}
            onClick={onRetry}
            fullWidth
          >
            Try Again
          </Button>
          
          <Button 
            variant="light" 
            onClick={onReload}
            fullWidth
          >
            Reload Page
          </Button>
          
          <Button 
            variant="subtle" 
            component="a"
            href="/"
            fullWidth
          >
            Go to Dashboard
          </Button>
        </Stack>

        <Text size="xs" c="dimmed" ta="center">
          If this problem persists, please contact your system administrator.
        </Text>
      </Stack>
    </Container>
  );
}

// Hook for functional components to handle errors
export function useErrorHandler() {
  const [error, setError] = useState<Error | null>(null);

  const handleError = (error: Error) => {
    console.error('Error caught by useErrorHandler:', error);
    setError(error);
  };

  const clearError = () => {
    setError(null);
  };

  // Re-throw error to be caught by ErrorBoundary
  if (error) {
    throw error;
  }

  return { handleError, clearError };
}

// HOC for wrapping components with error boundary
export function withErrorBoundary<T extends object>(
  Component: React.ComponentType<T>,
  fallback?: ReactNode
) {
  const WrappedComponent = (props: T) => (
    <ErrorBoundary fallback={fallback}>
      <Component {...props} />
    </ErrorBoundary>
  );

  WrappedComponent.displayName = `withErrorBoundary(${Component.displayName || Component.name})`;
  
  return WrappedComponent;
}