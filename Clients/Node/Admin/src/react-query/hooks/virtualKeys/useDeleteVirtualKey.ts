import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';

export function useDeleteVirtualKey(
  options?: UseMutationOptions<void, Error, number>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => adminClient.virtualKeys.deleteById(id),
    onSuccess: (_: void, id: number) => {
      // Remove the virtual key from cache
      queryClient.removeQueries({ queryKey: adminQueryKeys.virtualKey(id.toString()) });
      
      // Invalidate the virtual keys list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.virtualKeys() });
    },
    ...options,
  });
}