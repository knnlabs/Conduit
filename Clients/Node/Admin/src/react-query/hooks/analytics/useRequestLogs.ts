import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { RequestLogDto, RequestLogFilters } from '../../../models/analytics';
import type { PaginatedResponse } from '../../../models/common';

export function useRequestLogs(
  filters?: RequestLogFilters,
  options?: Omit<UseQueryOptions<PaginatedResponse<RequestLogDto>>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.requestLogs(filters),
    queryFn: () => adminClient.analytics.getRequestLogs(filters),
    staleTime: 30 * 1000, // 30 seconds
    ...options,
  });
}