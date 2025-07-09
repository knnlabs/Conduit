import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { ProviderCredentialDto } from '../../../models/provider';

interface UseProviderOptions {
  id?: number;
  name?: string;
}

export function useProvider(
  { id, name }: UseProviderOptions,
  options?: Omit<UseQueryOptions<ProviderCredentialDto>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  if (!id && !name) {
    throw new Error('Either id or name must be provided');
  }

  return useQuery({
    queryKey: id ? adminQueryKeys.provider(id.toString()) : adminQueryKeys.provider(name!),
    queryFn: () => (id ? adminClient.providers.getById(id) : adminClient.providers.getByName(name!)),
    enabled: Boolean(id || name),
    staleTime: 60000, // 1 minute
    ...options,
  });
}