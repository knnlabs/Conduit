import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { UpdateModelProviderMappingDto } from '../../../models/modelMapping';

interface UpdateModelMappingVariables {
  id: number;
  data: UpdateModelProviderMappingDto;
}

export function useUpdateModelMapping(
  options?: UseMutationOptions<void, Error, UpdateModelMappingVariables>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: UpdateModelMappingVariables) => adminClient.modelMappings.update(id, data),
    onSuccess: (_: void, { id }: UpdateModelMappingVariables) => {
      // Invalidate the specific mapping and the list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.modelMapping(id.toString()) });
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.modelMappings() });
    },
    ...options,
  });
}