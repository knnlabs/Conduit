'use client';

import { useQuery } from '@tanstack/react-query';

import { withAdminClient } from '@/lib/client/adminClient';

// React Query hook for fetching providers
export function useProviders() {
  const { data: providers, isLoading, error, refetch } = useQuery({
    queryKey: ['providers'],
    queryFn: async () => {
      const result = await withAdminClient(client =>
        client.providers.list(1, 1000)
      );
      return result.data ?? [];
    },
  });

  return {
    providers: providers ?? [],
    isLoading,
    error,
    refetch,
  };
}
