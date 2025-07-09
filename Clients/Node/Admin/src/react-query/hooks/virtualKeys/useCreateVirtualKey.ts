import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { CreateVirtualKeyRequest, CreateVirtualKeyResponse } from '../../../models/virtualKey';

export function useCreateVirtualKey(
  options?: UseMutationOptions<CreateVirtualKeyResponse, Error, CreateVirtualKeyRequest>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateVirtualKeyRequest) => adminClient.virtualKeys.create(data),
    onSuccess: () => {
      // Invalidate and refetch virtual keys list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.virtualKeys() });
      
      // Note: We can't cache the new key directly because the response
      // contains the actual key value which won't be in the list response
    },
    ...options,
  });
}