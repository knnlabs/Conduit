import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';

export interface UseDeleteBackupOptions extends Omit<UseMutationOptions<void, Error, string>, 'mutationFn'> {}

export function useDeleteBackup(options?: UseDeleteBackupOptions) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (backupId: string) => adminClient.databaseBackup.deleteBackup(backupId),
    onSuccess: (data: void, backupId: string, context: unknown) => {
      // Invalidate the backups list
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.backups.list() });
      
      // Remove the deleted backup from the cache
      queryClient.removeQueries({ queryKey: adminQueryKeys.system.backups.detail(backupId) });
      
      // Call the original onSuccess if provided
      options?.onSuccess?.(data, backupId, context);
    },
    ...options,
  });
}