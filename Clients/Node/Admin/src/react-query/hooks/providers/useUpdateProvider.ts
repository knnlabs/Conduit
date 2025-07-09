import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { UpdateProviderCredentialDto } from '../../../models/provider';

interface UpdateProviderVariables {
  id: number;
  data: UpdateProviderCredentialDto;
}

export function useUpdateProvider(
  options?: UseMutationOptions<void, Error, UpdateProviderVariables>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: UpdateProviderVariables) => adminClient.providers.update(id, data),
    onSuccess: (_: void, { id }: UpdateProviderVariables) => {
      // Invalidate the specific provider and the list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.provider(id.toString()) });
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.providers() });
      // Also invalidate provider models in case they changed
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.providerModels(id.toString()) });
    },
    ...options,
  });
}