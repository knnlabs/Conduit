'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  Table,
  Badge,
  Group,
  Text,
  ActionIcon,
  Menu,
  ScrollArea,
  Center,
} from '@mantine/core';
import {
  IconEye,
  IconRefresh,
  IconTrash,
  IconDotsVertical,
  IconChevronUp,
  IconChevronDown,
} from '@tabler/icons-react';
import { formatBytes, formatRelativeTime } from '@/utils/formatters';
import { useClearQueue } from '@/hooks/useErrorQueues';
import { ConfirmationModal } from '@/components/common/ConfirmationModal';
import type { ErrorQueueInfo } from '@knn_labs/conduit-admin-client';

interface ErrorQueueTableProps {
  queues: ErrorQueueInfo[];
  onRefresh: () => void;
}

type SortField = 'queueName' | 'messageCount' | 'messageBytes' | 'oldestMessage';
type SortDirection = 'asc' | 'desc';

export function ErrorQueueTable({ queues }: ErrorQueueTableProps) {
  const router = useRouter();
  const [sortField, setSortField] = useState<SortField>('messageCount');
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc');
  const [selectedQueue, setSelectedQueue] = useState<string | null>(null);
  const [showClearConfirm, setShowClearConfirm] = useState(false);

  const clearQueueMutation = useClearQueue();

  const handleSort = (field: SortField) => {
    if (field === sortField) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDirection('desc');
    }
  };

  const getFieldValue = (queue: ErrorQueueInfo, field: SortField): string | number => {
    switch (field) {
      case 'queueName':
        return queue.queueName;
      case 'messageCount':
        return queue.messageCount;
      case 'messageBytes':
        return queue.messageBytes;
      case 'oldestMessage':
        return queue.oldestMessageTimestamp
          ? new Date(queue.oldestMessageTimestamp).getTime()
          : 0;
      default:
        return '';
    }
  };
  
  const sortedQueues = [...queues].sort((a, b) => {
    const aValue = getFieldValue(a, sortField);
    const bValue = getFieldValue(b, sortField);

    if (sortDirection === 'asc') {
      return aValue > bValue ? 1 : -1;
    } else {
      return aValue < bValue ? 1 : -1;
    }
  });

  const getStatusBadge = (status: string) => {
    let color: string;
    if (status === 'critical') {
      color = 'red';
    } else if (status === 'warning') {
      color = 'yellow';
    } else {
      color = 'green';
    }
    return (
      <Badge size="sm" variant="light" color={color}>
        {status.toUpperCase()}
      </Badge>
    );
  };

  const SortHeader = ({
    field,
    children,
  }: {
    field: SortField;
    children: React.ReactNode;
  }) => (
    <th style={{ cursor: 'pointer' }} onClick={() => handleSort(field)}>
      <Group gap="xs">
        {children}
        {sortField === field &&
          (sortDirection === 'asc' ? (
            <IconChevronUp size={14} />
          ) : (
            <IconChevronDown size={14} />
          ))}
      </Group>
    </th>
  );

  const handleClearQueue = async () => {
    if (selectedQueue) {
      await clearQueueMutation.mutateAsync(selectedQueue);
      setShowClearConfirm(false);
      setSelectedQueue(null);
    }
  };

  if (queues.length === 0) {
    return (
      <Center py="xl">
        <Text c="dimmed">No error queues found</Text>
      </Center>
    );
  }

  return (
    <>
      <ScrollArea>
        <Table striped highlightOnHover>
          <thead>
            <tr>
              <SortHeader field="queueName">Queue Name</SortHeader>
              <th>Original Queue</th>
              <SortHeader field="messageCount">Messages</SortHeader>
              <SortHeader field="messageBytes">Size</SortHeader>
              <SortHeader field="oldestMessage">Oldest Message</SortHeader>
              <th>Message Rate</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {sortedQueues.map((queue) => (
              <tr key={queue.queueName}>
                <td>
                  <Text
                    fw={600}
                    style={{ cursor: 'pointer' }}
                    onClick={() => void router.push(`/error-queues/${encodeURIComponent(queue.queueName)}`)}
                    c="blue"
                  >
                    {queue.queueName}
                  </Text>
                </td>
                <td>
                  <Text size="sm" c="dimmed">
                    {queue.originalQueue}
                  </Text>
                </td>
                <td>
                  <Text fw={queue.messageCount > 100 ? 700 : 400}>
                    {queue.messageCount.toLocaleString()}
                  </Text>
                </td>
                <td>{formatBytes(queue.messageBytes)}</td>
                <td>
                  {queue.oldestMessageTimestamp ? (
                    <Text size="sm">
                      {formatRelativeTime(new Date(queue.oldestMessageTimestamp))}
                    </Text>
                  ) : (
                    <Text size="sm" c="dimmed">
                      -
                    </Text>
                  )}
                </td>
                <td>
                  <Text size="sm">
                    {queue.messageRate > 0
                      ? `${queue.messageRate.toFixed(1)}/min`
                      : '-'}
                  </Text>
                </td>
                <td>{getStatusBadge(queue.status)}</td>
                <td>
                  <Group gap="xs">
                    <ActionIcon
                      variant="subtle"
                      onClick={() => void router.push(`/error-queues/${encodeURIComponent(queue.queueName)}`)}
                    >
                      <IconEye size={16} />
                    </ActionIcon>
                    <Menu position="bottom-end">
                      <Menu.Target>
                        <ActionIcon variant="subtle">
                          <IconDotsVertical size={16} />
                        </ActionIcon>
                      </Menu.Target>
                      <Menu.Dropdown>
                        <Menu.Item
                          leftSection={<IconRefresh size={14} />}
                          disabled
                        >
                          Replay All Messages
                        </Menu.Item>
                        <Menu.Item
                          leftSection={<IconTrash size={14} />}
                          color="red"
                          onClick={() => {
                            setSelectedQueue(queue.queueName);
                            setShowClearConfirm(true);
                          }}
                        >
                          Clear Queue
                        </Menu.Item>
                      </Menu.Dropdown>
                    </Menu>
                  </Group>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      </ScrollArea>

      <ConfirmationModal
        opened={showClearConfirm}
        onClose={() => {
          setShowClearConfirm(false);
          setSelectedQueue(null);
        }}
        onConfirm={() => void handleClearQueue()}
        title="Clear Error Queue"
        message={
          selectedQueue
            ? `Are you sure you want to delete all messages from ${selectedQueue}? This action cannot be undone.`
            : ''
        }
        confirmText="Clear Queue"
        confirmColor="red"
        loading={clearQueueMutation.isPending}
      />
    </>
  );
}