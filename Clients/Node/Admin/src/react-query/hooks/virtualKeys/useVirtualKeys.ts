import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { VirtualKeyDto, VirtualKeyFilters } from '../../../models/virtualKey';

export function useVirtualKeys(
  filters?: VirtualKeyFilters,
  options?: Omit<UseQueryOptions<VirtualKeyDto[]>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: [...adminQueryKeys.virtualKeys(), { filters }],
    queryFn: () => adminClient.virtualKeys.list(filters),
    staleTime: 30000, // 30 seconds
    ...options,
  });
}