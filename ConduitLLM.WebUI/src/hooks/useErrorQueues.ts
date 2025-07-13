import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import type {
  ErrorQueueListResponse,
  ErrorMessageListResponse,
  ErrorMessageDetail,
  ErrorQueueStatistics,
  ErrorQueueHealth,
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

      const response = await fetch(`/api/error-queues${params.toString() ? `?${params.toString()}` : ''}`);
      if (!response.ok) {
        throw new Error('Failed to fetch error queues');
      }
      return response.json();
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

      const response = await fetch(`/api/error-queues/${encodeURIComponent(queueName)}/messages${params.toString() ? `?${params.toString()}` : ''}`);
      if (!response.ok) {
        throw new Error('Failed to fetch error messages');
      }
      return response.json();
    },
    enabled: !!queueName,
  });
}

export function useErrorMessage(queueName: string, messageId: string) {
  return useQuery({
    queryKey: ['error-message', queueName, messageId],
    queryFn: async () => {
      const response = await fetch(`/api/error-queues/${encodeURIComponent(queueName)}/messages/${encodeURIComponent(messageId)}`);
      if (!response.ok) {
        throw new Error('Failed to fetch error message');
      }
      return response.json();
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

      const response = await fetch(`/api/error-queues/statistics${params.toString() ? `?${params.toString()}` : ''}`);
      if (!response.ok) {
        throw new Error('Failed to fetch error queue statistics');
      }
      return response.json();
    },
    staleTime: 60000, // Statistics can be cached for 1 minute
  });
}

export function useErrorQueueHealth() {
  return useQuery({
    queryKey: ['error-queue-health'],
    queryFn: async () => {
      const response = await fetch('/api/error-queues/health');
      if (!response.ok) {
        throw new Error('Failed to fetch error queue health');
      }
      return response.json();
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
      return response.json();
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({
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
      return response.json();
    },
    onSuccess: (_, queueName) => {
      queryClient.invalidateQueries({ queryKey: ['error-queues'] });
      queryClient.invalidateQueries({
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
      return response.json();
    },
    onSuccess: (_, queueName) => {
      queryClient.invalidateQueries({ queryKey: ['error-queues'] });
      queryClient.invalidateQueries({
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
      return response.json();
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['error-queues'] });
      queryClient.invalidateQueries({
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