import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { UpdateVirtualKeyRequest } from '../../../models/virtualKey';

interface UpdateVirtualKeyVariables {
  id: number;
  data: UpdateVirtualKeyRequest;
}

export function useUpdateVirtualKey(
  options?: UseMutationOptions<void, Error, UpdateVirtualKeyVariables>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: UpdateVirtualKeyVariables) => adminClient.virtualKeys.update(id, data),
    onSuccess: (_: void, { id }: UpdateVirtualKeyVariables) => {
      // Invalidate the specific virtual key and the list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.virtualKey(id.toString()) });
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.virtualKeys() });
    },
    ...options,
  });
}