import { useMutation, UseMutationOptions, useQueryClient } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';

export interface UseMarkAllNotificationsReadOptions extends Omit<UseMutationOptions<number, Error, void>, 'mutationFn'> {}

export function useMarkAllNotificationsRead(options?: UseMarkAllNotificationsReadOptions) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation<number, Error, void>({
    mutationFn: () => adminClient.notifications.markAllAsRead(),
    onSuccess: (data: number, variables: void, context: unknown) => {
      // Invalidate all notification-related queries
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.notifications.all() });
      
      // Call the original onSuccess if provided
      options?.onSuccess?.(data, variables, context);
    },
    ...options,
  });
}