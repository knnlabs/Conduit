import React, { lazy, Suspense } from 'react';

// Simple lazy loading utility
export function lazyLoadComponent<T extends React.ComponentType<Record<string, unknown>>>(
  importFunc: () => Promise<{ default: T }>
): React.LazyExoticComponent<T> {
  return lazy(importFunc);
}

export function withSuspense<P extends object>(
  Component: React.ComponentType<P>,
  fallback: React.ReactNode = <div>Loading...</div>
) {
  const WrappedComponent = (props: P) => (
    <Suspense fallback={fallback}>
      <Component {...props} />
    </Suspense>
  );
  
  WrappedComponent.displayName = `withSuspense(${Component.displayName ?? Component.name ?? 'Component'})`;
  
  return WrappedComponent;
}

// Lazy load a page component with suspense
export function lazyLoadPage<T extends React.ComponentType<Record<string, unknown>>>(
  importFunc: () => Promise<{ default: T }>,
  options?: React.ComponentType | { loadingMessage?: string; moduleName?: string }
) {
  const Component = lazy(importFunc);
  
  let Fallback: React.ComponentType;
  if (typeof options === 'function') {
    Fallback = options;
  } else if (options?.loadingMessage) {
    const LoadingFallback = () => <div>{options.loadingMessage}</div>;
    LoadingFallback.displayName = 'LoadingFallback';
    Fallback = LoadingFallback;
  } else {
    const DefaultFallback = () => <div>Loading...</div>;
    DefaultFallback.displayName = 'DefaultFallback';
    Fallback = DefaultFallback;
  }
  
  const LazyPage = (props: React.ComponentProps<T>) => (
    <Suspense fallback={<Fallback />}>
      <Component {...props} />
    </Suspense>
  );
  
  LazyPage.displayName = `LazyPage(${options && typeof options === 'object' && options.moduleName ? options.moduleName : 'UnknownPage'})`;
  
  return LazyPage;
}