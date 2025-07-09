import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { CostByPeriodDto } from '../../../models/analytics';
import type { DateRange } from '../../../models/common';

export interface UseCostByPeriodOptions {
  dateRange: DateRange;
  groupBy?: 'hour' | 'day' | 'week' | 'month';
}

export function useCostByPeriod(
  { dateRange, groupBy = 'day' }: UseCostByPeriodOptions,
  options?: Omit<UseQueryOptions<CostByPeriodDto>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.costByPeriod({ dateRange, groupBy }),
    queryFn: () => adminClient.analytics.getCostByPeriod(dateRange, groupBy),
    staleTime: 5 * 60 * 1000, // 5 minutes
    ...options,
  });
}