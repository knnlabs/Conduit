'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import { withAdminClient } from '@/lib/client/adminClient';
import { notify } from '@/lib/notifications';

import type { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

/**
 * Factory hook that wraps React Query's `useMutation` with standardized
 * notification handling and automatic query invalidation.
 *
 * Replaces the common pattern of:
 * ```ts
 * useMutation({
 *   mutationFn: ...,
 *   onSuccess: () => { invalidate(); notifications.show({ ... green }); },
 *   onError: (err) => { notifications.show({ ... red }); },
 * })
 * ```
 *
 * @example
 * // Simple CRUD mutation
 * export function useCreateMapping() {
 *   return useAdminMutation({
 *     mutationFn: (data: CreateDto) => client => client.mappings.create(data),
 *     successMessage: 'Mapping created',
 *     invalidateKeys: ['model-mappings'],
 *   });
 * }
 *
 * @example
 * // Dynamic success message based on result
 * export function useBulkDelete() {
 *   return useAdminMutation({
 *     mutationFn: (ids: number[]) => client => client.mappings.bulkDelete(ids),
 *     successMessage: (result) => `Deleted ${result.successCount} items`,
 *     invalidateKeys: ['model-mappings'],
 *   });
 * }
 */
export function useAdminMutation<TData, TVariables>(options: {
  /** SDK operation — receives variables and returns a function that takes the admin client. */
  mutationFn: (variables: TVariables) => (client: ConduitAdminClient) => Promise<TData>;
  /** Static string or function that derives a message from the mutation result. */
  successMessage: string | ((data: TData) => string);
  /** Query keys to invalidate on success. */
  invalidateKeys?: string[];
  /** Additional callback after success notification. */
  onSuccess?: (data: TData) => void;
  /** Additional callback after error notification. */
  onError?: (error: Error) => void;
  /** If true, suppresses the success notification. */
  silent?: boolean;
}) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (variables: TVariables) =>
      withAdminClient(options.mutationFn(variables)),

    onSuccess: (data: TData) => {
      if (options.invalidateKeys) {
        for (const key of options.invalidateKeys) {
          void queryClient.invalidateQueries({ queryKey: [key] });
        }
      }

      if (!options.silent) {
        const message =
          typeof options.successMessage === 'function'
            ? options.successMessage(data)
            : options.successMessage;
        notify.success(message);
      }

      options.onSuccess?.(data);
    },

    onError: (error: Error) => {
      notify.error(error);
      options.onError?.(error);
    },
  });
}
