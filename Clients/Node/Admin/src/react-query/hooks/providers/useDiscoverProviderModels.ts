import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { ProviderModel } from '../../../services/ProviderModelsService';

interface DiscoverModelsVariables {
  providerId: string;
  options?: {
    forceRefresh?: boolean;
    virtualKey?: string;
  };
}

export function useDiscoverProviderModels(
  options?: UseMutationOptions<ProviderModel[], Error, DiscoverModelsVariables>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ providerId, options }: DiscoverModelsVariables) => 
      adminClient.providerModels.getProviderModels(providerId, options),
    onSuccess: (data: ProviderModel[], { providerId }: DiscoverModelsVariables) => {
      // Update the provider models cache
      queryClient.setQueryData(adminQueryKeys.providerModels(providerId), data);
      
      // Also invalidate model mappings as new models might be available
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.modelMappings() });
    },
    ...options,
  });
}