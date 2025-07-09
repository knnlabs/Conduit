import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { SystemInfoDto } from '../../../models/system';

export interface UseSystemInfoOptions extends Omit<UseQueryOptions<SystemInfoDto, Error>, 'queryKey' | 'queryFn'> {}

export function useSystemInfo(options?: UseSystemInfoOptions) {
  const { adminClient } = useConduitAdmin();
  
  return useQuery<SystemInfoDto, Error>({
    queryKey: adminQueryKeys.system.info(),
    queryFn: () => adminClient.system.getSystemInfo(),
    staleTime: 30000, // 30 seconds
    ...options,
  });
}