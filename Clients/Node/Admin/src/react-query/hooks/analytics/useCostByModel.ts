import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { ModelUsageDto } from '../../../models/analytics';
import type { DateRange } from '../../../models/common';

export interface CostByModelResponse {
  models: ModelUsageDto[];
  totalCost: number;
}

export function useCostByModel(
  dateRange: DateRange,
  options?: Omit<UseQueryOptions<CostByModelResponse>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.costByModel(dateRange),
    queryFn: () => adminClient.analytics.getCostByModel(dateRange),
    staleTime: 5 * 60 * 1000, // 5 minutes
    ...options,
  });
}