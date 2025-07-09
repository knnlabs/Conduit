import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { ModelProviderMappingDto } from '../../../models/modelMapping';

export function useModelMapping(
  id: number,
  options?: Omit<UseQueryOptions<ModelProviderMappingDto>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.modelMapping(id.toString()),
    queryFn: () => adminClient.modelMappings.getById(id),
    enabled: id > 0,
    staleTime: 60000, // 1 minute
    ...options,
  });
}