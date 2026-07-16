import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';

const SERIES_QUERY_KEY = 'model-series';

/**
 * Hook to fetch a single model series by ID with React Query caching.
 * Uses the full series list endpoint and filters client-side to benefit
 * from shared cache across all useModelSeriesById consumers.
 */
export function useModelSeriesById(seriesId: number | null | undefined) {
  const { data: allSeries, isLoading: loading, error } = useQuery({
    queryKey: [SERIES_QUERY_KEY],
    queryFn: () => withAdminClient(client => client.modelSeries.list()),
    staleTime: 5 * 60 * 1000, // 5 minutes
  });

  const series = allSeries?.find(s => s.id === seriesId);

  return {
    seriesName: series?.name ?? (seriesId ? `Series ${seriesId}` : null),
    seriesParameters: series?.parameters ?? null,
    loading,
    error,
  };
}
