'use client';

import React, { useMemo } from 'react';
import { ConduitProvider } from '@knn_labs/conduit-core-client/react-query';
import { ConduitAdminProvider } from '@knn_labs/conduit-admin-client/react-query';
import type { QueryClient } from '@tanstack/react-query';
import { useAuthStore } from '@/stores/useAuthStore';

interface ConduitProvidersProps {
  children: React.ReactNode;
  queryClient: QueryClient;
}

export function ConduitProviders({ children, queryClient }: ConduitProvidersProps) {
  const user = useAuthStore((state) => state.user);
  
  // Use the virtual key from auth store for Core SDK
  const virtualKey = useMemo(() => {
    return user?.virtualKey || '';
  }, [user?.virtualKey]);

  // Use the master key from auth store for Admin SDK
  const masterKey = useMemo(() => {
    return user?.masterKey || '';
  }, [user?.masterKey]);

  // API URLs from environment
  const coreApiUrl = process.env.NEXT_PUBLIC_CORE_API_URL || 'http://localhost:5001';
  const adminApiUrl = process.env.NEXT_PUBLIC_ADMIN_API_URL || 'http://localhost:5001';

  if (!virtualKey || !masterKey) {
    // If no keys, just render children without providers
    // This allows the login page to render
    return <>{children}</>;
  }

  return (
    <ConduitProvider
      virtualKey={virtualKey}
      baseUrl={coreApiUrl}
      queryClient={queryClient}
    >
      <ConduitAdminProvider
        authKey={masterKey}
        baseUrl={adminApiUrl}
        queryClient={queryClient}
      >
        {children}
      </ConduitAdminProvider>
    </ConduitProvider>
  );
}