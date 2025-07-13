'use client';

import { Modal, Tabs, Stack, Text, Code, Group, Button, Badge, Timeline, Loader, Center } from '@mantine/core';
import { IconFileCode, IconList, IconBug, IconHistory, IconCopy, IconReload, IconTrash } from '@tabler/icons-react';
import { useErrorMessage, useReplayMessage, useDeleteMessage } from '@/hooks/useErrorQueues';
import { formatDateTime } from '@/utils/formatters';
import { notifications } from '@mantine/notifications';

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
    if (message) {
      navigator.clipboard.writeText(JSON.stringify(message, null, 2));
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
      {isLoading ? (
        <Center h={400}>
          <Loader size="lg" />
        </Center>
      ) : message ? (
        <Stack>
          <Group justify="space-between" align="flex-start">
            <Stack gap="xs">
              <Text size="sm" c="dimmed">
                Message ID
              </Text>
              <Code>{message.messageId}</Code>
            </Stack>
            <Group>
              <Button
                leftSection={<IconReload size={16} />}
                variant="light"
                onClick={handleReplay}
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
                onClick={handleDelete}
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
              {message.body ? (
                <Code block style={{ fontSize: '12px' }}>
                  {JSON.stringify(message.body, null, 2)}
                </Code>
              ) : (
                <Text c="dimmed">No message body available</Text>
              )}
            </Tabs.Panel>

            <Tabs.Panel value="headers" pt="md">
              {message.headers && Object.keys(message.headers).length > 0 ? (
                <Stack gap="sm">
                  {Object.entries(message.headers).map(([key, value]) => (
                    <Group key={key} justify="space-between" align="flex-start">
                      <Text size="sm" fw={500}>
                        {key}:
                      </Text>
                      <Code style={{ maxWidth: '70%', overflow: 'auto' }}>
                        {typeof value === 'object' ? JSON.stringify(value) : String(value)}
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
                  <Code>{message.error.exceptionType}</Code>
                </Group>
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">
                    Failed At
                  </Text>
                  <Text size="sm">{formatDateTime(message.error.failedAt)}</Text>
                </Group>
                <div>
                  <Text size="sm" c="dimmed" mb="xs">
                    Error Message
                  </Text>
                  <Text size="sm">{message.error.message}</Text>
                </div>
                {message.error.stackTrace && (
                  <div>
                    <Text size="sm" c="dimmed" mb="xs">
                      Stack Trace
                    </Text>
                    <Code block style={{ fontSize: '12px', whiteSpace: 'pre-wrap' }}>
                      {message.error.stackTrace}
                    </Code>
                  </div>
                )}
                {message.fullException && (
                  <div>
                    <Text size="sm" c="dimmed" mb="xs">
                      Full Exception Details
                    </Text>
                    <Code block style={{ fontSize: '12px', whiteSpace: 'pre-wrap' }}>
                      {message.fullException}
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
                    color={message.retryCount === 0 ? 'gray' : message.retryCount < 3 ? 'yellow' : 'red'}
                  >
                    {message.retryCount} retries
                  </Badge>
                </Group>

                <Timeline active={-1} bulletSize={20}>
                  <Timeline.Item title="Message Created">
                    <Text size="xs" c="dimmed">
                      {formatDateTime(message.timestamp)}
                    </Text>
                    <Text size="sm">Message was originally sent</Text>
                  </Timeline.Item>
                  
                  {Array.from({ length: message.retryCount }).map((_, index) => (
                    <Timeline.Item key={index} title={`Retry ${index + 1}`} color="red">
                      <Text size="sm">Failed with: {message.error.exceptionType}</Text>
                    </Timeline.Item>
                  ))}
                  
                  <Timeline.Item title="Current State" color="red">
                    <Text size="xs" c="dimmed">
                      {formatDateTime(message.error.failedAt)}
                    </Text>
                    <Text size="sm">Message is in error queue</Text>
                  </Timeline.Item>
                </Timeline>

                {message.context && Object.keys(message.context).length > 0 && (
                  <div>
                    <Text size="sm" fw={600} mb="xs">
                      Additional Context
                    </Text>
                    <Code block style={{ fontSize: '12px' }}>
                      {JSON.stringify(message.context, null, 2)}
                    </Code>
                  </div>
                )}
              </Stack>
            </Tabs.Panel>
          </Tabs>
        </Stack>
      ) : (
        <Text c="dimmed">Message not found</Text>
      )}
    </Modal>
  );
}