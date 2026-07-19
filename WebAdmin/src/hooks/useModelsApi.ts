'use client';

import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';

export function useModels() {
  const { data: models, isLoading, error, refetch } = useQuery({
    queryKey: ['models'],
    queryFn: () => withAdminClient(client => client.models.list()),
  });

  return {
    models: models ?? [],
    isLoading,
    error,
    refetch,
  };
}
