import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { RequestLogDto } from '../../../models/analytics';

export function useRequestLog(
  id: string,
  options?: Omit<UseQueryOptions<RequestLogDto>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.requestLog(id),
    queryFn: () => adminClient.analytics.getRequestLog(id),
    enabled: !!id,
    ...options,
  });
}