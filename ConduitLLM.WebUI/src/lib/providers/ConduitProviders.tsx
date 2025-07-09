'use client';

import React, { useMemo } from 'react';
import { ConduitProvider } from '@knn_labs/conduit-core-client/react-query';
import type { QueryClient } from '@tanstack/react-query';
import { useAuthStore } from '@/stores/useAuthStore';

interface ConduitProvidersProps {
  children: React.ReactNode;
  queryClient: QueryClient;
}

export function ConduitProviders({ children, queryClient }: ConduitProvidersProps) {
  const user = useAuthStore((state) => state.user);
  
  // Use the virtual key from auth store for Core SDK
  const apiKey = useMemo(() => {
    return user?.virtualKey || '';
  }, [user?.virtualKey]);

  // Core API URL from environment
  const baseURL = process.env.NEXT_PUBLIC_CORE_API_URL || 'http://localhost:5001';

  if (!apiKey) {
    // If no API key, just render children without provider
    // This allows the login page to render
    return <>{children}</>;
  }

  return (
    <ConduitProvider
      virtualKey={apiKey}
      baseUrl={baseURL}
      queryClient={queryClient}
    >
      {children}
    </ConduitProvider>
  );
}