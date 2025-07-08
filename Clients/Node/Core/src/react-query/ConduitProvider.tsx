'use client';

import React, { createContext, useContext, useMemo } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConduitCoreClient } from '../client/ConduitCoreClient';
import type { ClientConfig } from '../client/types';

interface ConduitContextValue {
  client: ConduitCoreClient;
  virtualKey: string;
}

const ConduitContext = createContext<ConduitContextValue | undefined>(undefined);

export interface ConduitProviderProps {
  children: React.ReactNode;
  virtualKey: string;
  baseUrl?: string;
  queryClient?: QueryClient;
  config?: Partial<ClientConfig>;
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

export function ConduitProvider({
  children,
  virtualKey,
  baseUrl = '/api/core',
  queryClient = defaultQueryClient,
  config = {},
}: ConduitProviderProps) {
  const client = useMemo(
    () =>
      new ConduitCoreClient({
        apiKey: virtualKey,
        baseURL: baseUrl,
        ...config,
      }),
    [virtualKey, baseUrl, config]
  );

  const contextValue = useMemo(
    () => ({
      client,
      virtualKey,
    }),
    [client, virtualKey]
  );

  return (
    <ConduitContext.Provider value={contextValue}>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </ConduitContext.Provider>
  );
}

export function useConduit() {
  const context = useContext(ConduitContext);
  if (!context) {
    throw new Error('useConduit must be used within a ConduitProvider');
  }
  return context;
}