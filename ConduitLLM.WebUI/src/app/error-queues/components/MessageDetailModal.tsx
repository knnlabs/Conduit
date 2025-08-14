'use client';

import { Modal, Tabs, Stack, Text, Code, Group, Button, Badge, Timeline, Loader, Center } from '@mantine/core';
import { IconFileCode, IconList, IconBug, IconHistory, IconCopy, IconReload, IconTrash } from '@tabler/icons-react';
import { useErrorMessage, useReplayMessage, useDeleteMessage } from '@/hooks/useErrorQueues';
import { formatDateTime } from '@/utils/formatters';
import { notifications } from '@mantine/notifications';

interface ErrorQueueMessage {
  messageId: string;
  body: string;
  headers: Record<string, string>;
  error: {
    message: string;
    stackTrace?: string;
    fullException?: string;
    exceptionType?: string;
    failedAt?: string;
  };
  retryCount: number;
  timestamp: string;
  context?: Record<string, unknown>;
}

interface MessageDetailModalProps {
  queueName: string;
  messageId: string;
  opened: boolean;
  onClose: () => void;
}

export function MessageDetailModal({
  queueName,
  messageId,
  opened,
  onClose,
}: MessageDetailModalProps) {
  const { data: message, isLoading } = useErrorMessage(queueName, messageId);
  const typedMessage = message as ErrorQueueMessage | null;
  const replayMutation = useReplayMessage();
  const deleteMutation = useDeleteMessage();

  const handleReplay = async () => {
    await replayMutation.mutateAsync({ queueName, messageId });
    notifications.show({
      title: 'Message Replayed',
      message: 'The message has been queued for replay',
      color: 'green',
    });
  };

  const handleDelete = async () => {
    await deleteMutation.mutateAsync({ queueName, messageId });
    onClose();
    notifications.show({
      title: 'Message Deleted',
      message: 'The message has been removed from the queue',
      color: 'green',
    });
  };

  const handleCopyJson = () => {
    if (typedMessage) {
      void navigator.clipboard.writeText(JSON.stringify(typedMessage, null, 2));
      notifications.show({
        title: 'Copied',
        message: 'Message JSON copied to clipboard',
        color: 'green',
      });
    }
  };

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title="Message Details"
      size="xl"
      padding="lg"
    >
      {(() => {
        if (isLoading) {
          return (
            <Center h={400}>
              <Loader size="lg" />
            </Center>
          );
        }
        if (message) {
          return (
            <Stack>
          <Group justify="space-between" align="flex-start">
            <Stack gap="xs">
              <Text size="sm" c="dimmed">
                Message ID
              </Text>
              <Code>{typedMessage?.messageId}</Code>
            </Stack>
            <Group>
              <Button
                leftSection={<IconReload size={16} />}
                variant="light"
                onClick={() => void handleReplay()}
                loading={replayMutation.isPending}
              >
                Replay
              </Button>
              <Button
                leftSection={<IconCopy size={16} />}
                variant="light"
                onClick={handleCopyJson}
              >
                Copy JSON
              </Button>
              <Button
                leftSection={<IconTrash size={16} />}
                variant="light"
                color="red"
                onClick={() => void handleDelete()}
                loading={deleteMutation.isPending}
              >
                Delete
              </Button>
            </Group>
          </Group>

          <Tabs defaultValue="body">
            <Tabs.List>
              <Tabs.Tab value="body" leftSection={<IconFileCode size={16} />}>
                Message Body
              </Tabs.Tab>
              <Tabs.Tab value="headers" leftSection={<IconList size={16} />}>
                Headers
              </Tabs.Tab>
              <Tabs.Tab value="error" leftSection={<IconBug size={16} />}>
                Error Details
              </Tabs.Tab>
              <Tabs.Tab value="history" leftSection={<IconHistory size={16} />}>
                History
              </Tabs.Tab>
            </Tabs.List>

            <Tabs.Panel value="body" pt="md">
              {typedMessage?.body ? (
                <Code block style={{ fontSize: '12px' }}>
                  {JSON.stringify(typedMessage?.body, null, 2)}
                </Code>
              ) : (
                <Text c="dimmed">No message body available</Text>
              )}
            </Tabs.Panel>

            <Tabs.Panel value="headers" pt="md">
              {typedMessage?.headers && Object.keys(typedMessage?.headers).length > 0 ? (
                <Stack gap="sm">
                  {Object.entries(typedMessage?.headers).map(([key, value]) => (
                    <Group key={key} justify="space-between" align="flex-start">
                      <Text size="sm" fw={500}>
                        {key}:
                      </Text>
                      <Code style={{ maxWidth: '70%', overflow: 'auto' }}>
                        {typeof value === 'object' && value !== null ? JSON.stringify(value) : String(value)}
                      </Code>
                    </Group>
                  ))}
                </Stack>
              ) : (
                <Text c="dimmed">No headers available</Text>
              )}
            </Tabs.Panel>

            <Tabs.Panel value="error" pt="md">
              <Stack>
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">
                    Exception Type
                  </Text>
                  <Code>{typedMessage?.error.exceptionType}</Code>
                </Group>
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">
                    Failed At
                  </Text>
                  <Text size="sm">{formatDateTime(typedMessage?.error?.failedAt ?? '')}</Text>
                </Group>
                <div>
                  <Text size="sm" c="dimmed" mb="xs">
                    Error Message
                  </Text>
                  <Text size="sm">{typedMessage?.error.message}</Text>
                </div>
                {typedMessage?.error.stackTrace && (
                  <div>
                    <Text size="sm" c="dimmed" mb="xs">
                      Stack Trace
                    </Text>
                    <Code block style={{ fontSize: '12px', whiteSpace: 'pre-wrap' }}>
                      {typedMessage?.error.stackTrace}
                    </Code>
                  </div>
                )}
                {typedMessage?.error?.fullException && (
                  <div>
                    <Text size="sm" c="dimmed" mb="xs">
                      Full Exception Details
                    </Text>
                    <Code block style={{ fontSize: '12px', whiteSpace: 'pre-wrap' }}>
                      {typedMessage?.error?.fullException}
                    </Code>
                  </div>
                )}
              </Stack>
            </Tabs.Panel>

            <Tabs.Panel value="history" pt="md">
              <Stack>
                <Group justify="space-between" mb="md">
                  <Text size="sm" c="dimmed">
                    Retry Count
                  </Text>
                  <Badge
                    size="lg"
                    variant="light"
                    color={(() => {
                      if (typedMessage?.retryCount ?? 0 === 0) return 'gray';
                      if (typedMessage?.retryCount ?? 0 < 3) return 'yellow';
                      return 'red';
                    })()}
                  >
                    {typedMessage?.retryCount ?? 0} retries
                  </Badge>
                </Group>

                <Timeline active={-1} bulletSize={20}>
                  <Timeline.Item title="Message Created">
                    <Text size="xs" c="dimmed">
                      {formatDateTime(typedMessage?.timestamp ?? '')}
                    </Text>
                    <Text size="sm">Message was originally sent</Text>
                  </Timeline.Item>
                  
                  {Array.from({ length: typedMessage?.retryCount ?? 0 }).map((item, index) => (
                    <Timeline.Item key={`retry-${typedMessage?.messageId}-attempt-${index + 1}`} title={`Retry ${index + 1}`} color="red">
                      <Text size="sm">Failed with: {typedMessage?.error.exceptionType}</Text>
                    </Timeline.Item>
                  ))}
                  
                  <Timeline.Item title="Current State" color="red">
                    <Text size="xs" c="dimmed">
                      {formatDateTime(typedMessage?.error?.failedAt ?? '')}
                    </Text>
                    <Text size="sm">Message is in error queue</Text>
                  </Timeline.Item>
                </Timeline>

                {typedMessage?.context && Object.keys(typedMessage?.context).length > 0 && (
                  <div>
                    <Text size="sm" fw={600} mb="xs">
                      Additional Context
                    </Text>
                    <Code block style={{ fontSize: '12px' }}>
                      {JSON.stringify(typedMessage?.context, null, 2)}
                    </Code>
                  </div>
                )}
              </Stack>
            </Tabs.Panel>
          </Tabs>
            </Stack>
          );
        }
        return <Text c="dimmed">Message not found</Text>;
      })()}
    </Modal>
  );
}