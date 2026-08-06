import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';
import { OverallMediaStorageStats } from '../types';

export function useMediaStats() {
  const query = useQuery({
    queryKey: ['media-stats'],
    queryFn: async () => {
      const [stats, providerStats, typeStats] = await Promise.all([
        withAdminClient(client => client.media.getMediaStats('overall')),
        withAdminClient(client => client.media.getMediaStats('by-provider')),
        withAdminClient(client => client.media.getMediaStats('by-type')),
      ]);
      return { stats, providerStats, typeStats };
    },
  });

  return {
    stats: query.data?.stats ?? null as OverallMediaStorageStats | null,
    providerStats: query.data?.providerStats ?? {},
    typeStats: query.data?.typeStats ?? {},
    isLoading: query.isFetching,
    error: query.error instanceof Error ? query.error.message : null,
    refetch: async () => {
      await query.refetch();
    },
  };
}
