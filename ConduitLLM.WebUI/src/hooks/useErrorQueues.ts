import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import type { 
  MessageReplayResponse,
  MessageDeleteResponse,
  QueueClearResponse
} from '@knn_labs/conduit-admin-client';

interface ErrorQueueOptions {
  includeEmpty?: boolean;
  minMessages?: number;
  queueNameFilter?: string;
}

interface MessageListOptions {
  page?: number;
  pageSize?: number;
  includeHeaders?: boolean;
  includeBody?: boolean;
}

export function useErrorQueues(options?: ErrorQueueOptions) {
  return useQuery({
    queryKey: ['error-queues', options],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (options?.includeEmpty !== undefined) {
        params.append('includeEmpty', options.includeEmpty.toString());
      }
      if (options?.minMessages !== undefined) {
        params.append('minMessages', options.minMessages.toString());
      }
      if (options?.queueNameFilter) {
        params.append('queueNameFilter', options.queueNameFilter);
      }

      // Note: list method doesn't exist in errorQueues service
      // Using placeholder implementation
      // TODO: Implement error queue listing once SDK supports it
      return Promise.resolve([]);
    },
    refetchInterval: 30000, // Poll every 30 seconds
    staleTime: 20000, // Consider data stale after 20 seconds
  });
}

export function useErrorQueueMessages(
  queueName: string,
  options?: MessageListOptions
) {
  return useQuery({
    queryKey: ['error-messages', queueName, options],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (options?.page !== undefined) {
        params.append('page', options.page.toString());
      }
      if (options?.pageSize !== undefined) {
        params.append('pageSize', options.pageSize.toString());
      }
      if (options?.includeHeaders !== undefined) {
        params.append('includeHeaders', options.includeHeaders.toString());
      }
      if (options?.includeBody !== undefined) {
        params.append('includeBody', options.includeBody.toString());
      }

      // Note: getMessages method doesn't exist in errorQueues service
      // Using placeholder implementation
      // TODO: Implement error queue messages retrieval once SDK supports it
      return Promise.resolve([]);
    },
    enabled: !!queueName,
  });
}

export function useErrorMessage(queueName: string, messageId: string) {
  return useQuery({
    queryKey: ['error-message', queueName, messageId],
    queryFn: async () => {
      // Note: getMessage method doesn't exist in errorQueues service
      // Using placeholder implementation
      // TODO: Implement error queue message retrieval once SDK supports it
      return Promise.resolve(null);
    },
    enabled: !!queueName && !!messageId,
  });
}

export function useErrorQueueStatistics(options?: {
  since?: Date;
  groupBy?: 'hour' | 'day' | 'week';
}) {
  return useQuery({
    queryKey: ['error-queue-statistics', options],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (options?.since) {
        params.append('since', options.since.toISOString());
      }
      if (options?.groupBy) {
        params.append('groupBy', options.groupBy);
      }

      // Note: getStatistics method doesn't exist in errorQueues service
      // Using placeholder implementation
      // TODO: Implement error queue statistics once SDK supports it
      return Promise.resolve({ totalMessages: 0, totalQueues: 0 });
    },
    staleTime: 60000, // Statistics can be cached for 1 minute
  });
}

export function useErrorQueueHealth() {
  return useQuery({
    queryKey: ['error-queue-health'],
    queryFn: async () => {
      return withAdminClient(client => 
        client.errorQueues.getHealth()
      ) as unknown as Promise<{ isHealthy: boolean; queueCount: number }>;
    },
    refetchInterval: 30000, // Poll health every 30 seconds
  });
}

// Mutations for actions
export function useReplayMessage() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      queueName,
      messageId,
    }: {
      queueName: string;
      messageId: string;
    }) => {
      const response = await fetch(`/api/error-queues/${encodeURIComponent(queueName)}/messages/${encodeURIComponent(messageId)}/replay`, {
        method: 'POST',
      });
      if (!response.ok) {
        throw new Error('Failed to replay message');
      }
      return response.json() as Promise<MessageReplayResponse>;
    },
    onSuccess: (data, variables) => {
      void queryClient.invalidateQueries({
        queryKey: ['error-messages', variables.queueName],
      });
      notifications.show({
        title: 'Message Replayed',
        message: 'The message has been queued for replay',
        color: 'green',
      });
    },
    onError: (error) => {
      notifications.show({
        title: 'Replay Failed',
        message: error.message,
        color: 'red',
      });
    },
  });
}

export function useReplayAllMessages() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (queueName: string) => {
      const response = await fetch(`/api/error-queues/${encodeURIComponent(queueName)}/replay-all`, {
        method: 'POST',
      });
      if (!response.ok) {
        throw new Error('Failed to replay all messages');
      }
      return response.json() as Promise<MessageReplayResponse>;
    },
    onSuccess: (data, queueName) => {
      void queryClient.invalidateQueries({ queryKey: ['error-queues'] });
      void queryClient.invalidateQueries({
        queryKey: ['error-messages', queueName],
      });
      notifications.show({
        title: 'Messages Replayed',
        message: 'All messages have been queued for replay',
        color: 'green',
      });
    },
    onError: (error) => {
      notifications.show({
        title: 'Replay Failed',
        message: error.message,
        color: 'red',
      });
    },
  });
}

export function useClearQueue() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (queueName: string) => {
      const response = await fetch(`/api/error-queues/${encodeURIComponent(queueName)}/clear`, {
        method: 'DELETE',
      });
      if (!response.ok) {
        throw new Error('Failed to clear queue');
      }
      return response.json() as Promise<QueueClearResponse>;
    },
    onSuccess: (data, queueName) => {
      void queryClient.invalidateQueries({ queryKey: ['error-queues'] });
      void queryClient.invalidateQueries({
        queryKey: ['error-messages', queueName],
      });
      notifications.show({
        title: 'Queue Cleared',
        message: 'All messages have been removed from the queue',
        color: 'green',
      });
    },
    onError: (error) => {
      notifications.show({
        title: 'Clear Failed',
        message: error.message,
        color: 'red',
      });
    },
  });
}

export function useDeleteMessage() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      queueName,
      messageId,
    }: {
      queueName: string;
      messageId: string;
    }) => {
      const response = await fetch(`/api/error-queues/${encodeURIComponent(queueName)}/messages/${encodeURIComponent(messageId)}`, {
        method: 'DELETE',
      });
      if (!response.ok) {
        throw new Error('Failed to delete message');
      }
      return response.json() as Promise<MessageDeleteResponse>;
    },
    onSuccess: (data, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['error-queues'] });
      void queryClient.invalidateQueries({
        queryKey: ['error-messages', variables.queueName],
      });
      notifications.show({
        title: 'Message Deleted',
        message: 'The message has been removed from the queue',
        color: 'green',
      });
    },
    onError: (error) => {
      notifications.show({
        title: 'Delete Failed',
        message: error.message,
        color: 'red',
      });
    },
  });
}