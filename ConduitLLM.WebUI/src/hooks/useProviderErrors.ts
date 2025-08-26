import { useState, useEffect, useCallback } from 'react';
import { withAdminClient } from '@/lib/client/adminClient';
import type { components } from '@knn_labs/conduit-admin-client';

type ProviderErrorDto = components['schemas']['ConduitLLM.Admin.DTOs.ProviderErrorDto'];
type ProviderErrorSummaryDto = components['schemas']['ConduitLLM.Admin.DTOs.ProviderErrorSummaryDto'];
type ErrorStatisticsDto = components['schemas']['ConduitLLM.Admin.DTOs.ErrorStatisticsDto'];

interface UseProviderErrorsReturn {
  stats: ErrorStatisticsDto | null;
  summaries: ProviderErrorSummaryDto[];
  recentErrors: ProviderErrorDto[];
  isLoading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
}

export function useProviderErrors(hours: number = 24): UseProviderErrorsReturn {
  const [stats, setStats] = useState<ErrorStatisticsDto | null>(null);
  const [summaries, setSummaries] = useState<ProviderErrorSummaryDto[]>([]);
  const [recentErrors, setRecentErrors] = useState<ProviderErrorDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      // Fetch all data in parallel
      const [statsData, summariesData, errorsData] = await Promise.all([
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

      setStats(statsData);
      setSummaries(summariesData);
      setRecentErrors(errorsData);
    } catch (err) {
      console.error('Failed to fetch provider error data:', err);
      setError('Failed to load provider error data. Please try again.');
    } finally {
      setIsLoading(false);
    }
  }, [hours]);

  useEffect(() => {
    void fetchData();
  }, [fetchData]);

  return {
    stats,
    summaries,
    recentErrors,
    isLoading,
    error,
    refresh: fetchData,
  };
}