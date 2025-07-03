'use client';

import React, { Suspense } from 'react';
import { ErrorBoundary } from './ErrorBoundary';
import { Center, Loader, Stack, Text } from '@mantine/core';

interface AsyncBoundaryProps {
  children: React.ReactNode;
  loadingMessage?: string;
  fallback?: React.ReactNode;
  onError?: (error: Error, errorInfo: React.ErrorInfo) => void;
}

/**
 * Loading fallback component
 */
function LoadingFallback({ message }: { message?: string }) {
  return (
    <Center mih="50vh">
      <Stack align="center" gap="md">
        <Loader size="lg" />
        {message && <Text c="dimmed">{message}</Text>}
      </Stack>
    </Center>
  );
}

/**
 * Combines ErrorBoundary with Suspense for async components
 */
export function AsyncBoundary({ 
  children, 
  loadingMessage = 'Loading...', 
  fallback,
  onError 
}: AsyncBoundaryProps) {
  return (
    <ErrorBoundary fallback={fallback} onError={onError}>
      <Suspense fallback={<LoadingFallback message={loadingMessage} />}>
        {children}
      </Suspense>
    </ErrorBoundary>
  );
}

/**
 * Hook to wrap async operations with proper error handling
 */
export function useAsyncError() {
  const [, setError] = React.useState();

  return React.useCallback(
    (error: Error) => {
      setError(() => {
        throw error;
      });
    },
    [setError]
  );
}