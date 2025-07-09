import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { KeyUsageDto } from '../../../models/analytics';
import type { DateRange } from '../../../models/common';

export interface CostByKeyResponse {
  keys: KeyUsageDto[];
  totalCost: number;
}

export function useCostByKey(
  dateRange: DateRange,
  options?: Omit<UseQueryOptions<CostByKeyResponse>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.costByKey(dateRange),
    queryFn: () => adminClient.analytics.getCostByKey(dateRange),
    staleTime: 5 * 60 * 1000, // 5 minutes
    ...options,
  });
}