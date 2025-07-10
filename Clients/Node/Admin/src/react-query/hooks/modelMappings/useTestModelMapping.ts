import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import { CapabilityTestResult } from '../../../models/modelMapping';

interface TestModelMappingParams {
  modelAlias: string;
  capability: string;
}

export function useTestModelMapping(
  options?: UseMutationOptions<CapabilityTestResult, Error, TestModelMappingParams>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ modelAlias, capability }: TestModelMappingParams) =>
      adminClient.modelMappings.testCapability(modelAlias, capability),
    onSuccess: () => {
      // Invalidate model mappings queries to refresh the list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.modelMappings() });
    },
    ...options,
  });
}