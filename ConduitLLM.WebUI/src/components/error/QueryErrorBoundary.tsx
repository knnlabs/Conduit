'use client';

import React from 'react';
import { Alert, Stack, Text, Button, Card, Center } from '@mantine/core';
import { IconAlertCircle, IconRefresh } from '@tabler/icons-react';
import { useQueryErrorResetBoundary } from '@tanstack/react-query';

interface QueryErrorBoundaryProps {
  children: React.ReactNode;
  onReset?: () => void;
}

interface QueryErrorFallbackProps {
  error: Error;
  resetErrorBoundary: () => void;
}

/**
 * Error fallback for React Query errors
 */
function QueryErrorFallback({ error, resetErrorBoundary }: QueryErrorFallbackProps) {
  const isNetworkError = error.message.toLowerCase().includes('network') || 
                        error.message.toLowerCase().includes('fetch');
  const isAuthError = error.message.toLowerCase().includes('unauthorized') ||
                      error.message.toLowerCase().includes('authentication');

  return (
    <Center mih="50vh">
      <Card shadow="md" p="xl" maw={500} w="100%">
        <Stack align="center" gap="md">
          <Alert 
            icon={<IconAlertCircle size={20} />} 
            title={isAuthError ? 'Authentication Error' : isNetworkError ? 'Connection Error' : 'Error Loading Data'}
            color="red"
            variant="light"
            w="100%"
          >
            <Stack gap="xs">
              <Text size="sm">
                {isAuthError 
                  ? 'Your session may have expired. Please try logging in again.'
                  : isNetworkError 
                  ? 'Unable to connect to the server. Please check your internet connection.'
                  : error.message || 'An unexpected error occurred while loading data.'}
              </Text>
              {process.env.NODE_ENV === 'development' && (
                <Text size="xs" c="dimmed" style={{ fontFamily: 'monospace' }}>
                  {error.stack?.split('\n')[0]}
                </Text>
              )}
            </Stack>
          </Alert>

          <Button
            leftSection={<IconRefresh size={16} />}
            onClick={resetErrorBoundary}
            fullWidth
          >
            Try Again
          </Button>

          {isAuthError && (
            <Button
              variant="subtle"
              component="a"
              href="/login"
              fullWidth
            >
              Go to Login
            </Button>
          )}
        </Stack>
      </Card>
    </Center>
  );
}

/**
 * Error boundary specifically for React Query errors
 */
export class QueryErrorBoundary extends React.Component<
  QueryErrorBoundaryProps,
  { hasError: boolean; error: Error | null }
> {
  constructor(props: QueryErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error) {
    return { hasError: true, error };
  }

  override componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    console.error('QueryErrorBoundary caught an error:', error, errorInfo);
  }

  override render() {
    if (this.state.hasError && this.state.error) {
      return (
        <QueryErrorFallback
          error={this.state.error}
          resetErrorBoundary={() => {
            this.setState({ hasError: false, error: null });
            this.props.onReset?.();
          }}
        />
      );
    }

    return this.props.children;
  }
}

/**
 * Hook that combines QueryErrorBoundary with React Query's error reset
 */
export function useQueryErrorBoundary() {
  const { reset } = useQueryErrorResetBoundary();
  
  return {
    QueryErrorBoundary: ({ children }: { children: React.ReactNode }) => (
      <QueryErrorBoundary onReset={reset}>
        {children}
      </QueryErrorBoundary>
    ),
    reset,
  };
}