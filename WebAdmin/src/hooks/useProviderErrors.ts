import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';
import type { components } from '@/lib/admin-api';

type ProviderErrorDto = components['schemas']['ProviderErrorDto'];
type ProviderErrorSummaryDto = components['schemas']['ProviderErrorSummaryDto'];
type ErrorStatisticsDto = components['schemas']['ErrorStatisticsDto'];

interface UseProviderErrorsReturn {
  stats: ErrorStatisticsDto | null;
  summaries: ProviderErrorSummaryDto[];
  recentErrors: ProviderErrorDto[];
  isLoading: boolean;
  error: string | null;
  refetch: () => Promise<void>;
}

export function useProviderErrors(hours: number = 24): UseProviderErrorsReturn {
  const query = useQuery({
    queryKey: ['provider-errors', hours],
    queryFn: async () => {
      const [stats, summaries, recentErrors] = await Promise.all([
        withAdminClient(client =>
          client.providerErrors.getStatistics(hours)
        ),
        withAdminClient(client =>
          client.providerErrors.getSummary()
        ),
        withAdminClient(client =>
          client.providerErrors.getRecentErrors({ limit: 100 })
        ),
      ]);
      return { stats, summaries, recentErrors };
    },
  });

  return {
    stats: query.data?.stats ?? null,
    summaries: query.data?.summaries ?? [],
    recentErrors: query.data?.recentErrors ?? [],
    isLoading: query.isFetching,
    error: query.error
      ? 'Failed to load provider error data. Please try again.'
      : null,
    refetch: async () => {
      await query.refetch();
    },
  };
}
