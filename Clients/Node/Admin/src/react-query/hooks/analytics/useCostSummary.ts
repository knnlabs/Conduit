import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { CostSummaryDto } from '../../../models/analytics';
import type { DateRange } from '../../../models/common';

export function useCostSummary(
  dateRange: DateRange,
  options?: Omit<UseQueryOptions<CostSummaryDto>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.costSummary(dateRange),
    queryFn: () => adminClient.analytics.getCostSummary(dateRange),
    staleTime: 5 * 60 * 1000, // 5 minutes
    ...options,
  });
}