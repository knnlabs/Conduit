import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { UsageMetricsDto } from '../../../models/analytics';
import type { DateRange } from '../../../models/common';

export function useUsageMetrics(
  dateRange: DateRange,
  options?: Omit<UseQueryOptions<UsageMetricsDto>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.usageMetrics(dateRange),
    queryFn: () => adminClient.analytics.getUsageMetrics(dateRange),
    staleTime: 60 * 1000, // 1 minute
    ...options,
  });
}