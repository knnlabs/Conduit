import React, { lazy, Suspense } from 'react';

// Simple lazy loading utility
export function lazyLoadComponent<T extends React.ComponentType<any>>(
  importFunc: () => Promise<{ default: T }>
): React.LazyExoticComponent<T> {
  return lazy(importFunc);
}

export function withSuspense<P extends object>(
  Component: React.ComponentType<P>,
  fallback: React.ReactNode = <div>Loading...</div>
) {
  return (props: P) => (
    <Suspense fallback={fallback}>
      <Component {...props} />
    </Suspense>
  );
}

// Lazy load a page component with suspense
export function lazyLoadPage<T extends React.ComponentType<any>>(
  importFunc: () => Promise<{ default: T }>,
  options?: React.ComponentType | { loadingMessage?: string; moduleName?: string }
) {
  const Component = lazy(importFunc);
  
  let Fallback: React.ComponentType;
  if (typeof options === 'function') {
    Fallback = options;
  } else if (options?.loadingMessage) {
    Fallback = () => <div>{options.loadingMessage}</div>;
  } else {
    Fallback = () => <div>Loading...</div>;
  }
  
  return (props: React.ComponentProps<T>) => (
    <Suspense fallback={<Fallback />}>
      <Component {...props} />
    </Suspense>
  );
}