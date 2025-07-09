import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';

export function useDeleteModelMapping(
  options?: UseMutationOptions<void, Error, number>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => adminClient.modelMappings.deleteById(id),
    onSuccess: (_: void, id: number) => {
      // Remove the mapping from cache
      queryClient.removeQueries({ queryKey: adminQueryKeys.modelMapping(id.toString()) });
      
      // Invalidate the mappings list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.modelMappings() });
    },
    ...options,
  });
}