import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { RequestLogDto, RequestLogFilters } from '../../../models/analytics';

export interface UseSearchLogsOptions {
  query: string;
  filters?: RequestLogFilters;
}

export function useSearchLogs(
  { query, filters }: UseSearchLogsOptions,
  options?: Omit<UseQueryOptions<RequestLogDto[]>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.searchLogs(query, filters),
    queryFn: () => adminClient.analytics.searchLogs(query, filters),
    enabled: !!query,
    staleTime: 30 * 1000, // 30 seconds
    ...options,
  });
}