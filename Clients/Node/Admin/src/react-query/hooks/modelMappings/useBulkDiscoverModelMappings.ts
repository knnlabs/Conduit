import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { DiscoveredModel } from '../../../models/modelMapping';

export function useBulkDiscoverModelMappings(
  options?: UseMutationOptions<DiscoveredModel[], Error, void>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => adminClient.modelMappings.discoverModels(),
    onSuccess: () => {
      // Invalidate model mappings queries to refresh the list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.modelMappings() });
    },
    ...options,
  });
}