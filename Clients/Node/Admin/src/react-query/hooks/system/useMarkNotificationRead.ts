import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';

export interface UseMarkNotificationReadOptions extends Omit<UseMutationOptions<void, Error, number>, 'mutationFn'> {}

export function useMarkNotificationRead(options?: UseMarkNotificationReadOptions) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation<void, Error, number>({
    mutationFn: (notificationId: number) => adminClient.notifications.markAsRead(notificationId),
    onSuccess: (data: void, notificationId: number, context: unknown) => {
      // Invalidate notifications list and summary
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.notifications.all() });
      
      // Call the original onSuccess if provided
      options?.onSuccess?.(data, notificationId, context);
    },
    ...options,
  });
}