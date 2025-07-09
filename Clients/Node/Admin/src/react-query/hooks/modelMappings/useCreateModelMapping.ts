import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { CreateModelProviderMappingDto, ModelProviderMappingDto } from '../../../models/modelMapping';

export function useCreateModelMapping(
  options?: UseMutationOptions<ModelProviderMappingDto, Error, CreateModelProviderMappingDto>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateModelProviderMappingDto) => adminClient.modelMappings.create(data),
    onSuccess: (newMapping: ModelProviderMappingDto) => {
      // Invalidate and refetch model mappings list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.modelMappings() });
      
      // Cache the new mapping
      queryClient.setQueryData(
        adminQueryKeys.modelMapping(newMapping.id.toString()),
        newMapping
      );
    },
    ...options,
  });
}