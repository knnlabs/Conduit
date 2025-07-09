import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { BackupInfo } from '../../../models/databaseBackup';

export interface UseBackupOptions extends Omit<UseQueryOptions<BackupInfo, Error>, 'queryKey' | 'queryFn'> {}

export function useBackup(backupId: string, options?: UseBackupOptions) {
  const { adminClient } = useConduitAdmin();
  
  return useQuery({
    queryKey: adminQueryKeys.system.backups.detail(backupId),
    queryFn: () => adminClient.databaseBackup.getBackupInfo(backupId),
    enabled: !!backupId,
    staleTime: 30000, // 30 seconds
    ...options,
  });
}