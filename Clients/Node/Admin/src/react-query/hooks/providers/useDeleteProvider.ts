import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';

export function useDeleteProvider(
  options?: UseMutationOptions<void, Error, number>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => adminClient.providers.deleteById(id),
    onSuccess: (_: void, id: number) => {
      // Remove the provider from cache
      queryClient.removeQueries({ queryKey: adminQueryKeys.provider(id.toString()) });
      
      // Invalidate the providers list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.providers() });
      
      // Also invalidate model mappings as they might be affected
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.modelMappings() });
    },
    ...options,
  });
}