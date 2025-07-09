import { lazy, Suspense } from 'react';
import { LoadingOverlay } from '@mantine/core';
import { LazyErrorBoundary } from '@/components/error/LazyErrorBoundary';

export interface LazyLoadOptions {
  fallback?: React.ReactNode;
  loadingMessage?: string;
  moduleName?: string;
  enableErrorBoundary?: boolean;
}

export function lazyLoadPage<T extends React.ComponentType<any>>(
  importFunc: () => Promise<{ default: T }>,
  options: LazyLoadOptions = {}
) {
  const LazyComponent = lazy(importFunc);
  const { enableErrorBoundary = true } = options;

  return function LazyLoadedPage(props: React.ComponentProps<T>) {
    const fallback = options.fallback || (
      <LoadingOverlay
        visible
        overlayProps={{ radius: 'sm', blur: 2 }}
        loaderProps={{ color: 'blue', type: 'bars' }}
        pos="fixed"
        title={options.loadingMessage || 'Loading page...'}
      />
    );

    const content = (
      <Suspense fallback={fallback}>
        <LazyComponent {...props} />
      </Suspense>
    );

    if (enableErrorBoundary) {
      return (
        <LazyErrorBoundary moduleName={options.moduleName}>
          {content}
        </LazyErrorBoundary>
      );
    }

    return content;
  };
}

export function lazyLoadComponent<T extends React.ComponentType<any>>(
  importFunc: () => Promise<{ default: T }>,
  fallback?: React.ReactNode,
  options: Omit<LazyLoadOptions, 'fallback'> = {}
) {
  const LazyComponent = lazy(importFunc);
  const { enableErrorBoundary = true } = options;

  return function LazyLoadedComponent(props: React.ComponentProps<T>) {
    const content = (
      <Suspense fallback={fallback || <LoadingOverlay visible />}>
        <LazyComponent {...props} />
      </Suspense>
    );

    if (enableErrorBoundary) {
      return (
        <LazyErrorBoundary moduleName={options.moduleName}>
          {content}
        </LazyErrorBoundary>
      );
    }

    return content;
  };
}