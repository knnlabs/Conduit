import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { ModelProviderMappingDto, ModelMappingFilters } from '../../../models/modelMapping';

export function useModelMappings(
  filters?: ModelMappingFilters,
  options?: Omit<UseQueryOptions<ModelProviderMappingDto[]>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: [...adminQueryKeys.modelMappings(), { filters }],
    queryFn: () => adminClient.modelMappings.list(filters),
    staleTime: 60000, // 1 minute
    ...options,
  });
}