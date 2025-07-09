import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { NotificationDto, NotificationFilters } from '../../../models/system';

export interface UseNotificationsOptions extends Omit<UseQueryOptions<NotificationDto[], Error>, 'queryKey' | 'queryFn'> {
  filters?: NotificationFilters;
}

export function useNotifications(options?: UseNotificationsOptions) {
  const { adminClient } = useConduitAdmin();
  const { filters, ...queryOptions } = options || {};
  
  return useQuery<NotificationDto[], Error>({
    queryKey: adminQueryKeys.system.notifications.list(filters),
    queryFn: () => filters 
      ? adminClient.notifications.getFilteredNotifications(filters)
      : adminClient.notifications.getAllNotifications(),
    staleTime: 10000, // 10 seconds for notifications
    ...queryOptions,
  });
}