'use client';

import React, { createContext, useContext, useMemo } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConduitAdminClient } from '../client/ConduitAdminClient';
import type { ConduitConfig } from '../client/types';

interface ConduitAdminContextValue {
  adminClient: ConduitAdminClient;
  authKey: string;
}

const ConduitAdminContext = createContext<ConduitAdminContextValue | undefined>(undefined);

export interface ConduitAdminProviderProps {
  children: React.ReactNode;
  authKey: string;
  baseUrl?: string;
  queryClient?: QueryClient;
  config?: Partial<ConduitConfig>;
}

const defaultQueryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 3,
      retryDelay: (attemptIndex: number) => Math.min(1000 * 2 ** attemptIndex, 30000),
      staleTime: 5 * 60 * 1000, // 5 minutes
      gcTime: 10 * 60 * 1000, // 10 minutes
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: 1,
    },
  },
});

export function ConduitAdminProvider({
  children,
  authKey,
  baseUrl = '/api/admin',
  queryClient = defaultQueryClient,
  config = {},
}: ConduitAdminProviderProps) {
  const adminClient = useMemo(
    () =>
      new ConduitAdminClient({
        masterKey: authKey,
        adminApiUrl: baseUrl,
        conduitApiUrl: config?.conduitApiUrl,
        options: config?.options,
      }),
    [authKey, baseUrl, config]
  );

  const contextValue = useMemo(
    () => ({
      adminClient,
      authKey,
    }),
    [adminClient, authKey]
  );

  return (
    <ConduitAdminContext.Provider value={contextValue}>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </ConduitAdminContext.Provider>
  );
}

export function useConduitAdmin() {
  const context = useContext(ConduitAdminContext);
  if (!context) {
    throw new Error('useConduitAdmin must be used within a ConduitAdminProvider');
  }
  return context;
}