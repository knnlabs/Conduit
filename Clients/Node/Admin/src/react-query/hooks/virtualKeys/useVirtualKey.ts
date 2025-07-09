import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { VirtualKeyDto } from '../../../models/virtualKey';

export function useVirtualKey(
  id: number,
  options?: Omit<UseQueryOptions<VirtualKeyDto>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.virtualKey(id.toString()),
    queryFn: () => adminClient.virtualKeys.getById(id),
    enabled: id > 0,
    staleTime: 60000, // 1 minute
    ...options,
  });
}