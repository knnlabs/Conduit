import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { NotificationSummary } from '../../../models/system';

export interface UseNotificationSummaryOptions extends Omit<UseQueryOptions<NotificationSummary, Error>, 'queryKey' | 'queryFn'> {}

export function useNotificationSummary(options?: UseNotificationSummaryOptions) {
  const { adminClient } = useConduitAdmin();
  
  return useQuery({
    queryKey: adminQueryKeys.system.notifications.summary(),
    queryFn: () => adminClient.notifications.getNotificationSummary(),
    staleTime: 5000, // 5 seconds for summary
    ...options,
  });
}