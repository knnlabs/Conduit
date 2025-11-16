'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';

interface QueryProviderProps {
  children: React.ReactNode;
}

export function QueryProvider({ children }: QueryProviderProps) {
  const [queryClient] = useState(
    () => new QueryClient({
      defaultOptions: {
        queries: {
          // Balanced configuration for admin site with fresh data requirements
          staleTime: 0,                    // Data is always considered stale
          gcTime: 5 * 1000,                // Keep in memory for 5 seconds (prevents duplicate requests)
          refetchOnWindowFocus: true,      // Refetch when user returns to tab (keeps data in sync)
          refetchOnMount: true,            // Always fetch fresh data on component mount
          refetchOnReconnect: true,        // Refetch when network reconnects
          retry: 1,                        // Retry failed requests once
          retryDelay: 1000,               // Wait 1 second before retry
        },
        mutations: {
          retry: 0,                        // Don't retry mutations (prevent duplicate creates/updates)
        },
      },
    })
  );

  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}