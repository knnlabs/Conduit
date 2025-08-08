'use client';

import { useState } from 'react';
import {
  Table,
  ScrollArea,
  Text,
  Badge,
  ActionIcon,
  Group,
  Collapse,
  Box,
  Code,
  Stack,
  Loader,
  Center,
  Paper,
} from '@mantine/core';
import {
  IconChevronDown,
  IconChevronRight,
  IconEye,
  IconReload,
  IconTrash,
  IconCopy,
} from '@tabler/icons-react';
import { useErrorQueueMessages, useReplayMessage, useDeleteMessage } from '@/hooks/useErrorQueues';
import { MessageDetailModal } from './MessageDetailModal';
import { formatDateTime, formatRelativeTime } from '@/utils/formatters';
import { notifications } from '@mantine/notifications';
import type { ErrorMessage } from '@knn_labs/conduit-admin-client';

interface MessageListResponse {
  messages: ErrorMessage[];
  totalCount: number;
  page: number;
  pageSize: number;
}

interface MessageListProps {
  queueName: string;
  page: number;
  pageSize: number;
  searchQuery: string;
  dateFilter: string | null;
}

export function MessageList({
  queueName,
  page,
  pageSize,
  // searchQuery, // TODO: Implement search functionality
  // dateFilter, // TODO: Implement date filtering
}: MessageListProps) {
  const [expandedRows, setExpandedRows] = useState<Set<string>>(new Set());
  const [selectedMessage, setSelectedMessage] = useState<{
    queueName: string;
    messageId: string;
  } | null>(null);

  const { data, isLoading, error } = useErrorQueueMessages(queueName, {
    page,
    pageSize,
    includeHeaders: true,
    includeBody: true,
  });

  const replayMutation = useReplayMessage();
  const deleteMutation = useDeleteMessage();

  if (isLoading) {
    return (
      <Center h={300}>
        <Loader size="lg" />
      </Center>
    );
  }

  if (error) {
    return (
      <Paper shadow="xs" p="md" radius="md">
        <Text c="red">Failed to load messages: {error.message}</Text>
      </Paper>
    );
  }

  const messages = Array.isArray(data) ? data as ErrorMessage[] : (data as unknown as MessageListResponse)?.messages ?? [];

  const toggleRow = (messageId: string) => {
    setExpandedRows((prev) => {
      const newSet = new Set(prev);
      if (newSet.has(messageId)) {
        newSet.delete(messageId);
      } else {
        newSet.add(messageId);
      }
      return newSet;
    });
  };

  const handleReplay = async (messageId: string) => {
    await replayMutation.mutateAsync({ queueName, messageId });
  };

  const handleDelete = async (messageId: string) => {
    await deleteMutation.mutateAsync({ queueName, messageId });
  };

  // Using SDK ErrorMessage type - no need for local interface
  
  const handleCopyJson = (message: ErrorMessage) => {
    void navigator.clipboard.writeText(JSON.stringify(message, null, 2));
    notifications.show({
      title: 'Copied',
      message: 'Message JSON copied to clipboard',
      color: 'green',
    });
  };

  const getRetryBadge = (retryCount: number) => {
    let color: string;
    if (retryCount === 0) {
      color = 'gray';
    } else if (retryCount < 3) {
      color = 'yellow';
    } else {
      color = 'red';
    }
    return (
      <Badge size="sm" variant="light" color={color}>
        {retryCount} retries
      </Badge>
    );
  };

  if (messages.length === 0) {
    return (
      <Paper shadow="xs" p="xl" radius="md">
        <Center>
          <Text c="dimmed">No messages found in this queue</Text>
        </Center>
      </Paper>
    );
  }

  return (
    <>
      <ScrollArea>
        <Table>
          <thead>
            <tr>
              <th style={{ width: 40 }}></th>
              <th>Message ID</th>
              <th>Timestamp</th>
              <th>Message Type</th>
              <th>Correlation ID</th>
              <th>Retries</th>
              <th>Error Summary</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {messages.map((message: ErrorMessage) => {
              const isExpanded = expandedRows.has(message.messageId);
              return (
                <>
                  <tr key={message.messageId}>
                    <td>
                      <ActionIcon
                        variant="subtle"
                        onClick={() => toggleRow(message.messageId)}
                      >
                        {isExpanded ? (
                          <IconChevronDown size={16} />
                        ) : (
                          <IconChevronRight size={16} />
                        )}
                      </ActionIcon>
                    </td>
                    <td>
                      <Text size="sm" style={{ fontFamily: 'monospace' }}>
                        {message.messageId}
                      </Text>
                    </td>
                    <td>
                      <Text size="sm">
                        {formatRelativeTime(new Date(message.timestamp))}
                      </Text>
                    </td>
                    <td>
                      <Text size="sm" fw={500}>
                        {message.messageType}
                      </Text>
                    </td>
                    <td>
                      <Text size="sm" style={{ fontFamily: 'monospace' }}>
                        {message.correlationId ?? '-'}
                      </Text>
                    </td>
                    <td>{getRetryBadge(message.retryCount)}</td>
                    <td>
                      <Text size="sm" lineClamp={1}>
                        {message.error.message}
                      </Text>
                    </td>
                    <td>
                      <Group gap="xs">
                        <ActionIcon
                          variant="subtle"
                          onClick={() => void setSelectedMessage({ queueName, messageId: message.messageId })}
                        >
                          <IconEye size={16} />
                        </ActionIcon>
                        <ActionIcon
                          variant="subtle"
                          color="blue"
                          onClick={() => void handleReplay(message.messageId)}
                          loading={replayMutation.isPending}
                        >
                          <IconReload size={16} />
                        </ActionIcon>
                        <ActionIcon
                          variant="subtle"
                          onClick={() => handleCopyJson(message)}
                        >
                          <IconCopy size={16} />
                        </ActionIcon>
                        <ActionIcon
                          variant="subtle"
                          color="red"
                          onClick={() => void handleDelete(message.messageId)}
                          loading={deleteMutation.isPending}
                        >
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Group>
                    </td>
                  </tr>
                  {isExpanded && (
                    <tr>
                      <td colSpan={8} style={{ padding: 0 }}>
                        <Collapse in={isExpanded}>
                          <Box p="md" bg="gray.0">
                            <Stack gap="md">
                              <div>
                                <Text size="sm" fw={600} mb="xs">
                                  Error Details
                                </Text>
                                <Paper p="sm" radius="sm" withBorder>
                                  <Stack gap="xs">
                                    <Group>
                                      <Text size="sm" c="dimmed">
                                        Exception Type:
                                      </Text>
                                      <Code>{message.error.exceptionType}</Code>
                                    </Group>
                                    <Group>
                                      <Text size="sm" c="dimmed">
                                        Failed At:
                                      </Text>
                                      <Text size="sm">
                                        {formatDateTime(message.error.failedAt)}
                                      </Text>
                                    </Group>
                                    <div>
                                      <Text size="sm" c="dimmed" mb="xs">
                                        Error Message:
                                      </Text>
                                      <Text size="sm">{message.error.message}</Text>
                                    </div>
                                    {message.error.stackTrace && (
                                      <div>
                                        <Text size="sm" c="dimmed" mb="xs">
                                          Stack Trace:
                                        </Text>
                                        <Code block style={{ fontSize: '12px' }}>
                                          {message.error.stackTrace}
                                        </Code>
                                      </div>
                                    )}
                                  </Stack>
                                </Paper>
                              </div>

                              {message.body !== null && message.body !== undefined && (
                                <div>
                                  <Text size="sm" fw={600} mb="xs">
                                    Message Body
                                  </Text>
                                  <Code block style={{ fontSize: '12px' }}>
                                    {typeof message.body === 'string' 
                                      ? message.body 
                                      : JSON.stringify(message.body, null, 2)
                                    }
                                  </Code>
                                </div>
                              )}

                              {message.headers && Object.keys(message.headers).length > 0 && (
                                <div>
                                  <Text size="sm" fw={600} mb="xs">
                                    Headers
                                  </Text>
                                  <Code block style={{ fontSize: '12px' }}>
                                    {JSON.stringify(message.headers, null, 2)}
                                  </Code>
                                </div>
                              )}
                            </Stack>
                          </Box>
                        </Collapse>
                      </td>
                    </tr>
                  )}
                </>
              );
            })}
          </tbody>
        </Table>
      </ScrollArea>

      {selectedMessage && (
        <MessageDetailModal
          queueName={selectedMessage.queueName}
          messageId={selectedMessage.messageId}
          opened={!!selectedMessage}
          onClose={() => setSelectedMessage(null)}
        />
      )}
    </>
  );
}