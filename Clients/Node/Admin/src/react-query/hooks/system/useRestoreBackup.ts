import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { RestoreResult, RestoreOptions } from '../../../models/databaseBackup';

export interface UseRestoreBackupOptions extends Omit<UseMutationOptions<RestoreResult, Error, { backupId: string; options?: RestoreOptions }>, 'mutationFn'> {}

export function useRestoreBackup(options?: UseRestoreBackupOptions) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation<RestoreResult, Error, { backupId: string; options?: RestoreOptions }>({
    mutationFn: ({ backupId, options: restoreOptions }: { backupId: string; options?: RestoreOptions }) => 
      adminClient.databaseBackup.restoreBackup(backupId, restoreOptions),
    onSuccess: (data: RestoreResult, variables: { backupId: string; options?: RestoreOptions }, context: unknown) => {
      // Invalidate all queries as the database has been restored
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.all });
      
      // Call the original onSuccess if provided
      options?.onSuccess?.(data, variables, context);
    },
    ...options,
  });
}