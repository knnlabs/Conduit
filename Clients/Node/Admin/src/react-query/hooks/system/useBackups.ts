import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { BackupInfo } from '../../../models/databaseBackup';

export interface UseBackupsOptions extends Omit<UseQueryOptions<BackupInfo[], Error>, 'queryKey' | 'queryFn'> {}

export function useBackups(options?: UseBackupsOptions) {
  const { adminClient } = useConduitAdmin();
  
  return useQuery({
    queryKey: adminQueryKeys.system.backups.list(),
    queryFn: () => adminClient.databaseBackup.getBackups(),
    staleTime: 30000, // 30 seconds
    ...options,
  });
}