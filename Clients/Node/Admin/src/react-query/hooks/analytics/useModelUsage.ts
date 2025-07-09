import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { ModelUsageDto } from '../../../models/analytics';
import type { DateRange } from '../../../models/common';

export interface UseModelUsageOptions {
  modelId: string;
  dateRange: DateRange;
}

export function useModelUsage(
  { modelId, dateRange }: UseModelUsageOptions,
  options?: Omit<UseQueryOptions<ModelUsageDto>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.modelUsage(modelId, dateRange),
    queryFn: () => adminClient.analytics.getModelUsage(modelId, dateRange),
    enabled: !!modelId,
    staleTime: 5 * 60 * 1000, // 5 minutes
    ...options,
  });
}