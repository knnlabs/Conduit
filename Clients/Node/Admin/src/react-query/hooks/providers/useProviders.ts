import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { ProviderCredentialDto, ProviderFilters } from '../../../models/provider';

export function useProviders(
  filters?: ProviderFilters,
  options?: Omit<UseQueryOptions<ProviderCredentialDto[]>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.providers(),
    queryFn: () => adminClient.providers.list(filters),
    staleTime: 60000, // 1 minute
    ...options,
  });
}