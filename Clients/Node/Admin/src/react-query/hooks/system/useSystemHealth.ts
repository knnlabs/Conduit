import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { HealthStatusDto } from '../../../models/system';

export interface UseSystemHealthOptions extends Omit<UseQueryOptions<HealthStatusDto, Error>, 'queryKey' | 'queryFn'> {}

export function useSystemHealth(options?: UseSystemHealthOptions) {
  const { adminClient } = useConduitAdmin();
  
  return useQuery({
    queryKey: adminQueryKeys.system.health(),
    queryFn: () => adminClient.system.getHealth(),
    staleTime: 10000, // 10 seconds for health checks
    ...options,
  });
}