import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { BackupResult, BackupOptions } from '../../../models/databaseBackup';

export interface UseCreateBackupOptions extends Omit<UseMutationOptions<BackupResult, Error, BackupOptions | undefined>, 'mutationFn'> {}

export function useCreateBackup(options?: UseCreateBackupOptions) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation<BackupResult, Error, BackupOptions | undefined>({
    mutationFn: (createOptions: BackupOptions | undefined) => adminClient.databaseBackup.createBackup(createOptions),
    onSuccess: (data: BackupResult, variables: BackupOptions | undefined, context: unknown) => {
      // Invalidate the backups list to refresh with the new backup
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.backups.list() });
      
      // Set the new backup data in the cache if creation was successful
      if (data.success && data.backupInfo) {
        queryClient.setQueryData(adminQueryKeys.system.backups.detail(data.backupInfo.id), data.backupInfo);
      }
      
      // Call the original onSuccess if provided
      options?.onSuccess?.(data, variables, context);
    },
    ...options,
  });
}