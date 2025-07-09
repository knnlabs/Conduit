import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { CreateProviderCredentialDto, ProviderCredentialDto } from '../../../models/provider';

export function useCreateProvider(
  options?: UseMutationOptions<ProviderCredentialDto, Error, CreateProviderCredentialDto>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateProviderCredentialDto) => adminClient.providers.create(data),
    onSuccess: (newProvider: ProviderCredentialDto) => {
      // Invalidate and refetch providers list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.providers() });
      
      // Add the new provider to the cache
      queryClient.setQueryData(
        adminQueryKeys.provider(newProvider.id.toString()),
        newProvider
      );
    },
    ...options,
  });
}